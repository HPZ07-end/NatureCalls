#!/usr/bin/env python3
"""Check repository portability without starting Unity or requiring a license."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import sys
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]


def check(require_generated=False):
    errors = []
    files = subprocess.check_output(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=ROOT
    ).decode().split("\0")
    files = sorted(set(name for name in files if name))
    folded = {}
    guids = {}
    for name in files:
        path = ROOT / name
        if not path.is_file():
            errors.append(f"Tracked file missing from disk: {name}")
            continue
        # Check directory prefixes too (Assets/UI vs Assets/ui on case-sensitive CI).
        for prefix in [Path(name), *Path(name).parents]:
            text = prefix.as_posix()
            old = folded.setdefault(text.casefold(), text)
            if old != text:
                errors.append(f"Case collision: {old} / {text}")
        if path.suffix in (".json", ".asmdef"):
            try:
                json.loads(path.read_text(encoding="utf-8"))
            except (ValueError, UnicodeError) as error:
                errors.append(f"{name}: {error}")
        if path.suffix == ".md":
            # Repository Markdown paths only; external URLs are not network-probed in CI.
            markdown = re.sub(r"```.*?```", "", path.read_text(encoding="utf-8"), flags=re.DOTALL)
            for link in re.findall(r"\[[^\]]*\]\(([^\s)]+)\)", markdown):
                target = urlsplit(link.strip("<>"))
                if target.scheme or target.netloc or not target.path:
                    continue
                destination = path.parent / unquote(target.path)
                if not destination.exists():
                    errors.append(f"Broken Markdown path in {name}: {link}")
        if name.startswith("Assets/") and path.suffix == ".meta":
            asset = Path(str(path)[:-5])
            if not asset.exists():
                errors.append(f"Orphan metadata: {name}")
            match = re.search(r"^guid: ([a-f0-9]{32})$", path.read_text(), re.MULTILINE)
            if not match:
                errors.append(f"Invalid metadata GUID: {name}")
            elif match[1] in guids:
                errors.append(f"Duplicate GUID: {name} / {guids[match[1]]}")
            else:
                guids[match[1]] = name
        if name.split('/')[0].lower() in {"library", "temp", "logs", "obj", "build", "builds", "usersettings", "artifacts"}:
            errors.append(f"Generated file tracked: {name}")
    for path in (ROOT / "Assets").rglob("*"):
        relative = path.relative_to(ROOT)
        if any(part.startswith(".") for part in relative.parts) or path.suffix == ".meta":
            continue
        if not Path(str(path) + ".meta").is_file():
            errors.append(f"Missing metadata (import in Unity first): {relative}")
    for name in ["Library/probe", "Temp/probe", "Logs/probe", "Build/probe", "Builds/probe", "UserSettings/probe", "Artifacts/probe"]:
        if subprocess.run(["git", "check-ignore", "-q", "--no-index", name], cwd=ROOT).returncode != 0:
            errors.append(f"Generated path is not ignored: {name}")
    for name in files:
        if name.startswith(("Assets/", "Packages/", "ProjectSettings/")):
            if subprocess.run(["git", "check-ignore", "-q", "--no-index", name], cwd=ROOT).returncode == 0:
                errors.append(f"Source path is ignored: {name}")
    manifest = ROOT / "Packages/manifest.json"
    if not manifest.exists():
        errors.append("Missing Packages/manifest.json")
    else:
        try:
            dependencies = json.loads(manifest.read_text())["dependencies"]
            if "com.unity.ide.vscode" in dependencies:
                errors.append("Deprecated com.unity.ide.vscode package is not allowed")
            if "com.unity.ide.visualstudio" not in dependencies or "com.unity.test-framework" not in dependencies:
                errors.append("Missing Visual Studio Editor or Test Framework dependency")
        except (ValueError, KeyError):
            errors.append("Invalid package manifest")
    if require_generated:
        for name in ["Packages/packages-lock.json", "ProjectSettings/EditorSettings.asset",
                     "ProjectSettings/VersionControlSettings.asset", "Assets/Scenes/Smoke.unity"]:
            if not (ROOT / name).is_file():
                errors.append(f"Unity setup has not generated {name}")
    if errors:
        print("\n".join(sorted(set(errors))), file=sys.stderr)
        return 1
    print(f"Repository checks passed ({len(files)} files; {len(guids)} asset GUIDs).")
    return 0


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--require-generated", action="store_true")
    sys.exit(check(parser.parse_args().require_generated))
