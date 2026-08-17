#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SERVER = ROOT / "scripts" / "coolify" / "mcp_server.py"


def load_server():
    spec = importlib.util.spec_from_file_location("coolify_mcp_server", SERVER)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


def encode_rpc(message: dict) -> bytes:
    body = json.dumps(message).encode("utf-8")
    return f"Content-Length: {len(body)}\r\n\r\n".encode("ascii") + body


def decode_frames(raw: bytes) -> list[dict]:
    messages: list[dict] = []
    offset = 0
    while offset < len(raw):
        header_end = raw.find(b"\r\n\r\n", offset)
        if header_end < 0:
            break
        headers = raw[offset:header_end].decode("utf-8")
        length = None
        for line in headers.splitlines():
            if line.lower().startswith("content-length:"):
                length = int(line.split(":", 1)[1].strip())
        if length is None:
            break
        start = header_end + 4
        end = start + length
        messages.append(json.loads(raw[start:end].decode("utf-8")))
        offset = end
    return messages


class CoolifyMcpServerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.mod = load_server()

    def test_tools_list_without_token(self) -> None:
        listed = self.mod.handle_rpc({"jsonrpc": "2.0", "id": 2, "method": "tools/list"})
        names = {tool["name"] for tool in listed["result"]["tools"]}
        self.assertIn("get_status", names)
        self.assertIn("list_applications", names)
        self.assertIn("sync_ntbot_otel", names)

    def test_initialize_advertises_stdio_not_native_mcp(self) -> None:
        reply = self.mod.handle_rpc(
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "initialize",
                "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "t"}},
            }
        )
        self.assertEqual(reply["result"]["serverInfo"]["name"], "coolify")
        self.assertIn("Do not use HTTP to /mcp", reply["result"]["instructions"])

    def test_api_tool_without_token_is_error_not_crash(self) -> None:
        result = self.mod.call_tool("list_applications", {})
        self.assertTrue(result.get("isError"))
        self.assertIn("COOLIFY_ACCESS_TOKEN", result["content"][0]["text"])

    def test_sensitive_env_values_stay_masked(self) -> None:
        items = [
            {"uuid": "a", "key": "JWT_SECRET", "value": "super-secret"},
            {"uuid": "b", "key": "OTEL_SERVICE_NAME", "value": "ntbot-api"},
        ]
        masked = self.mod._mask_env_items(items, reveal=True)
        by_key = {row["key"]: row["value"] for row in masked}
        self.assertEqual(by_key["JWT_SECRET"], "***")
        self.assertEqual(by_key["OTEL_SERVICE_NAME"], "ntbot-api")

    def test_stdio_initialize_and_tools_list(self) -> None:
        payload = (
            encode_rpc(
                {
                    "jsonrpc": "2.0",
                    "id": 1,
                    "method": "initialize",
                    "params": {
                        "protocolVersion": "2024-11-05",
                        "capabilities": {},
                        "clientInfo": {"name": "unittest"},
                    },
                }
            )
            + encode_rpc({"jsonrpc": "2.0", "method": "notifications/initialized"})
            + encode_rpc({"jsonrpc": "2.0", "id": 2, "method": "tools/list"})
        )
        proc = subprocess.run(
            [sys.executable, str(SERVER)],
            input=payload,
            capture_output=True,
            timeout=10,
            check=False,
        )
        self.assertEqual(proc.returncode, 0, proc.stderr.decode())
        frames = decode_frames(proc.stdout)
        self.assertGreaterEqual(len(frames), 2)
        self.assertEqual(frames[0]["result"]["serverInfo"]["name"], "coolify")
        names = {tool["name"] for tool in frames[1]["result"]["tools"]}
        self.assertIn("get_status", names)


if __name__ == "__main__":
    unittest.main()
