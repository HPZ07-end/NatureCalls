import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace

spec = importlib.util.spec_from_file_location("room_broker", Path(__file__).parents[1] / "room_broker.py")
broker_module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(broker_module)


class FakeSystemctl:
    def __init__(self):
        self.calls = []
        self.active = set()

    def __call__(self, command, **kwargs):
        self.calls.append(command)
        action = command[1]
        if action == "start":
            self.active.add(int(command[2].split("@")[1]))
            return SimpleNamespace(returncode=0, stdout="")
        if action == "stop":
            self.active.discard(int(command[2].split("@")[1]))
            return SimpleNamespace(returncode=0, stdout="")
        if action == "is-active":
            port = int(command[2].split("@")[1])
            return SimpleNamespace(returncode=0 if port in self.active else 3,
                                   stdout="active\n" if port in self.active else "inactive\n")
        if action == "list-units":
            output = "\n".join(f"naturecalls-room@{p}.service loaded active running" for p in self.active)
            return SimpleNamespace(returncode=0, stdout=output)
        return SimpleNamespace(returncode=0, stdout="")


class BrokerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.systemctl = FakeSystemctl()
        self.broker = broker_module.RoomBroker(
            Path(self.temp.name) / "rooms.json", max_rooms=2,
            command=self.systemctl, is_listening=lambda port: True,
            log_ready=lambda port: True, start_timeout=0.1)

    def tearDown(self):
        self.temp.cleanup()

    def test_allocates_unique_code_and_port(self):
        first, second = self.broker.create("127.0.0.1"), self.broker.create("127.0.0.1")
        self.assertNotEqual(first["code"], second["code"])
        self.assertNotEqual(first["port"], second["port"])
        self.assertEqual(first["protocol"], 1)

    def test_limit_and_unknown(self):
        self.broker.create("a")
        self.broker.create("b")
        with self.assertRaisesRegex(broker_module.BrokerError, "已满"):
            self.broker.create("c")
        with self.assertRaisesRegex(broker_module.BrokerError, "不存在"):
            self.broker.lookup(999)

    def test_rate_limit(self):
        self.broker.max_rooms = 99
        for _ in range(5):
            self.broker.create("same")
        with self.assertRaisesRegex(broker_module.BrokerError, "每分钟"):
            self.broker.create("same")

    def test_cleanup_removes_stopped(self):
        room = self.broker.create("x")
        self.systemctl.active.clear()
        self.assertEqual(self.broker.cleanup(), [room["code"]])
        with self.assertRaises(broker_module.BrokerError):
            self.broker.lookup(room["code"])

    def test_restore_reads_only_active_units(self):
        self.broker.rooms = {123: 8001, 124: 8002}
        self.broker.save()
        self.systemctl.active = {8002}
        restored = broker_module.RoomBroker(self.broker.state_path, command=self.systemctl,
                                            is_listening=lambda p: True, log_ready=lambda p: True)
        restored.restore()
        self.assertEqual(restored.rooms, {124: 8002})

    def test_exhaustion(self):
        self.broker.max_rooms = 200
        self.broker.rooms = {100 + i: 8001 + i for i in range(99)}
        with self.assertRaisesRegex(broker_module.BrokerError, "耗尽"):
            self.broker.create("x")


if __name__ == "__main__":
    unittest.main()
