#!/usr/bin/env python3
"""Repeatable local/public broker room and LAN client probes.

With --local this starts a localhost broker and fake-systemctl launcher. Without it,
--broker is treated as an already deployed broker. This checkout uses only --local.
"""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import os
import signal
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]


def request(url, method="GET", data=None):
    body = None if data is None else json.dumps(data).encode()
    req = urllib.request.Request(url, method=method, data=body, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=5) as response:
        return response.status, json.loads(response.read())


def endpoint(value):
    host, sep, port = value.rpartition(":")
    if not sep or not host or not port.isdigit() or not 1 <= int(port) <= 65535:
        raise argparse.ArgumentTypeError("broker must be host:port")
    return host, int(port)


def executable(path):
    path = Path(path)
    path = path.resolve()
    if path.suffix == ".app": path /= "Contents/MacOS/NatureCalls"
    if not path.is_file(): raise argparse.ArgumentTypeError(f"Player executable not found: {path}")
    return path


def wait_report(path, process, timeout=120):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline and not path.exists() and process.poll() is None:
        time.sleep(.2)
    if not path.exists(): raise RuntimeError(f"missing probe report: {path} (exit={process.poll()})")
    report = json.loads(path.read_text())
    if process.wait(timeout=10) != 0 or report.get("status") != "passed":
        raise RuntimeError(f"probe failed: {report}")
    return report


def launcher_text():
    return '''#!/usr/bin/env python3
import os, pathlib, signal, subprocess, sys
root=pathlib.Path(os.environ["ROOM_E2E_ROOT"]); server=pathlib.Path(os.environ["ROOM_E2E_SERVER"])
cmd=sys.argv[1]; unit=sys.argv[2] if len(sys.argv)>2 else ""
port=unit.rsplit("@",1)[-1].split(".")[0]; pidfile=root/(port+".pid"); logfile=root/"logs"/(port+".log")
if cmd == "start":
 p=subprocess.Popen([str(server),"-batchmode","-nographics","-server","-port",port,"-idle-exit","20","-logFile",str(logfile)], stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)
 pidfile.write_text(str(p.pid))
elif cmd == "stop":
 if pidfile.exists():
  try: os.kill(int(pidfile.read_text()), signal.SIGTERM)
  except ProcessLookupError: pass
elif cmd == "is-active":
 try: os.kill(int(pidfile.read_text()), 0); print("active"); sys.exit(0)
 except (FileNotFoundError, ProcessLookupError, ValueError): print("inactive"); sys.exit(3)
elif cmd == "list-units":
 for p in root.glob("*.pid"):
  try: os.kill(int(p.read_text()),0); print("naturecalls-room@"+p.stem+".service loaded active running")
  except (ProcessLookupError,ValueError): pass
'''


def start_client(player, directory, role, broker, extra, remote=True):
    log = (directory / f"{role}-process.log").open("w")
    args = [str(player), "-batchmode", "-screen-width", "1100", "-screen-height", "760",
            "-server-probe", "-probe-role", role,
            "-probe-directory", str(directory), "-broker", f"{broker[0]}:{broker[1]}",
            "-logFile", str(directory / f"{role}-player.log")] + (["-probe-remote"] if remote else []) + extra
    return subprocess.Popen(args, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT), log


def run_remote(player, broker, directory, close_timeout=60):
    leader, leader_log = start_client(player, directory, "leader", broker, ["-probe-create-server-room"])
    code_path = directory / "room-code.txt"
    deadline = time.monotonic() + 60
    while not code_path.exists() and leader.poll() is None and time.monotonic() < deadline:
        time.sleep(.2)
    if not code_path.exists(): raise RuntimeError("leader did not publish room-code.txt")
    code = code_path.read_text().strip()
    follower, follower_log = start_client(player, directory, "follower", broker, ["-probe-join", code])
    try:
        leader_report = wait_report(directory / "leader.json", leader)
        follower_report = wait_report(directory / "follower.json", follower)
        _, health = request(f"http://{broker[0]}:{broker[1]}/health")
        deadline = time.monotonic() + close_timeout
        while time.monotonic() < deadline:
            try: request(f"http://{broker[0]}:{broker[1]}/rooms/{code}")
            except urllib.error.HTTPError as error:
                if error.code == 404: break
                raise
            time.sleep(1)
        else: raise RuntimeError(f"room code was not removed within {close_timeout:.0f} seconds")
        summary = {"status": "passed", "mode": "remote-room", "code": int(code),
                   "leader": leader_report, "follower": follower_report, "health": health}
        (directory / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
        print(json.dumps(summary, indent=2))
    finally:
        for process in (leader, follower):
            if process.poll() is None: process.terminate()
        for log in (leader_log, follower_log): log.close()


def run_lan(player, directory):
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as reservation:
        reservation.bind(("127.0.0.1", 0)); port = reservation.getsockname()[1]
    host, host_log = start_client(player, directory, "lan-host", ("127.0.0.1", 0),
                                  ["-probe-lan-host", "-port", str(port)], remote=False)
    time.sleep(3)
    client, client_log = start_client(player, directory, "lan-follower", ("127.0.0.1", 0),
                                      ["-probe-lan", "-port", str(port)], remote=False)
    try:
        host_report = wait_report(directory / "lan-host.json", host, 90)
        client_report = wait_report(directory / "lan-follower.json", client, 90)
        summary = {"status": "passed", "mode": "lan", "port": port,
                   "host": host_report, "client": client_report}
        (directory / "lan-summary.json").write_text(json.dumps(summary, indent=2) + "\n")
        print(json.dumps(summary, indent=2))
    finally:
        for process in (host, client):
            if process.poll() is None: process.terminate()
        for log in (host_log, client_log): log.close()


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("player", type=executable, help="macOS .app or Player executable")
    parser.add_argument("--broker", type=endpoint, required=True, help="broker host:port")
    parser.add_argument("--local", action="store_true", help="start local broker and fake systemctl server launcher")
    parser.add_argument("--lan", action="store_true", help="run the direct Host + LAN-list click probe")
    parser.add_argument("--close-timeout", type=float, help="seconds to wait for the room code to expire (default 60 local, 360 remote: rooms idle-exit after 300 s)")
    args = parser.parse_args(argv)
    directory = ROOT / "Artifacts" / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:6] + "-room-e2e")
    directory.mkdir(parents=True); (directory / "logs").mkdir()
    broker_process = None
    try:
        if args.local:
            root = directory / "launcher"; root.mkdir()
            launcher = root / "launcher.py"; launcher.write_text(launcher_text()); launcher.chmod(0o755)
            env = dict(os.environ, ROOM_E2E_ROOT=str(root), ROOM_E2E_SERVER=str(args.player))
            broker_process = subprocess.Popen([sys.executable, str(ROOT / "scripts/room_broker.py"), "--bind", "127.0.0.1",
                                                "--port", str(args.broker[1]), "--state", str(root / "rooms.json"),
                                                "--launcher", str(launcher), "--log-dir", str(root / "logs")], env=env)
            deadline = time.monotonic() + 10
            while time.monotonic() < deadline:
                try:
                    request(f"http://127.0.0.1:{args.broker[1]}/health"); break
                except OSError: time.sleep(.1)
            else: raise RuntimeError("local broker did not start")
        if args.lan: run_lan(args.player, directory)
        else: run_remote(args.player, args.broker, directory, args.close_timeout or (60 if args.local else 360))
        print(f"Artifacts: {directory}")
        return 0
    except Exception as error:
        (directory / "summary.json").write_text(json.dumps({"status":"failed", "error":str(error)}, indent=2) + "\n")
        print(f"ERROR: {error}; Artifacts: {directory}", file=sys.stderr)
        return 1
    finally:
        if broker_process is not None:
            broker_process.terminate(); broker_process.wait(timeout=5)


if __name__ == "__main__": sys.exit(main())
