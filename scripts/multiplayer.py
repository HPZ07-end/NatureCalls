#!/usr/bin/env python3
"""Run two real development Players against localhost NGO; does not validate Relay."""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import socket
import subprocess
import sys
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("player", type=Path, help="macOS .app or Windows development .exe")
    args = parser.parse_args()
    player = args.player.resolve()
    if player.suffix == ".app":
        player = player / "Contents/MacOS/NatureCalls"
    if not player.is_file():
        parser.error("Built Player executable not found.")
    directory = ROOT / "Artifacts" / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:6] + "-multiplayer")
    directory.mkdir(parents=True)
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as reservation:
        reservation.bind(("127.0.0.1", 0))
        port = reservation.getsockname()[1]
    children = []
    streams = []
    print("Local NGO test logs: " + str(directory), flush=True)
    try:
        for role in ("host", "client"):
            stream = (directory / (role + "-process.log")).open("w")
            streams.append(stream)
            command = [str(player), "-batchmode", "-screen-width", "1100", "-screen-height", "760",
                       "-m1-probe", "-m1-role", role, "-m1-port", str(port),
                       "-m1-report", str(directory / (role + ".json")), "-logFile", str(directory / (role + "-player.log"))]
            children.append(subprocess.Popen(command, cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT))
            if role == "host":
                time.sleep(2)
        deadline = time.monotonic() + 90
        for child in children:
            child.wait(timeout=max(1, deadline - time.monotonic()))
        reports = {}
        for role, child in zip(("host", "client"), children):
            report = json.loads((directory / (role + ".json")).read_text())
            reports[role] = report
            if child.returncode != 0 or report.get("status") != "passed":
                raise RuntimeError(f"{role} failed: {report}")
        (directory / "summary.json").write_text(json.dumps({"status": "passed", "transport": "localhost UnityTransport (not Relay)", "reports": reports}, indent=2) + "\n")
        print(json.dumps(reports, indent=2))
        return 0
    except (OSError, ValueError, RuntimeError, subprocess.TimeoutExpired) as error:
        (directory / "summary.json").write_text(json.dumps({"status": "failed", "error": str(error)}, indent=2) + "\n")
        print("ERROR: " + str(error), file=sys.stderr)
        return 1
    finally:
        # Only processes created by this invocation are stopped; never an Editor or unrelated Player.
        for child in children:
            if child.poll() is None:
                child.terminate()
                try:
                    child.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    child.kill()
                    child.wait()
        for stream in streams:
            stream.close()


if __name__ == "__main__":
    sys.exit(main())
