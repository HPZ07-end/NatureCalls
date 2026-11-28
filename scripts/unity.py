#!/usr/bin/env python3
"""Unity CLI for macOS/Windows, using Python 3.10+ standard library only."""
import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import shutil
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


def unity_lock_holders(lockfile: Path) -> list[str]:
    """Processes that keep this project's Unity lock: any process with the file open, or a Unity Editor on this project."""
    holders = []
    opened = subprocess.run(["lsof", "-t", str(lockfile)], capture_output=True, text=True)
    holders += [f"pid {pid} has {lockfile.name} open" for pid in opened.stdout.split()]
    editors = subprocess.run(["pgrep", "-fl", "Unity.app/Contents/MacOS/Unity"], capture_output=True, text=True)
    for line in editors.stdout.splitlines():
        if str(ROOT) in line:
            holders.append("Unity Editor on this project: " + line)
    return holders


def can_check_lock_holders() -> bool:
    return sys.platform != "win32" and shutil.which("lsof") is not None and shutil.which("pgrep") is not None


def clear_stale_unity_lock():
    # A Unity lock is only stale when no Unity process for this project runs and nobody has the file open
    # (Unity leaves it behind when it aborts, e.g. on compile errors). Anything else stops the run.
    lockfile = ROOT / "Temp/UnityLockfile"
    if not lockfile.exists():
        return
    if not can_check_lock_holders():
        raise RuntimeError("UnityLockfile exists and holders cannot be checked on this platform. Close Unity and inspect it manually.")
    holders = unity_lock_holders(lockfile)
    if holders:
        raise RuntimeError("UnityLockfile is held (" + "; ".join(holders) + "). Save and close that Unity first.")
    record = ROOT / "Artifacts/stale-lock.txt"
    record.parent.mkdir(exist_ok=True)
    with record.open("a") as stream:
        stream.write(f"{datetime.now(timezone.utc).isoformat()} removed stale {lockfile} (mtime {datetime.fromtimestamp(lockfile.stat().st_mtime, timezone.utc).isoformat()}); no Unity process, no open handle\n")
    lockfile.unlink()
    print(f"Removed stale Unity lock (no process holds it); recorded in {record}", flush=True)


@contextmanager
def project_lock():
    clear_stale_unity_lock()
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
    clear_stale_unity_lock()
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


KEEP_BUILDS = 3


def prune_builds(directory, keep=KEEP_BUILDS):
    """Delete all but the newest `keep` build folders (named <UTC stamp>-<id>, so names sort by time). User 2026-11-28."""
    if not directory.is_dir():
        return []
    builds = sorted((item for item in directory.iterdir() if item.is_dir()), key=lambda item: item.name, reverse=True)
    removed = builds[keep:]
    for item in removed:
        shutil.rmtree(item)
    return removed


AUTO_DEPLOY_BRANCHES = ("dev", "main")


def auto_deploy_plan(branch, dirty, setting):
    """Whether a successful client build should also deploy the course server, and why (user 2026-11-26).

    Only clean builds of dev or main deploy, so the shared server always matches a merged commit; feature
    branches, Codex worktrees and dirty trees just build. NATURECALLS_AUTO_DEPLOY=0 turns it off."""
    if setting is not None and setting.strip().lower() in {"0", "false", "no", "off"}:
        return False, "automatic deploy is disabled (NATURECALLS_AUTO_DEPLOY=0)"
    if dirty:
        return False, "tracked files have uncommitted changes, so the server could not match a commit"
    if branch not in AUTO_DEPLOY_BRANCHES:
        return False, f"branch '{branch or 'detached'}' is not dev or main"
    return True, f"clean {branch} build"


def auto_deploy():
    """After a passed client build: deploy the same commit to the course server (skipped if it already runs it)."""
    def git(*args):
        return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True).stdout.strip()
    branch = git("rev-parse", "--abbrev-ref", "HEAD")
    dirty = bool(git("status", "--porcelain", "--untracked-files=no"))
    deploy, reason = auto_deploy_plan(branch if branch != "HEAD" else "", dirty, os.environ.get("NATURECALLS_AUTO_DEPLOY"))
    if not deploy:
        print(f"Course server not deployed: {reason}.", flush=True)
        return 0
    print(f"Deploying the course server to match this {reason} (pass --no-deploy to skip)...", flush=True)
    result = subprocess.run([sys.executable, str(ROOT / "scripts/deploy_server.py"), "--skip-if-current"], cwd=ROOT)
    if result.returncode:
        print("ERROR: the client build passed, but deploying the course server failed; see the output above.", file=sys.stderr)
    return result.returncode


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
        if action in ("setup", "validate", "playground", "camera", "lobby-flow", "dressing-room", "toilet-lobby", "server-lobby-controls", "toilet-anchors", "spawn-points", "match-flow", "facilities", "toileting", "map-integration", "map2-integration", "map2-occupancy-signs", "map2-stall-003-sign", "plumbing", "mess-art", "map3-integration"):
            if use_nographics():
                command += ["-nographics"]
            command += ["-quit", "-executeMethod", "NatureCalls.Editor.Automation." +
                        {"setup": "Setup", "playground": "Playground", "camera": "Camera",
                         "lobby-flow": "LobbyFlow", "dressing-room": "DressingRoom", "toilet-lobby": "ToiletLobby", "server-lobby-controls": "ServerLobbyControls", "toilet-anchors": "ToiletAnchors", "spawn-points": "SpawnPoints", "match-flow": "MatchFlow", "facilities": "Facilities", "toileting": "Toileting", "map-integration": "MapIntegration", "map2-integration": "Map2Integration", "map2-occupancy-signs": "Map2OccupancySigns", "map2-stall-003-sign": "Map2Stall003Sign", "plumbing": "Plumbing", "mess-art": "MessArt", "map3-integration": "Map3Integration", "validate": "Validate"}[action]]
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
        if summary["status"] == "passed" and action.startswith("build-"):
            removed = prune_builds(build.parent.parent)  # Builds/<platform>/<stamp>/<player>
            if removed:
                print(f"Removed {len(removed)} older build(s) from {build.parent.parent.relative_to(ROOT)}; kept the newest {KEEP_BUILDS}.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["detect", "setup", "validate", "playground", "camera", "lobby-flow", "dressing-room", "toilet-lobby", "server-lobby-controls", "toilet-anchors", "spawn-points", "match-flow", "facilities", "toileting", "map-integration", "map2-integration", "map2-occupancy-signs", "map2-stall-003-sign", "plumbing", "mess-art", "map3-integration", "test-editmode", "test-playmode", "build-macos", "build-windows", "build-server"])
    parser.add_argument("--timeout", type=int, default=1800, help="Editor timeout in seconds (default 1800)")
    parser.add_argument("--test-filter", help="Explicit Unity test name/regex filter; recorded in summary.json")
    parser.add_argument("--no-deploy", action="store_true",
                        help="build-macos/build-windows: do not deploy the course server afterwards")
    arguments = parser.parse_args()
    try:
        if arguments.action == "detect":
            editor = editor_path()
            print(json.dumps({"editor": str(editor), "version": verify_editor(editor)}, indent=2))
        else:
            run(arguments.action, arguments.timeout, arguments.test_filter)
            if arguments.action in ("build-macos", "build-windows") and not arguments.no_deploy:
                return 1 if auto_deploy() else 0
    except (RuntimeError, OSError, ValueError, ET.ParseError, subprocess.TimeoutExpired) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
