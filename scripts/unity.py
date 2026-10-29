#!/usr/bin/env python3
"""Unity CLI for macOS/Windows, using Python 3.10+ standard library only."""
import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def project_version():
    text = (ROOT / "ProjectSettings/ProjectVersion.txt").read_text()
    return re.search(r"^m_EditorVersion: (\S+)", text, re.MULTILINE).group(1)


def editor_path():
    version = project_version()
    custom = os.environ.get("UNITY_EDITOR")
    if custom:
        candidates = [Path(custom).expanduser()]
    elif sys.platform == "darwin":
        candidates = [Path(f"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity")]
    elif sys.platform == "win32":
        candidates = [Path(os.environ.get("ProgramFiles", "C:/Program Files")) /
                      f"Unity/Hub/Editor/{version}/Editor/Unity.exe"]
    else:
        candidates = [Path.home() / f"Unity/Hub/Editor/{version}/Editor/Unity"]
    for candidate in candidates:
        if candidate.is_file():
            return candidate.resolve()
    raise RuntimeError(f"Unity {version} not found. Install via Hub, or set UNITY_EDITOR to the executable.")


def verify_editor(editor):
    result = subprocess.run([str(editor), "-version"], capture_output=True, text=True, timeout=60)
    versions = re.findall(r"\b\d+\.\d+\.\d+[abfp]\d+\b", result.stdout + result.stderr)
    if result.returncode != 0 or project_version() not in versions:
        raise RuntimeError(f"Editor version mismatch or version probe failed: {result.stdout} {result.stderr}")
    return project_version()


@contextmanager
def project_lock():
    # Never remove Unity's own lock, including apparently stale locks.
    if (ROOT / "Temp/UnityLockfile").exists():
        raise RuntimeError("UnityLockfile exists. Save and close this project's Unity GUI first; do not delete its lock automatically.")
    lock = ROOT / "Artifacts/unity.lock"
    lock.parent.mkdir(exist_ok=True)
    try:
        descriptor = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError:
        raise RuntimeError("Another automation run holds Artifacts/unity.lock. Check its PID before manual recovery.") from None
    try:
        with os.fdopen(descriptor, "w") as stream:
            stream.write(str(os.getpid()))
        yield
    finally:
        lock.unlink(missing_ok=True)


def check_results(path):
    root = ET.parse(path).getroot()
    cases = list(root.iter("test-case"))
    if not cases or root.get("result") != "Passed" or any(case.get("result") != "Passed" for case in cases):
        raise RuntimeError("Tests failed, skipped, or no tests ran; inspect results.xml.")
    return {"total": len(cases), "passed": len(cases)}


def run(action, timeout):
    editor = editor_path()
    with project_lock():
        version = verify_editor(editor)
        stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:6]
        directory = ROOT / "Artifacts" / f"{stamp}-{action}"
        directory.mkdir(parents=True)
        build = ROOT / "Builds/macOS" / stamp / "NatureCalls.app"
        command = [str(editor), "-batchmode", "-projectPath", str(ROOT), "-logFile", str(directory / "editor.log")]
        if action in ("setup", "validate", "playground", "camera"):
            command += ["-nographics", "-quit", "-executeMethod", "NatureCalls.Editor.Automation." +
                        {"setup": "Setup", "playground": "Playground", "camera": "Camera", "validate": "Validate"}[action]]
        elif action in ("test-editmode", "test-playmode"):
            if action == "test-editmode":
                command += ["-nographics"]
            # Test Framework exits itself; -quit can exit before tests finish.
            command += ["-runTests", "-testPlatform", "EditMode" if action == "test-editmode" else "PlayMode",
                        "-testResults", str(directory / "results.xml")]
        elif action == "build-macos":
            command += ["-quit", "-buildTarget", "osxuniversal", "-executeMethod", "NatureCalls.Editor.Automation.BuildMac"]
        environment = dict(os.environ, NATURECALLS_RUN_PATH=str(directory), NATURECALLS_BUILD_PATH=str(build))
        summary = {"action": action, "editor": str(editor), "version": version,
                   "command": command, "status": "failed"}
        print(f"Logs and results: {directory}", flush=True)
        try:
            with (directory / "process.log").open("w") as output:
                result = subprocess.run(command, env=environment, cwd=ROOT, stdout=output,
                                        stderr=subprocess.STDOUT, timeout=timeout)
            summary["exit_code"] = result.returncode
            if result.returncode:
                raise RuntimeError(f"Unity exited {result.returncode}; inspect editor.log and process.log.")
            if action.startswith("test-"):
                summary["tests"] = check_results(directory / "results.xml")
            else:
                receipt = directory / ("build.ok" if action == "build-macos" else "validate.ok")
                if not receipt.exists() or receipt.read_text().strip() != version:
                    raise RuntimeError("Unity did not produce the expected completion receipt.")
            if action == "build-macos":
                if not (build / "Contents/Info.plist").is_file() or not (build / "Contents/MacOS").is_dir():
                    raise RuntimeError("Build report succeeded but app bundle is incomplete.")
                summary["build"] = str(build)
            summary["status"] = "passed"
        except Exception as error:
            summary["error"] = str(error)
            raise
        finally:
            (directory / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
        print(json.dumps(summary, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["detect", "setup", "validate", "playground", "camera", "test-editmode", "test-playmode", "build-macos"])
    parser.add_argument("--timeout", type=int, default=1800, help="Editor timeout in seconds (default 1800)")
    arguments = parser.parse_args()
    try:
        if arguments.action == "detect":
            editor = editor_path()
            print(json.dumps({"editor": str(editor), "version": verify_editor(editor)}, indent=2))
        else:
            run(arguments.action, arguments.timeout)
    except (RuntimeError, OSError, ValueError, ET.ParseError, subprocess.TimeoutExpired) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
