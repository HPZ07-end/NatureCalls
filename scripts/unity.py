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
    def ignored(node):
        return node.get("result") == "Skipped" and node.get("label") == "Ignored"
    passed = sum(case.get("result") == "Passed" for case in cases)
    # Unity propagates Ignored to suites and reports Passed or Skipped:Ignored at the root.
    nodes = [root, *root.iter("test-suite"), *cases]
    if not passed or any(node.get("result") != "Passed" and not ignored(node)
                         and not (node is root and node.get("result") == "Skipped:Ignored")
                         for node in nodes):
        raise RuntimeError("Tests failed, were inconclusive/unexpectedly skipped, or no tests passed; inspect results.xml.")
    return {"total": len(cases), "passed": passed, "ignored": sum(ignored(case) for case in cases)}


def player_target(platform=None):
    return {"darwin": "osxuniversal", "win32": "Win64", "linux": "Linux64"}[
        sys.platform if platform is None else platform]


def restore_player(editor, directory, environment, timeout, summary):
    """Use a fresh Editor process: batchmode cannot safely switch targets in executeMethod."""
    directory.mkdir()
    command = [str(editor), "-batchmode", "-quit", "-projectPath", str(ROOT),
               "-buildTarget", player_target(), "-standaloneBuildSubtarget", "Player",
               "-executeMethod", "NatureCalls.Editor.Automation.ValidatePlayer",
               "-logFile", str(directory / "editor.log")]
    if use_nographics():
        command += ["-nographics"]
    summary["restore"] = {"command": command, "status": "failed"}
    if (ROOT / "Temp/UnityLockfile").exists():
        raise RuntimeError("Server build left UnityLockfile; cannot safely restore Player. Inspect the build logs.")
    print(f"Restoring desktop Player: {directory}", flush=True)
    with (directory / "process.log").open("w") as output:
        result = subprocess.run(command, env=dict(environment, NATURECALLS_RUN_PATH=str(directory)),
                                cwd=ROOT, stdout=output, stderr=subprocess.STDOUT, timeout=timeout)
    summary["restore"]["exit_code"] = result.returncode
    receipt = directory / "validate.ok"
    if result.returncode or not receipt.exists() or receipt.read_text().strip() != project_version():
        raise RuntimeError("Desktop Player restoration failed; inspect restore/editor.log.")
    summary["restore"]["status"] = "passed"


def use_nographics(platform=None, environment=None):
    """Use headless graphics by default off Windows; allow an explicit override."""
    platform = sys.platform if platform is None else platform
    environment = os.environ if environment is None else environment
    override = environment.get("UNITY_NOGRAPHICS")
    if override is not None:
        return override.strip().lower() in {"1", "true", "yes", "on"}
    return platform != "win32"


def run(action, timeout, test_filter=None):
    editor = editor_path()
    with project_lock():
        version = verify_editor(editor)
        stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:6]
        directory = ROOT / "Artifacts" / f"{stamp}-{action}"
        directory.mkdir(parents=True)
        build = (ROOT / "Builds/LinuxServer" / stamp / "NatureCalls" if action == "build-server"
                 else ROOT / "Builds/Windows" / stamp / "NatureCalls.exe" if action == "build-windows"
                 else ROOT / "Builds/macOS" / stamp / "NatureCalls.app")
        command = [str(editor), "-batchmode", "-projectPath", str(ROOT), "-logFile", str(directory / "editor.log")]
        if action != "build-server":
            target = {"build-macos": "osxuniversal", "build-windows": "Win64"}.get(action, player_target())
            command += ["-buildTarget", target, "-standaloneBuildSubtarget", "Player"]
        if action in ("setup", "validate", "playground", "camera", "lobby-flow", "toilet-anchors", "spawn-points", "match-flow", "facilities"):
            if use_nographics():
                command += ["-nographics"]
            command += ["-quit", "-executeMethod", "NatureCalls.Editor.Automation." +
                        {"setup": "Setup", "playground": "Playground", "camera": "Camera",
                         "lobby-flow": "LobbyFlow", "toilet-anchors": "ToiletAnchors", "spawn-points": "SpawnPoints", "match-flow": "MatchFlow", "facilities": "Facilities", "validate": "Validate"}[action]]
        elif action in ("test-editmode", "test-playmode"):
            if action == "test-editmode" and use_nographics():
                command += ["-nographics"]
            # Test Framework exits itself; -quit can exit before tests finish.
            command += ["-runTests", "-testPlatform", "EditMode" if action == "test-editmode" else "PlayMode",
                        "-testResults", str(directory / "results.xml")]
        elif action == "build-macos":
            command += ["-quit", "-executeMethod", "NatureCalls.Editor.Automation.BuildMac"]
        elif action == "build-windows":
            command += ["-quit", "-executeMethod", "NatureCalls.Editor.Automation.BuildWindows"]
        elif action == "build-server":
            command += ["-quit", "-nographics", "-buildTarget", "Linux64", "-standaloneBuildSubtarget", "Server",
                        "-executeMethod", "NatureCalls.Editor.Automation.BuildServer"]
        if test_filter:
            if not action.startswith("test-"):
                raise ValueError("--test-filter requires a test action.")
            command += ["-testFilter", test_filter]
        environment = dict(os.environ, NATURECALLS_RUN_PATH=str(directory), NATURECALLS_BUILD_PATH=str(build))
        summary = {"action": action, "editor": str(editor), "version": version,
                   "command": command, "status": "failed"}
        print(f"Logs and results: {directory}", flush=True)
        try:
            try:
                with (directory / "process.log").open("w") as output:
                    result = subprocess.run(command, env=environment, cwd=ROOT, stdout=output,
                                            stderr=subprocess.STDOUT, timeout=timeout)
                summary["exit_code"] = result.returncode
            finally:
                if action == "build-server":
                    restore_player(editor, directory / "restore", environment, timeout, summary)
            summary["exit_code"] = result.returncode
            if result.returncode:
                raise RuntimeError(f"Unity exited {result.returncode}; inspect editor.log and process.log.")
            if action.startswith("test-"):
                summary["tests"] = check_results(directory / "results.xml")
            else:
                receipt = directory / ("build.ok" if action.startswith("build-") else "validate.ok")
                if not receipt.exists() or receipt.read_text().strip() != version:
                    raise RuntimeError("Unity did not produce the expected completion receipt.")
            if action == "build-macos":
                if not (build / "Contents/Info.plist").is_file() or not (build / "Contents/MacOS").is_dir():
                    raise RuntimeError("Build report succeeded but app bundle is incomplete.")
                summary["build"] = str(build)
            elif action in ("build-windows", "build-server"):
                if not build.is_file() or not build.with_name("NatureCalls_Data").is_dir():
                    raise RuntimeError("Build report succeeded but Player is incomplete.")
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
    parser.add_argument("action", choices=["detect", "setup", "validate", "playground", "camera", "lobby-flow", "toilet-anchors", "spawn-points", "match-flow", "facilities", "test-editmode", "test-playmode", "build-macos", "build-windows", "build-server"])
    parser.add_argument("--timeout", type=int, default=1800, help="Editor timeout in seconds (default 1800)")
    parser.add_argument("--test-filter", help="Explicit Unity test name/regex filter; recorded in summary.json")
    arguments = parser.parse_args()
    try:
        if arguments.action == "detect":
            editor = editor_path()
            print(json.dumps({"editor": str(editor), "version": verify_editor(editor)}, indent=2))
        else:
            run(arguments.action, arguments.timeout, arguments.test_filter)
    except (RuntimeError, OSError, ValueError, ET.ParseError, subprocess.TimeoutExpired) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
