#!/usr/bin/env python3
"""Inspect Coolify deploys and sync OpenTelemetry env vars onto NtBot apps.

Requires COOLIFY_ACCESS_TOKEN (and optional COOLIFY_BASE_URL).
Never prints secret values — only key names and public URLs.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from typing import Any

DEFAULT_BASE_URL = "http://46.225.161.55:8000"
SIGNOZ_SERVICE_UUID = "eva3s2kbg9a48onb3ws2hvgd"
NTBOT_PROJECT_UUID = "lbk5rfh2w9qe2ck0exs0l3eq"
NTBOT_API_UUID = "q9ekfmucjzkyn45i715lv0z2"
NTBOT_WEB_UUID = "hnoe3x858fi0ikuex9ubwr60"
POSTGRES_UUID = "q96lrxulc7eu01u8ln9tmszq"

OTEL_KEYS = {
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

PUBLIC_URL_KEYS = (
    "SERVICE_URL_OTELCOLLECTORHTTP_4318",
    "SERVICE_URL_OTELCOLLECTOR_4318",
    "SERVICE_URL_SIGNOZ_8080",
)


class CoolifyClient:
    def __init__(self, base_url: str, token: str) -> None:
        self.base_url = base_url.rstrip("/")
        self.token = token.removeprefix("Bearer ").strip()

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
            raise RuntimeError(f"{method} {path} -> HTTP {exc.code}: {detail}") from exc


def _as_list(payload: Any) -> list[dict[str, Any]]:
    if payload is None:
        return []
    if isinstance(payload, list):
        return payload
    if isinstance(payload, dict):
        for key in ("data", "applications", "services", "projects", "envs", "resources"):
            value = payload.get(key)
            if isinstance(value, list):
                return value
    return []


def resource_label(item: dict[str, Any]) -> str:
    return item.get("name") or item.get("fqdn") or item.get("uuid") or "?"


def fqdn_of(item: dict[str, Any]) -> str:
    return item.get("fqdn") or item.get("fqdns") or ""


def print_inventory(client: CoolifyClient) -> dict[str, Any]:
    try:
        version = client.request("GET", "/api/v1/version")
        print(f"coolify version: {version}")
    except RuntimeError as exc:
        print(f"coolify version: unavailable ({exc})")

    inventory: dict[str, Any] = {"projects": [], "applications": [], "services": [], "databases": []}
    for kind, path in (
        ("projects", "/api/v1/projects"),
        ("applications", "/api/v1/applications"),
        ("services", "/api/v1/services"),
        ("databases", "/api/v1/databases"),
    ):
        try:
            inventory[kind] = _as_list(client.request("GET", path))
        except RuntimeError as exc:
            print(f"\n## {kind.title()}\n- skipped: {exc}")
            continue
        print(f"\n## {kind.title()}")
        known = {
            "projects": {NTBOT_PROJECT_UUID},
            "applications": {NTBOT_API_UUID, NTBOT_WEB_UUID},
            "services": {SIGNOZ_SERVICE_UUID},
            "databases": {POSTGRES_UUID},
        }[kind]
        for item in inventory[kind]:
            mark = " *" if item.get("uuid") in known or SIGNOZ_SERVICE_UUID in str(item.get("uuid", "")) else ""
            extra = ""
            if kind != "projects":
                extra = f"  status={item.get('status')}"
            if kind in {"applications", "services"}:
                extra += f"  fqdn={fqdn_of(item)}"
            print(f"- {resource_label(item)}  uuid={item.get('uuid')}{extra}{mark}")
    return inventory


def list_env_keys(client: CoolifyClient, kind: str, uuid: str) -> list[dict[str, Any]]:
    path = f"/api/v1/{kind}/{uuid}/envs"
    return _as_list(client.request("GET", path))


def env_map(envs: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    mapped: dict[str, dict[str, Any]] = {}
    for item in envs:
        key = item.get("key") or item.get("name")
        if key:
            mapped[str(key)] = item
    return mapped


def public_otlp_endpoint(client: CoolifyClient) -> str | None:
    try:
        envs = env_map(list_env_keys(client, "services", SIGNOZ_SERVICE_UUID))
    except RuntimeError as exc:
        print(f"warn: could not list SigNoz service envs: {exc}", file=sys.stderr)
        return None

    print("\n## SigNoz service env keys")
    for key in sorted(envs):
        suffix = ""
        if key in PUBLIC_URL_KEYS:
            suffix = f" = {envs[key].get('value') or envs[key].get('real_value') or ''}"
        print(f"- {key}{suffix}")

    for key in PUBLIC_URL_KEYS:
        raw = envs.get(key, {}).get("value") or envs.get(key, {}).get("real_value")
        if raw and "4318" in str(raw):
            return str(raw).rstrip("/")
    return None


def upsert_env(client: CoolifyClient, uuid: str, key: str, value: str, existing: dict[str, dict[str, Any]]) -> str:
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
        client.request("PATCH", f"/api/v1/applications/{uuid}/envs", body)
        return "updated"
    try:
        client.request("POST", f"/api/v1/applications/{uuid}/envs", body)
        return "created"
    except RuntimeError:
        client.request("PATCH", f"/api/v1/applications/{uuid}/envs", body)
        return "updated"


def sync_otel(client: CoolifyClient, endpoint: str, deploy: bool, force: bool = False) -> None:
    print(f"\n## Sync OTEL endpoint {endpoint}")
    for uuid, extras in OTEL_KEYS.items():
        try:
            existing = env_map(list_env_keys(client, "applications", uuid))
        except RuntimeError as exc:
            print(f"  warn: cannot list envs ({exc}); posting keys")
            existing = {}
        values = {
            "OTEL_EXPORTER_OTLP_ENDPOINT": endpoint,
            **extras,
        }
        print(f"app {uuid}")
        for key, value in values.items():
            action = upsert_env(client, uuid, key, value, existing)
            print(f"  {action} {key}")
        if deploy:
            result = client.request("GET", f"/api/v1/deploy?uuid={uuid}&force={'true' if force else 'false'}")
            print(f"  deploy: {result}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", default=os.environ.get("COOLIFY_BASE_URL", DEFAULT_BASE_URL))
    parser.add_argument("--token", default=os.environ.get("COOLIFY_ACCESS_TOKEN") or os.environ.get("COOLIFY_TOKEN"))
    parser.add_argument("--sync-otel", action="store_true")
    parser.add_argument("--otlp-endpoint", default=os.environ.get("OTEL_EXPORTER_OTLP_ENDPOINT"))
    parser.add_argument("--deploy", action="store_true")
    parser.add_argument("--force", action="store_true", help="Force rebuild when deploying")
    parser.add_argument(
        "--git-branch",
        default=os.environ.get("COOLIFY_GIT_BRANCH"),
        help="PATCH git_branch on NtBot.Api/Web (uses is_preserve_repository_enabled)",
    )
    args = parser.parse_args()

    if not args.token:
        print("COOLIFY_ACCESS_TOKEN is required.", file=sys.stderr)
        return 2

    client = CoolifyClient(args.base_url, args.token)
    print_inventory(client)
    discovered = public_otlp_endpoint(client)
    endpoint = args.otlp_endpoint or discovered or f"http://{SIGNOZ_SERVICE_UUID}-otel-collector:4318"
    print(f"\nresolved OTLP endpoint: {endpoint}")

    if args.git_branch:
        for uuid, name in ((NTBOT_API_UUID, "api"), (NTBOT_WEB_UUID, "web")):
            result = client.request(
                "PATCH",
                f"/api/v1/applications/{uuid}",
                {
                    "git_branch": args.git_branch,
                    "is_preserve_repository_enabled": True,
                },
            )
            print(f"git_branch {name}: {result}")

    if args.sync_otel:
        sync_otel(client, endpoint, args.deploy, args.force)
    elif args.deploy:
        for uuid, name in ((NTBOT_API_UUID, "api"), (NTBOT_WEB_UUID, "web")):
            result = client.request("GET", f"/api/v1/deploy?uuid={uuid}&force={'true' if args.force else 'false'}")
            print(f"deploy {name}: {result}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
