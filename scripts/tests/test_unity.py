"""Regression tests for failure detection in the CLI; these are not Unity tests."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

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
