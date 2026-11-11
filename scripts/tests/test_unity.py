"""Regression tests for failure detection in the CLI; these are not Unity tests."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import subprocess

spec = importlib.util.spec_from_file_location("unity", Path(__file__).resolve().parents[1] / "unity.py")
unity = importlib.util.module_from_spec(spec)
spec.loader.exec_module(unity)


class ResultsTests(unittest.TestCase):
    def check_xml(self, xml):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.xml"
            path.write_text(xml)
            return unity.check_results(path)

    def test_accepts_real_passed_case(self):
        self.assertEqual(self.check_xml('<test-run result="Passed"><test-case result="Passed"/></test-run>')["passed"], 1)

    def test_accepts_ignored_but_reports_it_separately(self):
        result = self.check_xml('<test-run result="Skipped:Ignored"><test-suite result="Skipped" label="Ignored">'
                                '<test-case result="Passed"/><test-case result="Skipped" label="Ignored"/>'
                                '</test-suite></test-run>')
        self.assertEqual(result, {"total": 2, "passed": 1, "ignored": 1})

    def test_rejects_all_ignored(self):
        with self.assertRaises(RuntimeError):
            self.check_xml('<test-run result="Skipped:Ignored"><test-case result="Skipped" label="Ignored"/></test-run>')

    def test_ignored_cannot_hide_failed_suite_or_case(self):
        for child in ['<test-case result="Failed"/>', '<test-suite result="Failed"/>']:
            with self.subTest(child=child), self.assertRaises(RuntimeError):
                self.check_xml('<test-run result="Skipped:Ignored"><test-case result="Passed"/>'
                               '<test-case result="Skipped" label="Ignored"/>' + child + '</test-run>')

    def test_rejects_empty_run(self):
        with self.assertRaises(RuntimeError):
            self.check_xml('<test-run result="Passed"/>')

    def test_rejects_failed_or_skipped_case(self):
        for result in ["Failed", "Skipped", "Inconclusive"]:
            with self.subTest(result=result), self.assertRaises(RuntimeError):
                self.check_xml(f'<test-run result="Passed"><test-case result="{result}"/></test-run>')

    def test_rejects_failed_suite_even_with_passed_cases(self):
        with self.assertRaises(RuntimeError):
            self.check_xml('<test-run result="Failed"><test-case result="Passed"/></test-run>')


class LockTests(unittest.TestCase):
    def setUp(self):
        self.previous = unity.ROOT
        self.directory = tempfile.TemporaryDirectory()
        unity.ROOT = Path(self.directory.name)

    def tearDown(self):
        unity.ROOT = self.previous
        self.directory.cleanup()

    def test_refuses_gui_lock_and_preserves_it(self):
        lock = unity.ROOT / "Temp/UnityLockfile"
        lock.parent.mkdir()
        lock.touch()
        with self.assertRaises(RuntimeError), unity.project_lock():
            self.fail("Unity GUI lock was ignored")
        self.assertTrue(lock.exists())

    def test_refuses_concurrent_automation(self):
        with unity.project_lock():
            with self.assertRaises(RuntimeError), unity.project_lock():
                self.fail("Concurrent command acquired project")
        self.assertFalse((unity.ROOT / "Artifacts/unity.lock").exists())

    def test_releases_lock_on_error(self):
        with self.assertRaises(ValueError), unity.project_lock():
            raise ValueError("failed launch")
        self.assertFalse((unity.ROOT / "Artifacts/unity.lock").exists())


class TargetTests(unittest.TestCase):
    def test_native_targets(self):
        for platform, target in [("darwin", "osxuniversal"), ("win32", "Win64"), ("linux", "Linux64")]:
            self.assertEqual(unity.player_target(platform), target)

    def test_restore_requires_receipt_not_just_zero_exit(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            summary = {}
            with patch.object(unity, "ROOT", root), \
                    patch.object(unity.subprocess, "run", return_value=subprocess.CompletedProcess([], 0)):
                with self.assertRaisesRegex(RuntimeError, "restoration failed"):
                    unity.restore_player(Path("Unity"), root / "restore", {}, 10, summary)
            self.assertEqual(summary["restore"]["status"], "failed")

    def test_restore_preserves_unity_lock(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "Temp").mkdir()
            lock = root / "Temp/UnityLockfile"
            lock.touch()
            with patch.object(unity, "ROOT", root), patch.object(unity.subprocess, "run") as launch:
                with self.assertRaisesRegex(RuntimeError, "UnityLockfile"):
                    unity.restore_player(Path("Unity"), root / "restore", {}, 10, {})
                launch.assert_not_called()
            self.assertTrue(lock.exists())

    def test_server_failure_still_restores_player(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "ProjectSettings").mkdir()
            (root / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.3.26f1")
            calls = []
            def launch(command, **kwargs):
                calls.append(command)
                if command[command.index("-executeMethod") + 1].endswith("ValidatePlayer"):
                    receipt = Path(kwargs["env"]["NATURECALLS_RUN_PATH"]) / "validate.ok"
                    receipt.write_text("6000.3.26f1\n")
                    return subprocess.CompletedProcess(command, 0)
                return subprocess.CompletedProcess(command, 1)
            with patch.object(unity, "ROOT", root), patch.object(unity, "editor_path", return_value=Path("Unity")), \
                    patch.object(unity, "verify_editor", return_value="6000.3.26f1"), \
                    patch.object(unity.subprocess, "run", side_effect=launch):
                with self.assertRaisesRegex(RuntimeError, "Unity exited 1"):
                    unity.run("build-server", 10)
            self.assertEqual(len(calls), 2)
            self.assertEqual(calls[1][calls[1].index("-standaloneBuildSubtarget") + 1], "Player")
            self.assertEqual(calls[1][calls[1].index("-buildTarget") + 1], unity.player_target())


class GraphicsModeTests(unittest.TestCase):
    def test_windows_uses_graphics_by_default(self):
        self.assertFalse(unity.use_nographics("win32", {}))

    def test_non_windows_uses_nographics_by_default(self):
        self.assertTrue(unity.use_nographics("darwin", {}))

    def test_environment_can_enable_nographics_on_windows(self):
        self.assertTrue(unity.use_nographics("win32", {"UNITY_NOGRAPHICS": "true"}))

    def test_environment_can_disable_nographics_on_non_windows(self):
        self.assertFalse(unity.use_nographics("linux", {"UNITY_NOGRAPHICS": "0"}))


if __name__ == "__main__":
    unittest.main()
