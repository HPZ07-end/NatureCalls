#!/usr/bin/env python3
"""Check PR semantic records and explicitly acknowledge public Glossary revisions."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT).decode("utf-8").strip()


def glossary(revision):
    if not git("ls-tree", revision, "--", "GLOSSARY.md"):
        return None
    return subprocess.check_output(["git", "show", f"{revision}:GLOSSARY.md"], cwd=ROOT).decode("utf-8")


def validate(body, before, after, *, established=False, target="dev"):
    declarations = re.findall(r"^Semantic-Diff:\s*([^\r\n]+)$", body, re.MULTILINE)
    if len(declarations) != 1 or declarations[0].strip() not in {"none", "changed", "baseline"}:
        raise ValueError("Require exactly one Semantic-Diff: none|changed|baseline declaration.")
    mode = declarations[0].strip()
    if after is None:
        raise ValueError("The authoritative GLOSSARY.md must exist at PR head.")
    if mode == "none":
        if before != after:
            raise ValueError("GLOSSARY.md changed but Semantic-Diff is none.")
        return mode
    if mode == "baseline" and (before is not None or established or target != "dev"):
        raise ValueError("baseline is only the first Glossary introduction into dev.")
    labels = ["Added", "Changed", "Removed", "Reason", "Human Approval"]
    fields = {}
    for label in labels:
        matches = list(re.finditer(r"^" + re.escape(label) + r":\s*$", body, re.MULTILINE))
        if len(matches) > 1:
            raise ValueError(f"Duplicate {label} section.")
        if not matches:
            continue
        tail = body[matches[0].end():]
        items = []
        for line in tail.splitlines():
            if not line.strip():
                continue
            if not line.startswith("- "):
                break
            item = line[2:].strip()
            if not item or re.search(r"<[^>]*>|\b(TODO|TBD)\b", item, re.IGNORECASE):
                raise ValueError(f"Unfilled {label} item.")
            items.append(item)
        if not items:
            raise ValueError(f"{label} needs nonempty '- ' entries.")
        fields[label] = items
    required = ["Reason", "Human Approval"] if mode == "baseline" else labels
    if any(label not in fields for label in required):
        raise ValueError("Missing sections: " + ", ".join(label for label in required if label not in fields))
    for label in ("Reason", "Human Approval"):
        if any(item.lower() in {"none", "n/a", "pending"} for item in fields[label]):
            raise ValueError(f"{label} must provide a real explanation/reference.")
    if mode == "changed":
        paths = []
        for label in labels[:3]:
            entries = fields[label]
            if entries == ["none"]:
                continue
            for item in entries:
                if not re.fullmatch(r"NatureCalls(?:/[A-Za-z][A-Za-z0-9]*)*", item):
                    raise ValueError(f"Invalid canonical concept path in {label}: {item}")
                paths.append(item)
        if not paths:
            raise ValueError("changed needs at least one concept path.")
    return mode


def check(args):
    if args.event:
        pr = json.loads(Path(args.event).read_text(encoding="utf-8"))["pull_request"]
        base, head, body, target = pr["base"]["sha"], pr["head"]["sha"], pr.get("body") or "", pr["base"]["ref"]
    else:
        if not args.body_file:
            raise ValueError("--body-file is required without --event.")
        base, head, body, target = args.base, args.head, Path(args.body_file).read_text(encoding="utf-8"), args.target
    # Resolve supplied revisions before composing object names; no shell evaluation.
    base, head = git("rev-parse", "--verify", base + "^{commit}"), git("rev-parse", "--verify", head + "^{commit}")
    established = bool(git("log", "-1", "--format=%H", base, "--", "GLOSSARY.md"))
    mode = validate(body, glossary(base), glossary(head), established=established, target=target)
    print(f"Semantic Diff passed: {mode}; base={base}; head={head}; target={target}")
    print("Format checks do not establish human approval or semantic correctness.")


def sync(args):
    subprocess.run(["git", "fetch", "origin", "--prune"], cwd=ROOT, check=True)
    revision = git("rev-parse", "--verify", f"refs/remotes/origin/{args.branch}^{{commit}}")
    task = git("rev-parse", "HEAD")
    path = Path(git("rev-parse", "--git-path", "semantic-sync.json"))
    if not path.is_absolute():
        path = ROOT / path
    try:
        records = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(records, dict):
            records = {}
    except (FileNotFoundError, ValueError):
        records = {}
    print(f"Task HEAD: {task}\norigin/{args.branch}: {revision}")
    if args.ack:
        if args.ack != revision:
            raise ValueError("Remote changed or SHA is incomplete. Read current Glossary before acknowledging.")
        records[args.branch] = revision
        temporary = path.with_suffix(".tmp")
        temporary.write_text(json.dumps(records, indent=2) + "\n", encoding="utf-8")
        temporary.replace(path)
        print("Recorded explicit acknowledgement: " + revision)
        return
    previous = records.get(args.branch)
    valid = isinstance(previous, str) and bool(re.fullmatch(r"[0-9a-f]{40,64}", previous))
    if valid:
        valid = subprocess.run(["git", "merge-base", "--is-ancestor", previous, revision], cwd=ROOT,
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0
    if valid:
        print(git("diff", previous, revision, "--", "GLOSSARY.md") or "No Glossary text changes since acknowledgement.")
    else:
        print("No reliable acknowledgement. Read the full public Glossary:")
        print(glossary(revision) or "Public baseline has no Glossary yet; read the proposed baseline in the task branch.")
    print("Review affected semantics and implementation, then explicitly --ack " + revision)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    checker = commands.add_parser("check")
    checker.add_argument("--event", help="GitHub pull_request event JSON path")
    checker.add_argument("--base", default="origin/dev")
    checker.add_argument("--head", default="HEAD")
    checker.add_argument("--target", choices=["dev", "main"], default="dev")
    checker.add_argument("--body-file")
    synchronizer = commands.add_parser("sync")
    synchronizer.add_argument("--branch", choices=["dev", "main"], default="dev")
    synchronizer.add_argument("--ack", help="Full SHA explicitly read and reviewed by the caller")
    args = parser.parse_args()
    try:
        (check if args.command == "check" else sync)(args)
    except (ValueError, OSError, KeyError, subprocess.CalledProcessError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
