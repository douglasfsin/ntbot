#!/usr/bin/env python3
"""Stdio MCP server for Coolify via the REST API.

This instance does not expose Coolify's native Streamable HTTP endpoint at
``/mcp`` (POST returns Laravel CSRF 419 and GET redirects to /login). Cursor
Cloud Agents that point HTTP MCP at ``http://<host>:8000/mcp`` fail during
tool discovery. This process speaks MCP over stdio and calls ``/api/v1/*``.

Requires ``COOLIFY_ACCESS_TOKEN`` for API calls. ``tools/list`` works without
a token so Cursor discovery succeeds before the secret is present.
"""

from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
from typing import Any, Callable

PROTOCOL_VERSION = "2024-11-05"
SERVER_NAME = "coolify"
SERVER_VERSION = "1.1.0"
DEFAULT_BASE_URL = "http://46.225.161.55:8000"

NTBOT_PROJECT_UUID = "lbk5rfh2w9qe2ck0exs0l3eq"
NTBOT_API_UUID = "q9ekfmucjzkyn45i715lv0z2"
NTBOT_WEB_UUID = "hnoe3x858fi0ikuex9ubwr60"
SIGNOZ_SERVICE_UUID = "eva3s2kbg9a48onb3ws2hvgd"
POSTGRES_UUID = "q96lrxulc7eu01u8ln9tmszq"

SENSITIVE_KEY_MARKERS = (
    "password",
    "secret",
    "token",
    "key",
    "connectionstring",
    "connection_string",
    "private",
    "credential",
    "webhook",
    "jwt",
)


def eprint(*args: Any) -> None:
    print(*args, file=sys.stderr, flush=True)


def token_from_env() -> str:
    for name in ("COOLIFY_ACCESS_TOKEN", "COOLIFY_API_TOKEN", "COOLIFY_TOKEN"):
        raw = os.environ.get(name, "").strip()
        if raw:
            return raw.removeprefix("Bearer ").strip()
    return ""


def base_url_from_env() -> str:
    return os.environ.get("COOLIFY_BASE_URL", DEFAULT_BASE_URL).rstrip("/")


class CoolifyError(RuntimeError):
    pass


class CoolifyClient:
    def __init__(self, base_url: str, token: str) -> None:
        self.base_url = base_url.rstrip("/")
        self.token = token

    def request(self, method: str, path: str, body: Any | None = None) -> Any:
        url = f"{self.base_url}{path}"
        data = None if body is None else json.dumps(body).encode()
        req = urllib.request.Request(
            url,
            data=data,
            method=method,
            headers={
                "Authorization": f"Bearer {self.token}",
                "Accept": "application/json",
                "Content-Type": "application/json",
            },
        )
        try:
            with urllib.request.urlopen(req, timeout=30) as resp:
                raw = resp.read().decode()
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode(errors="replace")
            raise CoolifyError(f"{method} {path} -> HTTP {exc.code}: {detail}") from exc
        except urllib.error.URLError as exc:
            raise CoolifyError(f"{method} {path} -> {exc}") from exc


def _as_list(payload: Any) -> list[Any]:
    if payload is None:
        return []
    if isinstance(payload, list):
        return payload
    if isinstance(payload, dict):
        for key in ("data", "applications", "services", "projects", "envs", "resources", "deployments"):
            value = payload.get(key)
            if isinstance(value, list):
                return value
    return [payload]


def _is_sensitive_key(key: str) -> bool:
    lowered = key.lower()
    return any(marker in lowered for marker in SENSITIVE_KEY_MARKERS)


def _mask_env_items(items: list[Any], reveal: bool) -> list[dict[str, Any]]:
    masked: list[dict[str, Any]] = []
    for item in items:
        if not isinstance(item, dict):
            continue
        row = {
            "uuid": item.get("uuid"),
            "key": item.get("key") or item.get("name"),
            "is_runtime": item.get("is_runtime"),
            "is_buildtime": item.get("is_buildtime"),
            "is_preview": item.get("is_preview"),
        }
        value = item.get("value") or item.get("real_value")
        key = str(row["key"] or "")
        if reveal and not _is_sensitive_key(key):
            row["value"] = value
        elif value:
            row["value"] = "***"
        masked.append(row)
    return masked


def _summarize(item: dict[str, Any]) -> dict[str, Any]:
    return {
        "uuid": item.get("uuid"),
        "name": item.get("name"),
        "status": item.get("status"),
        "fqdn": item.get("fqdn") or item.get("fqdns"),
        "git_repository": item.get("git_repository") or item.get("repository_project_id"),
        "git_branch": item.get("git_branch"),
        "description": item.get("description"),
    }


def require_client() -> CoolifyClient:
    token = token_from_env()
    if not token:
        raise CoolifyError(
            "COOLIFY_ACCESS_TOKEN is missing. Add it as a Cursor Cloud Agent "
            "secret (or local env) and reconnect the Coolify MCP as stdio, "
            "not HTTP to /mcp."
        )
    return CoolifyClient(base_url_from_env(), token)


def probe_native_mcp(base_url: str) -> dict[str, Any]:
    url = f"{base_url.rstrip('/')}/mcp"
    body = json.dumps(
        {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "protocolVersion": PROTOCOL_VERSION,
                "capabilities": {},
                "clientInfo": {"name": "coolify-mcp-probe", "version": SERVER_VERSION},
            },
        }
    ).encode()
    req = urllib.request.Request(
        url,
        data=body,
        method="POST",
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json, text/event-stream",
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=8) as resp:
            raw = resp.read().decode(errors="replace")
            return {"ok": True, "http": resp.status, "body": raw[:300]}
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode(errors="replace")
        return {
            "ok": False,
            "http": exc.code,
            "body": detail[:300],
            "hint": (
                "Native Coolify /mcp is not available on this instance. "
                "Use this stdio server against /api/v1 instead of HTTP MCP."
            ),
        }
    except urllib.error.URLError as exc:
        return {"ok": False, "error": str(exc)}


def handle_get_status(_args: dict[str, Any]) -> dict[str, Any]:
    base = base_url_from_env()
    token_present = bool(token_from_env())
    health = None
    try:
        with urllib.request.urlopen(f"{base}/api/v1/health", timeout=8) as resp:
            health = resp.read().decode(errors="replace")
    except Exception as exc:  # noqa: BLE001 — status probe
        health = f"error: {exc}"
    version: Any = None
    if token_present:
        try:
            version = require_client().request("GET", "/api/v1/version")
        except CoolifyError as exc:
            version = str(exc)
    native = probe_native_mcp(base)
    return {
        "base_url": base,
        "token_present": token_present,
        "api_health": health,
        "api_version": version,
        "native_mcp": native,
        "transport": "stdio",
        "server": {"name": SERVER_NAME, "version": SERVER_VERSION},
    }


def handle_get_version(_args: dict[str, Any]) -> Any:
    return require_client().request("GET", "/api/v1/version")


def handle_list_projects(_args: dict[str, Any]) -> list[dict[str, Any]]:
    return [_summarize(item) for item in _as_list(require_client().request("GET", "/api/v1/projects")) if isinstance(item, dict)]


def handle_get_project(args: dict[str, Any]) -> Any:
    uuid = args["uuid"]
    return require_client().request("GET", f"/api/v1/projects/{uuid}")


def handle_list_environments(args: dict[str, Any]) -> Any:
    uuid = args["uuid"]
    return require_client().request("GET", f"/api/v1/projects/{uuid}/environments")


def handle_list_applications(_args: dict[str, Any]) -> list[dict[str, Any]]:
    return [_summarize(item) for item in _as_list(require_client().request("GET", "/api/v1/applications")) if isinstance(item, dict)]


def handle_get_application(args: dict[str, Any]) -> Any:
    return require_client().request("GET", f"/api/v1/applications/{args['uuid']}")


def handle_list_application_envs(args: dict[str, Any]) -> list[dict[str, Any]]:
    items = _as_list(require_client().request("GET", f"/api/v1/applications/{args['uuid']}/envs"))
    return _mask_env_items(items, bool(args.get("reveal")))


def handle_upsert_application_env(args: dict[str, Any]) -> dict[str, Any]:
    client = require_client()
    uuid = args["uuid"]
    key = args["key"]
    value = args["value"]
    existing = {
        str(item.get("key") or item.get("name")): item
        for item in _as_list(client.request("GET", f"/api/v1/applications/{uuid}/envs"))
        if isinstance(item, dict)
    }
    body = {
        "key": key,
        "value": value,
        "is_literal": True,
        "is_preview": False,
        "is_shown_once": False,
        "is_buildtime": False,
        "is_runtime": True,
    }
    current = existing.get(key)
    if current and current.get("uuid"):
        client.request("PATCH", f"/api/v1/applications/{uuid}/envs", {**body, "uuid": current["uuid"]})
        return {"action": "updated", "key": key, "application": uuid}
    client.request("POST", f"/api/v1/applications/{uuid}/envs", body)
    return {"action": "created", "key": key, "application": uuid}


def handle_list_services(_args: dict[str, Any]) -> list[dict[str, Any]]:
    return [_summarize(item) for item in _as_list(require_client().request("GET", "/api/v1/services")) if isinstance(item, dict)]


def handle_get_service(args: dict[str, Any]) -> Any:
    return require_client().request("GET", f"/api/v1/services/{args['uuid']}")


def handle_list_service_envs(args: dict[str, Any]) -> list[dict[str, Any]]:
    items = _as_list(require_client().request("GET", f"/api/v1/services/{args['uuid']}/envs"))
    return _mask_env_items(items, bool(args.get("reveal")))


def handle_list_databases(_args: dict[str, Any]) -> list[dict[str, Any]]:
    return [_summarize(item) for item in _as_list(require_client().request("GET", "/api/v1/databases")) if isinstance(item, dict)]


def handle_get_database(args: dict[str, Any]) -> Any:
    return require_client().request("GET", f"/api/v1/databases/{args['uuid']}")


def handle_list_deployments(_args: dict[str, Any]) -> Any:
    return require_client().request("GET", "/api/v1/deployments")


def handle_deploy(args: dict[str, Any]) -> Any:
    uuid = args["uuid"]
    force = "true" if args.get("force") else "false"
    query = urllib.parse.urlencode({"uuid": uuid, "force": force})
    return require_client().request("GET", f"/api/v1/deploy?{query}")


def handle_control(args: dict[str, Any]) -> Any:
    kind = args["kind"]
    action = args["action"]
    uuid = args["uuid"]
    if kind not in {"applications", "services", "databases"}:
        raise CoolifyError("kind must be applications, services, or databases")
    if action not in {"start", "stop", "restart"}:
        raise CoolifyError("action must be start, stop, or restart")
    return require_client().request("GET", f"/api/v1/{kind}/{uuid}/{action}")


def handle_get_application_logs(args: dict[str, Any]) -> Any:
    uuid = args["uuid"]
    lines = int(args.get("lines") or 100)
    return require_client().request("GET", f"/api/v1/applications/{uuid}/logs?lines={lines}")


def handle_get_ntbot_inventory(_args: dict[str, Any]) -> dict[str, Any]:
    client = require_client()
    return {
        "version": client.request("GET", "/api/v1/version"),
        "project_uuid": NTBOT_PROJECT_UUID,
        "applications": handle_list_applications({}),
        "services": handle_list_services({}),
        "databases": handle_list_databases({}),
        "known": {
            "ntbot_api": NTBOT_API_UUID,
            "ntbot_web": NTBOT_WEB_UUID,
            "signoz": SIGNOZ_SERVICE_UUID,
            "postgres_ntquant": POSTGRES_UUID,
        },
    }


def handle_sync_ntbot_otel(args: dict[str, Any]) -> dict[str, Any]:
    client = require_client()
    endpoint = args.get("otlp_endpoint") or f"http://{SIGNOZ_SERVICE_UUID}-otel-collector:4318"
    specs = {
        NTBOT_API_UUID: {
            "OTEL_SERVICE_NAME": "ntbot-api",
            "OTEL_EXPORTER_OTLP_PROTOCOL": "http/protobuf",
            "OTEL_RESOURCE_ATTRIBUTES": "service.namespace=NtBot,project=NtBot,deployment.environment=Production",
        },
        NTBOT_WEB_UUID: {
            "OTEL_SERVICE_NAME": "ntbot-web",
            "OTEL_EXPORTER_OTLP_PROTOCOL": "http/protobuf",
            "OTEL_RESOURCE_ATTRIBUTES": "service.namespace=NtBot,project=NtBot,deployment.environment=Production",
        },
    }
    results: list[dict[str, Any]] = []
    for uuid, extras in specs.items():
        for key, value in {"OTEL_EXPORTER_OTLP_ENDPOINT": endpoint, **extras}.items():
            results.append(handle_upsert_application_env({"uuid": uuid, "key": key, "value": value}))
        if args.get("deploy"):
            results.append({"deploy": handle_deploy({"uuid": uuid, "force": False})})
    return {"otlp_endpoint": endpoint, "changes": results}


Handler = Callable[[dict[str, Any]], Any]

TOOLS: dict[str, dict[str, Any]] = {
    "get_status": {
        "description": "Diagnose Coolify MCP vs REST API. Reports whether native /mcp works (it does not on this host) and whether COOLIFY_ACCESS_TOKEN is present. No secrets returned.",
        "inputSchema": {"type": "object", "properties": {}},
        "handler": handle_get_status,
    },
    "get_version": {
        "description": "Coolify API version (GET /api/v1/version).",
        "inputSchema": {"type": "object", "properties": {}},
        "handler": handle_get_version,
    },
    "list_projects": {
        "description": "List Coolify projects (uuid, name).",
        "inputSchema": {"type": "object", "properties": {}},
        "handler": handle_list_projects,
    },
    "get_project": {
        "description": "Get a Coolify project by UUID.",
        "inputSchema": {
            "type": "object",
            "properties": {"uuid": {"type": "string"}},
            "required": ["uuid"],
        },
        "handler": handle_get_project,
    },
    "list_environments": {
        "description": "List environments in a Coolify project.",
        "inputSchema": {
            "type": "object",
            "properties": {"uuid": {"type": "string", "description": "Project UUID"}},
            "required": ["uuid"],
        },
        "handler": handle_list_environments,
    },
    "list_applications": {
        "description": "List Coolify applications (uuid, name, status, fqdn).",
        "inputSchema": {"type": "object", "properties": {}},
        "handler": handle_list_applications,
    },
    "get_application": {
        "description": "Get a Coolify application by UUID.",
        "inputSchema": {
            "type": "object",
            "properties": {"uuid": {"type": "string"}},
            "required": ["uuid"],
        },
        "handler": handle_get_application,
    },
    "list_application_envs": {
        "description": "List application env var names. Values are masked unless reveal=true and the key is not sensitive.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "uuid": {"type": "string"},
                "reveal": {"type": "boolean", "default": False},
            },
            "required": ["uuid"],
        },
        "handler": handle_list_application_envs,
    },
    "upsert_application_env": {
        "description": "Create or update a runtime environment variable on an application.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "uuid": {"type": "string"},
                "key": {"type": "string"},
                "value": {"type": "string"},
            },
            "required": ["uuid", "key", "value"],
        },
        "handler": handle_upsert_application_env,
    },
    "list_services": {
        "description": "List Coolify one-click/compose services (includes SigNoz).",
        "inputSchema": {"type": "object", "properties": {}},
        "handler": handle_list_services,
    },
    "get_service": {
        "description": "Get a Coolify service by UUID.",
        "inputSchema": {
            "type": "object",
            "properties": {"uuid": {"type": "string"}},
            "required": ["uuid"],
        },
        "handler": handle_get_service,
    },
    "list_service_envs": {
        "description": "List service env var names. Values masked by default.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "uuid": {"type": "string"},
                "reveal": {"type": "boolean", "default": False},
            },
            "required": ["uuid"],
        },
        "handler": handle_list_service_envs,
    },
    "list_databases": {
        "description": "List Coolify databases.",
        "inputSchema": {"type": "object", "properties": {}},
        "handler": handle_list_databases,
    },
    "get_database": {
        "description": "Get a Coolify database by UUID.",
        "inputSchema": {
            "type": "object",
            "properties": {"uuid": {"type": "string"}},
            "required": ["uuid"],
        },
        "handler": handle_get_database,
    },
    "list_deployments": {
        "description": "List running/recent Coolify deployments.",
        "inputSchema": {"type": "object", "properties": {}},
        "handler": handle_list_deployments,
    },
    "deploy": {
        "description": "Queue a Coolify deploy by resource UUID.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "uuid": {"type": "string"},
                "force": {"type": "boolean", "default": False},
            },
            "required": ["uuid"],
        },
        "handler": handle_deploy,
    },
    "control": {
        "description": "Start, stop, or restart an application, service, or database.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "kind": {"type": "string", "enum": ["applications", "services", "databases"]},
                "action": {"type": "string", "enum": ["start", "stop", "restart"]},
                "uuid": {"type": "string"},
            },
            "required": ["kind", "action", "uuid"],
        },
        "handler": handle_control,
    },
    "get_application_logs": {
        "description": "Fetch recent application container logs.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "uuid": {"type": "string"},
                "lines": {"type": "integer", "default": 100},
            },
            "required": ["uuid"],
        },
        "handler": handle_get_application_logs,
    },
    "get_ntbot_inventory": {
        "description": "NtBot-focused Coolify inventory (Api, Web, SigNoz, Postgres) plus live lists.",
        "inputSchema": {"type": "object", "properties": {}},
        "handler": handle_get_ntbot_inventory,
    },
    "sync_ntbot_otel": {
        "description": "Write OTEL env vars on NTBot.Api and NTBot.Web. Optionally queue deploys.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "otlp_endpoint": {
                    "type": "string",
                    "description": "OTLP HTTP collector URL. Defaults to internal SigNoz otel-collector:4318.",
                },
                "deploy": {"type": "boolean", "default": False},
            },
        },
        "handler": handle_sync_ntbot_otel,
    },
}


def tools_list_payload() -> dict[str, Any]:
    tools = []
    for name, spec in TOOLS.items():
        tools.append(
            {
                "name": name,
                "description": spec["description"],
                "inputSchema": spec["inputSchema"],
            }
        )
    return {"tools": tools}


def call_tool(name: str, arguments: dict[str, Any] | None) -> dict[str, Any]:
    spec = TOOLS.get(name)
    if spec is None:
        return {
            "content": [{"type": "text", "text": f"Unknown tool: {name}"}],
            "isError": True,
        }
    try:
        result = spec["handler"](arguments or {})
        text = result if isinstance(result, str) else json.dumps(result, ensure_ascii=False, indent=2, default=str)
        return {"content": [{"type": "text", "text": text}]}
    except (CoolifyError, KeyError, ValueError, TypeError) as exc:
        return {"content": [{"type": "text", "text": str(exc)}], "isError": True}


def handle_rpc(message: dict[str, Any]) -> dict[str, Any] | None:
    method = message.get("method")
    msg_id = message.get("id")
    params = message.get("params") or {}

    if method == "initialize":
        return {
            "jsonrpc": "2.0",
            "id": msg_id,
            "result": {
                "protocolVersion": PROTOCOL_VERSION,
                "capabilities": {"tools": {"listChanged": False}},
                "serverInfo": {"name": SERVER_NAME, "version": SERVER_VERSION},
                "instructions": (
                    "Coolify REST MCP. Do not use HTTP to /mcp on this host. "
                    "Set COOLIFY_ACCESS_TOKEN before calling API tools."
                ),
            },
        }
    if method == "notifications/initialized" or method is None or msg_id is None:
        return None
    if method == "ping":
        return {"jsonrpc": "2.0", "id": msg_id, "result": {}}
    if method == "tools/list":
        return {"jsonrpc": "2.0", "id": msg_id, "result": tools_list_payload()}
    if method == "tools/call":
        name = params.get("name")
        arguments = params.get("arguments") or {}
        return {"jsonrpc": "2.0", "id": msg_id, "result": call_tool(str(name), arguments)}

    return {
        "jsonrpc": "2.0",
        "id": msg_id,
        "error": {"code": -32601, "message": f"Method not found: {method}"},
    }


def read_message(stdin: Any) -> dict[str, Any] | None:
    line = stdin.readline()
    if not line:
        return None
    if line.startswith(b"{") or line.startswith(b"["):
        return json.loads(line)
    headers = [line]
    while True:
        nxt = stdin.readline()
        if not nxt:
            return None
        if nxt in (b"\r\n", b"\n"):
            break
        headers.append(nxt)
    length: int | None = None
    for raw in b"".join(headers).decode("utf-8", errors="replace").splitlines():
        if raw.lower().startswith("content-length:"):
            length = int(raw.split(":", 1)[1].strip())
    if length is None:
        raise CoolifyError("MCP frame missing Content-Length")
    body = stdin.read(length)
    return json.loads(body.decode("utf-8"))


def write_message(stdout: Any, message: dict[str, Any]) -> None:
    body = json.dumps(message, ensure_ascii=False).encode("utf-8")
    stdout.write(f"Content-Length: {len(body)}\r\n\r\n".encode("ascii") + body)
    stdout.flush()


def serve() -> int:
    stdin = sys.stdin.buffer
    stdout = sys.stdout.buffer
    eprint(f"{SERVER_NAME} mcp {SERVER_VERSION} base={base_url_from_env()} token={'yes' if token_from_env() else 'no'}")
    while True:
        try:
            message = read_message(stdin)
        except Exception as exc:  # noqa: BLE001
            eprint(f"read error: {exc}")
            return 1
        if message is None:
            return 0
        try:
            reply = handle_rpc(message)
        except Exception as exc:  # noqa: BLE001
            reply = {
                "jsonrpc": "2.0",
                "id": message.get("id"),
                "error": {"code": -32603, "message": str(exc)},
            }
        if reply is not None:
            write_message(stdout, reply)


def main(argv: list[str] | None = None) -> int:
    args = list(sys.argv[1:] if argv is None else argv)
    if args[:1] == ["--self-test"]:
        init = handle_rpc(
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "initialize",
                "params": {"protocolVersion": PROTOCOL_VERSION, "capabilities": {}, "clientInfo": {"name": "self-test"}},
            }
        )
        listed = handle_rpc({"jsonrpc": "2.0", "id": 2, "method": "tools/list"})
        status = call_tool("get_status", {})
        print(json.dumps({"initialize": init, "tools": listed, "status": status}, indent=2, default=str))
        return 0
    return serve()


if __name__ == "__main__":
    raise SystemExit(main())
