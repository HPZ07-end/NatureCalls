#!/usr/bin/env python3
"""Build the Linux dedicated server and deploy it to the course server in one command.

Steps: check the working tree, build with scripts/unity.py build-server, upload, unpack into
/opt/naturecalls/releases/<release>, switch the `current` link, restart the running rooms, verify each
room listens again, and roll back to the previous release if any check fails. Python standard library only.
"""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import re
import shlex
import subprocess
import sys
import tarfile
import tempfile

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_HOST = "ubuntu@49.233.132.244"
DEFAULT_PORTS = [8000]
REMOTE_ROOT = "/opt/naturecalls"
SERVICE = ROOT / "scripts/naturecalls@.service"
ROOM_SERVICE = ROOT / "scripts/naturecalls-room@.service"
BROKER_SERVICE = ROOT / "scripts/naturecalls-broker.service"
BROKER_SCRIPT = ROOT / "scripts/room_broker.py"
# Server-side staging copy of the last uploaded build: rsync only sends the blocks that changed since then.
REMOTE_STAGE = "/tmp/naturecalls-stage"


def run(command, **kwargs):
    print("$ " + shlex.join(command), flush=True)
    return subprocess.run(command, check=True, **kwargs)


def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, check=True, capture_output=True, text=True).stdout.strip()


def tracked_changes():
    """Tracked files that differ from HEAD; untracked files do not affect the build."""
    return [line for line in git("status", "--porcelain", "--untracked-files=no").splitlines() if line.strip()]


def parse_build(output):
    """The server executable path from `unity.py build-server` output, or None if the build did not pass."""
    if not re.search(r'"status":\s*"passed"', output):
        return None
    match = re.search(r'"build":\s*"([^"]+)"', output)
    return Path(match.group(1)) if match else None


def already_current(link, commit):
    """Whether the server's `current` link (e.g. `releases/6537a3c-2026...`) already points at this commit."""
    name = link.strip().rstrip("/").rsplit("/", 1)[-1]
    return bool(commit) and not commit.endswith("-dirty") and name.startswith(commit + "-")


def remote_current(host):
    result = subprocess.run(["ssh", "-o", "BatchMode=yes", host, f"readlink {REMOTE_ROOT}/current"],
                            capture_output=True, text=True, timeout=30)
    return result.stdout.strip() if result.returncode == 0 else ""


def seed_command():
    """Remote shell: create the staging directory and, if empty, fill it from the current release."""
    return (f'mkdir -p {REMOTE_STAGE} && if [ -z "$(ls -A {REMOTE_STAGE})" ] && [ -d {REMOTE_ROOT}/current/ ]; '
            f'then cp -a {REMOTE_ROOT}/current/. {REMOTE_STAGE}/; fi')


def release_name(commit, build):
    return f"{commit}-{build.parent.name}"


def remote_script(release, archive, ports, keep, service_changed, broker_changed=False):
    """Bash run on the server with sudo. Switches `current` atomically and rolls back if a room fails."""
    rooms = " ".join(str(port) for port in ports)
    return f"""set -euo pipefail
ROOT={REMOTE_ROOT}
NEW=$ROOT/releases/{release}
PREV=$(readlink "$ROOT/current" || true)
mkdir -p "$NEW"
mkdir -p "$NEW/scripts"
{f'tar -xzf {archive} -C "$NEW"' if archive.endswith(".tar.gz") else f'cp -a {archive}/. "$NEW"/'}
chmod +x "$NEW/NatureCalls"
chown -R root:root "$NEW"
{"install -m 644 /tmp/naturecalls@.service /etc/systemd/system/naturecalls@.service" if service_changed else ":"}
install -m 644 /tmp/naturecalls-room@.service /etc/systemd/system/naturecalls-room@.service
install -m 644 /tmp/naturecalls-broker.service /etc/systemd/system/naturecalls-broker.service
install -m 755 /tmp/room_broker.py "$ROOT/releases/{release}/scripts/room_broker.py"
systemctl daemon-reload
ln -sfn "releases/{release}" "$ROOT/current.next" && mv -T "$ROOT/current.next" "$ROOT/current"
STARTED=$(date '+%Y-%m-%d %H:%M:%S')
for port in {rooms}; do if [ "$port" = 8000 ]; then systemctl enable naturecalls@$port >/dev/null 2>&1 || true; systemctl restart naturecalls@$port; fi; done
ok=1
if {"systemctl restart naturecalls-broker.service" if broker_changed else "systemctl start naturecalls-broker.service"}; then :; else
  echo "BROKER_FAILED: systemctl restart"; ok=0
fi
broker_ok=0
for i in $(seq 1 10); do
  if curl --fail --silent --show-error --max-time 2 http://127.0.0.1:8100/health >/dev/null; then broker_ok=1; break; fi
  sleep 1
done
if [ "$broker_ok" != 1 ]; then echo "BROKER_FAILED: health check"; ok=0; fi
for port in {rooms}; do
  for i in $(seq 1 20); do
    if systemctl is-active --quiet naturecalls@$port && ss -lun | grep -q ":$port " \\
       && journalctl -u naturecalls@$port --since "$STARTED" --no-pager | grep -q "Server listening on UDP $port"; then
      echo "ROOM_OK $port"; continue 2
    fi
    sleep 1
  done
  echo "ROOM_FAILED $port"; journalctl -u naturecalls@$port --since "$STARTED" -n 30 --no-pager || true; ok=0
done
if [ "$ok" != 1 ]; then
  if [ -n "$PREV" ]; then
    ln -sfn "$PREV" "$ROOT/current.next" && mv -T "$ROOT/current.next" "$ROOT/current"
    for port in {rooms}; do systemctl restart naturecalls@$port || true; done
    systemctl restart naturecalls-broker.service || true
    echo "ROLLED_BACK $PREV"
  fi
  {f"rm -f {archive}" if archive.endswith(".tar.gz") else ":"}
  exit 1
fi
cd "$ROOT/releases"
ls -1t | grep -vx "{release}" | tail -n +{keep + 1} | xargs -r rm -rf
{f"rm -f {archive}" if archive.endswith(".tar.gz") else ": # staging copy kept for the next delta upload"}
echo "DEPLOYED {release}"
"""


def remote_ports(host):
    """Ports of the rooms currently running on the server, so a deploy restarts exactly those."""
    listing = subprocess.run(["ssh", "-o", "BatchMode=yes", host,
                              "systemctl list-units 'naturecalls@*' --state=active --plain --no-legend"],
                             check=True, capture_output=True, text=True).stdout
    ports = sorted({int(port) for port in re.findall(r"naturecalls@(\d+)\.service", listing)})
    return ports or DEFAULT_PORTS


def remote_service_differs(host):
    current = subprocess.run(["ssh", "-o", "BatchMode=yes", host, "cat /etc/systemd/system/naturecalls@.service"],
                             capture_output=True, text=True).stdout
    return current != SERVICE.read_text()


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", default=DEFAULT_HOST, help=f"SSH target with key login (default {DEFAULT_HOST})")
    parser.add_argument("--ports", type=int, nargs="+", help="rooms to restart (default: the ones running now, else 8000)")
    parser.add_argument("--build", type=Path, help="deploy an existing Builds/LinuxServer/<id>/NatureCalls instead of building")
    parser.add_argument("--keep", type=int, default=3, help="older releases to keep for rollback (default 3)")
    parser.add_argument("--allow-dirty", action="store_true", help="deploy even if tracked files have uncommitted changes")
    parser.add_argument("--dry-run", action="store_true", help="print the plan and the server script without changing anything")
    parser.add_argument("--tar", action="store_true",
                        help="upload the whole build as one archive instead of an rsync delta (slow on the course network)")
    parser.add_argument("--skip-if-current", action="store_true",
                        help="do nothing when the server already runs this commit (used by automatic deploys after builds)")
    args = parser.parse_args()
    if args.keep < 1:
        parser.error("--keep must be at least 1")

    changes = tracked_changes()
    if changes and not args.allow_dirty:
        print("ERROR: uncommitted tracked changes; commit them so the server matches a commit, or pass --allow-dirty:\n  "
              + "\n  ".join(changes), file=sys.stderr)
        return 1
    commit = git("rev-parse", "--short", "HEAD") + ("-dirty" if changes else "")
    if args.skip_if_current and not args.dry_run:
        link = remote_current(args.host)
        if already_current(link, commit):
            print(f"Server already runs {commit} ({link}); nothing to deploy.")
            return 0

    build = args.build.resolve() if args.build else None
    if build is None and not args.dry_run:
        print("Building the Linux dedicated server (close this project's Unity GUI first)...", flush=True)
        result = subprocess.run([sys.executable, str(ROOT / "scripts/unity.py"), "build-server", "--timeout", "3600"],
                                cwd=ROOT, capture_output=True, text=True)
        print(result.stdout[-2000:], end="")
        build = parse_build(result.stdout)
        if result.returncode != 0 or build is None:
            print("ERROR: build-server failed; nothing was deployed.\n" + result.stderr[-2000:], file=sys.stderr)
            return 1
    if build is not None and not build.is_file():
        parser.error(f"Server executable not found: {build}")

    # A dry run is intentionally offline: it must not probe or mutate the course server.
    ports = args.ports or (DEFAULT_PORTS if args.dry_run else remote_ports(args.host))
    release = release_name(commit, build) if build else f"{commit}-build-id"
    archive = f"/tmp/naturecalls-{release}.tar.gz" if args.tar else REMOTE_STAGE
    service_changed = True if args.dry_run else remote_service_differs(args.host)
    broker_changed = True
    script = remote_script(release, archive, ports, args.keep, service_changed, broker_changed)
    print(f"Release {release} -> {args.host}:{REMOTE_ROOT}/releases, rooms {ports}, dynamic rooms are left running; room template and broker service are always installed; broker restart: {broker_changed}")
    if args.dry_run:
        print(script)
        return 0

    with tempfile.TemporaryDirectory() as directory:
        if args.tar:
            package = Path(directory) / "server.tar.gz"
            with tarfile.open(package, "w:gz") as tar:
                for item in build.parent.iterdir():
                    tar.add(item, arcname=item.name)
            run(["scp", "-q", "-o", "BatchMode=yes", str(package), f"{args.host}:{archive}"])
        else:
            # Seed the staging copy from the running release once, then send only what changed.
            run(["ssh", "-o", "BatchMode=yes", args.host, seed_command()])
            run(["rsync", "-az", "--delete", "--partial", "--stats", "-e", "ssh -o BatchMode=yes",
                 f"{build.parent}/", f"{args.host}:{REMOTE_STAGE}/"])
        if service_changed:
            run(["scp", "-q", "-o", "BatchMode=yes", str(SERVICE), f"{args.host}:/tmp/naturecalls@.service"])
        run(["scp", "-q", "-o", "BatchMode=yes", str(ROOM_SERVICE), f"{args.host}:/tmp/naturecalls-room@.service"])
        run(["scp", "-q", "-o", "BatchMode=yes", str(BROKER_SERVICE), f"{args.host}:/tmp/naturecalls-broker.service"])
        run(["scp", "-q", "-o", "BatchMode=yes", str(BROKER_SCRIPT), f"{args.host}:/tmp/room_broker.py"])
    deploy = subprocess.run(["ssh", "-o", "BatchMode=yes", args.host, "sudo -n bash -s"], input=script,
                            capture_output=True, text=True)
    print(deploy.stdout + deploy.stderr, end="")

    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    record = ROOT / "Artifacts" / f"{stamp}-deploy-server"
    record.mkdir(parents=True, exist_ok=True)
    status = "deployed" if deploy.returncode == 0 else ("rolled back" if "ROLLED_BACK" in deploy.stdout else "failed")
    (record / "summary.json").write_text(json.dumps({
        "status": status, "release": release, "host": args.host, "ports": ports, "build": str(build),
        "serviceTemplateUpdated": service_changed, "output": deploy.stdout + deploy.stderr}, indent=2) + "\n")
    print(f"{status.upper()}: {release}; record {record}")
    return 0 if deploy.returncode == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
