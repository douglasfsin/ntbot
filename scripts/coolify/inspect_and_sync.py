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
    version = client.request("GET", "/api/v1/version")
    print(f"coolify version: {version}")

    projects = _as_list(client.request("GET", "/api/v1/projects"))
    apps = _as_list(client.request("GET", "/api/v1/applications"))
    services = _as_list(client.request("GET", "/api/v1/services"))
    databases = _as_list(client.request("GET", "/api/v1/databases"))

    print("\n## Projects")
    for item in projects:
        mark = " *" if item.get("uuid") == NTBOT_PROJECT_UUID else ""
        print(f"- {resource_label(item)}  uuid={item.get('uuid')}{mark}")

    print("\n## Applications")
    for item in apps:
        mark = " *" if item.get("uuid") in {NTBOT_API_UUID, NTBOT_WEB_UUID} else ""
        print(
            f"- {resource_label(item)}  uuid={item.get('uuid')}  "
            f"status={item.get('status')}  fqdn={fqdn_of(item)}{mark}"
        )

    print("\n## Services")
    for item in services:
        mark = " *" if SIGNOZ_SERVICE_UUID in str(item.get("uuid", "")) else ""
        print(
            f"- {resource_label(item)}  uuid={item.get('uuid')}  "
            f"status={item.get('status')}  fqdn={fqdn_of(item)}{mark}"
        )

    print("\n## Databases")
    for item in databases:
        mark = " *" if item.get("uuid") == POSTGRES_UUID else ""
        print(f"- {resource_label(item)}  uuid={item.get('uuid')}  status={item.get('status')}{mark}")

    return {"projects": projects, "applications": apps, "services": services, "databases": databases}


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
        client.request("PATCH", f"/api/v1/applications/{uuid}/envs", {**body, "uuid": current["uuid"]})
        return "updated"
    client.request("POST", f"/api/v1/applications/{uuid}/envs", body)
    return "created"


def sync_otel(client: CoolifyClient, endpoint: str, deploy: bool) -> None:
    print(f"\n## Sync OTEL endpoint {endpoint}")
    for uuid, extras in OTEL_KEYS.items():
        existing = env_map(list_env_keys(client, "applications", uuid))
        values = {
            "OTEL_EXPORTER_OTLP_ENDPOINT": endpoint,
            **extras,
        }
        print(f"app {uuid}")
        for key, value in values.items():
            action = upsert_env(client, uuid, key, value, existing)
            print(f"  {action} {key}")
        if deploy:
            result = client.request("GET", f"/api/v1/deploy?uuid={uuid}&force=false")
            print(f"  deploy: {result}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", default=os.environ.get("COOLIFY_BASE_URL", DEFAULT_BASE_URL))
    parser.add_argument("--token", default=os.environ.get("COOLIFY_ACCESS_TOKEN") or os.environ.get("COOLIFY_TOKEN"))
    parser.add_argument("--sync-otel", action="store_true")
    parser.add_argument("--otlp-endpoint", default=os.environ.get("OTEL_EXPORTER_OTLP_ENDPOINT"))
    parser.add_argument("--deploy", action="store_true")
    args = parser.parse_args()

    if not args.token:
        print("COOLIFY_ACCESS_TOKEN is required.", file=sys.stderr)
        return 2

    client = CoolifyClient(args.base_url, args.token)
    print_inventory(client)
    discovered = public_otlp_endpoint(client)
    endpoint = args.otlp_endpoint or discovered or f"http://{SIGNOZ_SERVICE_UUID}-otel-collector:4318"
    print(f"\nresolved OTLP endpoint: {endpoint}")

    if args.sync_otel:
        sync_otel(client, endpoint, args.deploy)
    return 0


if __name__ == "__main__":
    sys.exit(main())
