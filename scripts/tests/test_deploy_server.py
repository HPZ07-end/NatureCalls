"""Tests for the deploy script's pure parts; they do not contact the server or run Unity."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("deploy_server", Path(__file__).resolve().parents[1] / "deploy_server.py")
deploy = importlib.util.module_from_spec(spec)
spec.loader.exec_module(deploy)


class DeployTests(unittest.TestCase):
    def test_parse_build_needs_a_passed_status(self):
        passed = '{\n  "status": "passed",\n  "build": "/x/Builds/LinuxServer/20261111T010000Z-abc/NatureCalls"\n}'
        self.assertEqual(deploy.parse_build(passed), Path("/x/Builds/LinuxServer/20261111T010000Z-abc/NatureCalls"))
        self.assertIsNone(deploy.parse_build(passed.replace("passed", "failed")))
        self.assertIsNone(deploy.parse_build('{"status": "passed"}'))

    def test_release_name_joins_commit_and_build_id(self):
        build = Path("/x/Builds/LinuxServer/20261111T010000Z-abc/NatureCalls")
        self.assertEqual(deploy.release_name("c26b26b", build), "c26b26b-20261111T010000Z-abc")

    def test_remote_script_switches_atomically_verifies_and_rolls_back(self):
        script = deploy.remote_script("c26b26b-x", "/tmp/a.tar.gz", [8000, 8001], keep=3, service_changed=False)
        self.assertIn("for port in 8000 8001; do", script)
        self.assertIn('mv -T "$ROOT/current.next" "$ROOT/current"', script)
        self.assertIn("Server listening on UDP $port", script)
        self.assertIn("ROLLED_BACK", script)
        self.assertIn("tail -n +4", script, "Keeps the new release plus three older ones.")
        self.assertNotIn("install -m 644", script, "The service template is only installed when it changed.")
        self.assertIn("install -m 644", deploy.remote_script("r", "/tmp/a", [8000], 3, service_changed=True))


if __name__ == "__main__":
    unittest.main()
