#!/usr/bin/env python3
"""Small, dependency-free HTTP broker for on-demand NatureCalls rooms.

The broker owns only dynamic ports 8001..8099.  It persists the code-to-port
mapping so a restart can recover rooms that systemd still has running.
"""
from __future__ import annotations

import argparse
import json
import os
import random
import re
import subprocess
import threading
import time
from collections import defaultdict, deque
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Callable

PROTOCOL = 1
PORT_MIN, PORT_MAX, FIXED_PORT = 8001, 8099, 8000
CODE_MIN, CODE_MAX = 100, 999
MAX_BODY = 4096


class BrokerError(Exception):
    def __init__(self, status: int, message: str):
        super().__init__(message)
        self.status = status
        self.message = message


class RoomBroker:
    def __init__(self, state_path: Path, max_rooms: int = 12,
                 command: Callable[..., subprocess.CompletedProcess] | None = None,
                 is_listening: Callable[[int], bool] | None = None,
                 log_ready: Callable[[int], bool] | None = None,
                 start_timeout: float = 30.0, clock: Callable[[], float] = time.monotonic):
        self.state_path = Path(state_path)
        self.max_rooms = max(1, int(max_rooms))
        self.command = command or subprocess.run
        self.is_listening = is_listening or self._udp_listening
        self.log_ready = log_ready or self._journal_ready
        self.start_timeout = start_timeout
        self.clock = clock
        self.lock = threading.RLock()
        self.rooms: dict[int, int] = {}
        self.requests: defaultdict[str, deque[float]] = defaultdict(deque)
        self.load()

    @staticmethod
    def _valid_code(code):
        return isinstance(code, int) and CODE_MIN <= code <= CODE_MAX

    @staticmethod
    def _valid_port(port):
        return isinstance(port, int) and PORT_MIN <= port <= PORT_MAX

    def load(self):
        try:
            data = json.loads(self.state_path.read_text())
        except (FileNotFoundError, json.JSONDecodeError, OSError):
            return
        rooms = data.get("rooms", data) if isinstance(data, dict) else {}
        if isinstance(rooms, dict):
            for code, port in rooms.items():
                try:
                    code, port = int(code), int(port)
                except (TypeError, ValueError):
                    continue
                if self._valid_code(code) and self._valid_port(port):
                    self.rooms[code] = port

    def save(self):
        self.state_path.parent.mkdir(parents=True, exist_ok=True)
        temp = self.state_path.with_suffix(".tmp")
        temp.write_text(json.dumps({"protocol": PROTOCOL, "rooms": self.rooms}, sort_keys=True) + "\n")
        os.replace(temp, self.state_path)

    def restore(self):
        """Keep only dynamic units that systemd reports as active."""
        try:
            result = self.command(["systemctl", "list-units", "naturecalls-room@*.service",
                                   "--state=active", "--plain", "--no-legend"],
                                  check=True, capture_output=True, text=True)
            active = {int(p) for p in re.findall(r"naturecalls-room@(\d+)\.service", result.stdout)
                      if self._valid_port(int(p))}
        except (OSError, subprocess.CalledProcessError, ValueError):
            active = set()
        with self.lock:
            self.rooms = {code: port for code, port in self.rooms.items() if port in active}
            self.save()

    def _new_pair(self):
        used = set(self.rooms.values())
        ports = [p for p in range(PORT_MIN, PORT_MAX + 1) if p not in used]
        if not ports:
            raise BrokerError(503, "动态房间端口已耗尽，请稍后再试。")
        codes = [c for c in range(CODE_MIN, CODE_MAX + 1) if c not in self.rooms]
        if not codes:
            raise BrokerError(503, "房间号已耗尽，请稍后再试。")
        return random.choice(codes), random.choice(ports)

    def create(self, client_ip: str):
        with self.lock:
            now = self.clock()
            recent = self.requests[client_ip]
            while recent and now - recent[0] >= 60:
                recent.popleft()
            if len(recent) >= 5:
                raise BrokerError(429, "同一地址每分钟最多创建 5 个房间。")
            if len(self.rooms) >= self.max_rooms:
                raise BrokerError(503, "服务器房间已满，请稍后再试。")
            code, port = self._new_pair()
            try:
                self.command(["systemctl", "start", f"naturecalls-room@{port}"], check=True)
                deadline = self.clock() + self.start_timeout
                while self.clock() < deadline:
                    if self.is_listening(port) and self.log_ready(port):
                        self.rooms[code] = port
                        recent.append(now)
                        self.save()
                        return {"protocol": PROTOCOL, "code": code, "port": port}
                    time.sleep(0.05)
            except (OSError, subprocess.CalledProcessError):
                pass
            try:
                self.command(["systemctl", "stop", f"naturecalls-room@{port}"], check=False)
            finally:
                raise BrokerError(503, "房间服务器启动超时，请稍后再试。")

    def lookup(self, code: int):
        with self.lock:
            if not self._valid_code(code) or code not in self.rooms:
                raise BrokerError(404, "房间不存在或已关闭。")
            return {"protocol": PROTOCOL, "code": code, "port": self.rooms[code]}

    def cleanup(self):
        with self.lock:
            dead = []
            for code, port in self.rooms.items():
                try:
                    result = self.command(["systemctl", "is-active", f"naturecalls-room@{port}"],
                                          check=False, capture_output=True, text=True)
                    if result.returncode != 0 or result.stdout.strip() != "active":
                        dead.append(code)
                except OSError:
                    dead.append(code)
            for code in dead:
                self.rooms.pop(code, None)
            if dead:
                self.save()
            return dead

    def health(self):
        return {"ok": True, "rooms": len(self.rooms), "max": self.max_rooms, "protocol": PROTOCOL}

    @staticmethod
    def _udp_listening(port):
        # ss is intentionally queried without a shell; it is only a readiness check.
        try:
            result = subprocess.run(["ss", "-lun"], capture_output=True, text=True, check=False)
            return bool(re.search(rf":{port}(?:\s|$)", result.stdout))
        except OSError:
            return False

    @staticmethod
    def _journal_ready(port):
        try:
            result = subprocess.run(["journalctl", "-u", f"naturecalls-room@{port}", "-n", "80", "--no-pager"],
                                    capture_output=True, text=True, check=False)
            return f"Server listening on UDP {port}" in result.stdout
        except OSError:
            return False


class Handler(BaseHTTPRequestHandler):
    broker: RoomBroker = None
    server_version = "NatureCallsBroker/1"

    def respond(self, status, payload):
        raw = json.dumps(payload, ensure_ascii=False).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)

    def do_GET(self):
        try:
            if self.path == "/health":
                return self.respond(200, self.broker.health())
            match = re.fullmatch(r"/rooms/(\d{3})", self.path)
            if match:
                return self.respond(200, self.broker.lookup(int(match.group(1))))
            raise BrokerError(404, "接口不存在。")
        except BrokerError as error:
            self.respond(error.status, {"protocol": PROTOCOL, "error": error.message})

    def do_POST(self):
        if self.path != "/rooms":
            return self.respond(404, {"protocol": PROTOCOL, "error": "接口不存在。"})
        length = int(self.headers.get("Content-Length", "0") or 0)
        if length > MAX_BODY:
            return self.respond(413, {"protocol": PROTOCOL, "error": "请求体过大。"})
        try:
            if length:
                json.loads(self.rfile.read(length))
            return self.respond(201, self.broker.create(self.client_address[0]))
        except (ValueError, json.JSONDecodeError):
            return self.respond(400, {"protocol": PROTOCOL, "error": "请求体必须是合法 JSON。"})
        except BrokerError as error:
            return self.respond(error.status, {"protocol": PROTOCOL, "error": error.message})

    def log_message(self, *_):
        pass


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--bind", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=8100)
    parser.add_argument("--max-rooms", type=int, default=12)
    parser.add_argument("--state", type=Path, default=Path("/var/lib/naturecalls-broker/rooms.json"))
    args = parser.parse_args(argv)
    broker = RoomBroker(args.state, args.max_rooms)
    broker.restore()
    Handler.broker = broker
    httpd = ThreadingHTTPServer((args.bind, args.port), Handler)
    def reap():
        while True:
            time.sleep(10)
            broker.cleanup()
    threading.Thread(target=reap, daemon=True).start()
    httpd.serve_forever()


if __name__ == "__main__":
    main()
