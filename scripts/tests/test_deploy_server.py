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

    def test_already_current_matches_the_commit_prefix_only(self):
        self.assertTrue(deploy.already_current("releases/6537a3c-20261126T090000Z-abc", "6537a3c"))
        self.assertTrue(deploy.already_current("/opt/naturecalls/releases/6537a3c-x\n", "6537a3c"))
        self.assertFalse(deploy.already_current("releases/2bca57f-20261126T012947Z-8d028c", "6537a3c"))
        self.assertFalse(deploy.already_current("releases/6537a3c-dirty-x", "6537a3c-dirty"))
        self.assertFalse(deploy.already_current("", "6537a3c"))

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
        self.assertIn("install -m 644 /tmp/naturecalls-room@.service", script)
        self.assertIn("install -m 644 /tmp/naturecalls-broker.service", script)
        self.assertIn("BROKER_FAILED: health check", script)
        self.assertIn("systemctl restart naturecalls-broker.service || true", script)
        self.assertIn("seq 1 10", script)

    def test_dynamic_service_logs_to_journal_and_is_not_enabled(self):
        service = (Path(__file__).parents[1] / "naturecalls-room@.service").read_text()
        self.assertIn("-idle-exit 300 -logFile -", service)
        self.assertIn("User=ubuntu", service)
        self.assertIn("NoNewPrivileges=true", service)
        self.assertIn("StandardOutput=journal", service)
        self.assertNotIn("[Install]", service)


if __name__ == "__main__":
    unittest.main()
