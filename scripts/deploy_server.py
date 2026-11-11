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


def release_name(commit, build):
    return f"{commit}-{build.parent.name}"


def remote_script(release, archive, ports, keep, service_changed):
    """Bash run on the server with sudo. Switches `current` atomically and rolls back if a room fails."""
    rooms = " ".join(str(port) for port in ports)
    return f"""set -euo pipefail
ROOT={REMOTE_ROOT}
NEW=$ROOT/releases/{release}
PREV=$(readlink "$ROOT/current" || true)
mkdir -p "$NEW"
tar -xzf {archive} -C "$NEW"
chmod +x "$NEW/NatureCalls"
chown -R root:root "$NEW"
{"install -m 644 /tmp/naturecalls@.service /etc/systemd/system/naturecalls@.service && systemctl daemon-reload" if service_changed else ":"}
ln -sfn "releases/{release}" "$ROOT/current.next" && mv -T "$ROOT/current.next" "$ROOT/current"
STARTED=$(date '+%Y-%m-%d %H:%M:%S')
for port in {rooms}; do systemctl enable naturecalls@$port >/dev/null 2>&1 || true; systemctl restart naturecalls@$port; done
ok=1
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
    echo "ROLLED_BACK $PREV"
  fi
  rm -f {archive}
  exit 1
fi
cd "$ROOT/releases"
ls -1t | grep -vx "{release}" | tail -n +{keep + 1} | xargs -r rm -rf
rm -f {archive}
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
    args = parser.parse_args()
    if args.keep < 1:
        parser.error("--keep must be at least 1")

    changes = tracked_changes()
    if changes and not args.allow_dirty:
        print("ERROR: uncommitted tracked changes; commit them so the server matches a commit, or pass --allow-dirty:\n  "
              + "\n  ".join(changes), file=sys.stderr)
        return 1
    commit = git("rev-parse", "--short", "HEAD") + ("-dirty" if changes else "")

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

    ports = args.ports or remote_ports(args.host)
    release = release_name(commit, build) if build else f"{commit}-<build-id>"
    archive = f"/tmp/naturecalls-{release}.tar.gz"
    service_changed = remote_service_differs(args.host)
    script = remote_script(release, archive, ports, args.keep, service_changed)
    print(f"Release {release} -> {args.host}:{REMOTE_ROOT}/releases, rooms {ports}, service template changed: {service_changed}")
    if args.dry_run:
        print(script)
        return 0

    with tempfile.TemporaryDirectory() as directory:
        package = Path(directory) / "server.tar.gz"
        with tarfile.open(package, "w:gz") as tar:
            for item in build.parent.iterdir():
                tar.add(item, arcname=item.name)
        run(["scp", "-q", "-o", "BatchMode=yes", str(package), f"{args.host}:{archive}"])
        if service_changed:
            run(["scp", "-q", "-o", "BatchMode=yes", str(SERVICE), f"{args.host}:/tmp/naturecalls@.service"])
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
