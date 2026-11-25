#!/usr/bin/env python3
"""Local broker/dynamic-server lifecycle probe; never contacts the course server.

This validates the broker's real HTTP path and a real dedicated Player process.
Interactive client join and LAN broadcast remain separate manual checks because a
headless Player has no UI action that creates a broker room.
"""
import argparse
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import urllib.request
import urllib.error

ROOT = Path(__file__).resolve().parents[1]


def request(url, method="GET", data=None):
    body = None if data is None else json.dumps(data).encode()
    req = urllib.request.Request(url, method=method, data=body, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=5) as response:
        return response.status, json.loads(response.read())


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("server", type=Path, help="built NatureCalls executable or .app")
    parser.add_argument("--port", type=int, default=18100)
    args = parser.parse_args(argv)
    server = args.server.resolve()
    if server.suffix == ".app": server = server / "Contents/MacOS/NatureCalls"
    if not server.is_file(): parser.error(f"server executable not found: {server}")
    with tempfile.TemporaryDirectory(prefix="naturecalls-room-e2e-") as temp:
        root = Path(temp)
        log_dir, state = root / "logs", root / "rooms.json"
        log_dir.mkdir()
        launcher = root / "launcher.py"
        launcher.write_text(f'''#!/usr/bin/env python3
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
''')
        launcher.chmod(0o755)
        env = dict(os.environ, ROOM_E2E_ROOT=str(root), ROOM_E2E_SERVER=str(server))
        broker = subprocess.Popen([sys.executable, str(ROOT / "scripts/room_broker.py"), "--bind", "127.0.0.1",
                                   "--port", str(args.port), "--state", str(state), "--launcher", str(launcher),
                                   "--log-dir", str(log_dir)], env=env)
        try:
            deadline = time.monotonic() + 10
            while True:
                try:
                    _, health = request(f"http://127.0.0.1:{args.port}/health")
                    if health["ok"]: break
                except OSError:
                    if time.monotonic() > deadline: raise
                    time.sleep(.1)
            _, room = request(f"http://127.0.0.1:{args.port}/rooms", "POST", {})
            code, game_port = room["code"], room["port"]
            _, found = request(f"http://127.0.0.1:{args.port}/rooms/{code}")
            assert found["port"] == game_port
            process_deadline = time.monotonic() + 35
            while time.monotonic() < process_deadline:
                if not (root / f"{game_port}.pid").exists(): break
                try: os.kill(int((root / f"{game_port}.pid").read_text()), 0)
                except (ProcessLookupError, ValueError): break
                time.sleep(.5)
            else: raise RuntimeError("dynamic room did not exit within 35 seconds")
            # The live broker's ten-second reaper observes the fake systemctl state.
            time.sleep(11)
            try: request(f"http://127.0.0.1:{args.port}/rooms/{code}")
            except urllib.error.HTTPError as error:
                assert error.code == 404
            else: raise RuntimeError("closed room still returned from broker")
            print(json.dumps({"status":"passed", "code":code, "port":game_port, "broker":"local", "clients":"not exercised"}))
            return 0
        finally:
            broker.terminate(); broker.wait(timeout=5)


if __name__ == "__main__": sys.exit(main())
