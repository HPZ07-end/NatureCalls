"""Reject incomplete records, hidden Glossary edits, and repeated baselines."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("semantic", Path(__file__).resolve().parents[1] / "semantic.py")
semantic = importlib.util.module_from_spec(spec)
spec.loader.exec_module(semantic)

BASELINE = "Semantic-Diff: baseline\n\nReason:\n- Initial tree.\n\nHuman Approval:\n- Human governance request, 2026-10-19.\n"
CHANGED = """Semantic-Diff: changed

Added:
- none

Changed:
- NatureCalls/Gameplay/Player/Urgency

Removed:
- none

Reason:
- Approved rule change.

Human Approval:
- https://example.com/review/1
"""


class SemanticTests(unittest.TestCase):
    def test_unchanged(self):
        self.assertEqual(semantic.validate("Semantic-Diff: none", "same", "same"), "none")

    def test_none_cannot_hide_edits_or_addition(self):
        for before in (None, "old"):
            with self.subTest(before=before), self.assertRaises(ValueError):
                semantic.validate("Semantic-Diff: none", before, "new")

    def test_cannot_delete_authority(self):
        with self.assertRaises(ValueError):
            semantic.validate(CHANGED, "old", None)

    def test_missing_invalid_duplicate_marker(self):
        for body in ("", "Semantic-Diff: maybe", "Semantic-Diff: none\nSemantic-Diff: changed"):
            with self.subTest(body=body), self.assertRaises(ValueError):
                semantic.validate(body, "same", "same")

    def test_first_baseline(self):
        self.assertEqual(semantic.validate(BASELINE, None, "new"), "baseline")

    def test_baseline_cannot_be_reused(self):
        for before, established, target in (("old", False, "dev"), (None, True, "dev"), (None, False, "main")):
            with self.subTest(target=target), self.assertRaises(ValueError):
                semantic.validate(BASELINE, before, "new", established=established, target=target)

    def test_changed_with_and_without_glossary_edit(self):
        for before in ("new", "old"):
            self.assertEqual(semantic.validate(CHANGED, before, "new"), "changed")

    def test_incomplete_record(self):
        for label in ("Added", "Changed", "Removed", "Reason", "Human Approval"):
            with self.subTest(label=label), self.assertRaises(ValueError):
                semantic.validate(CHANGED.replace(label + ":", "Missing:"), "old", "new")

    def test_placeholders_and_bad_paths(self):
        for body in (CHANGED.replace("https://example.com/review/1", "pending"),
                     CHANGED.replace("Approved rule change.", "<brief explanation>"),
                     CHANGED.replace("NatureCalls/Gameplay/Player/Urgency", "Player.Urgency"),
                     CHANGED.replace("NatureCalls/Gameplay/Player/Urgency", "none")):
            with self.subTest(body=body), self.assertRaises(ValueError):
                semantic.validate(body, "old", "new")

    def test_duplicate_section(self):
        with self.assertRaises(ValueError):
            semantic.validate(CHANGED + "\nReason:\n- Another reason.\n", "old", "new")


if __name__ == "__main__":
    unittest.main()
