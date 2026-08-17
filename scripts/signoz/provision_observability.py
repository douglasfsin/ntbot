#!/usr/bin/env python3
"""Create or update SigNoz logs views and dashboards for NtBot, Orbital and Montescar."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid
from typing import Any

PROJECTS = ("NtBot", "Orbital", "Montescar")


def panel_id(project: str, key: str) -> str:
    digest = hashlib.sha256(f"ntbot-signoz:{project}:{key}".encode()).digest()
    return str(uuid.UUID(bytes=digest[:16]))


def ns_filter(project: str) -> str:
    return f"service.namespace = '{project}'"


def error_filter(project: str) -> str:
    return f"{ns_filter(project)} AND severity_text IN ['ERROR', 'Fatal', 'FATAL']"


def warn_filter(project: str) -> str:
    return f"{ns_filter(project)} AND severity_text IN ['WARN', 'Warning', 'WARNING']"


def order(name: str, direction: str = "desc") -> dict[str, Any]:
    return {"key": {"name": name}, "direction": direction}


def log_spec(filter_expr: str, aggregation: str, *, group_by: str | None = None, step: bool = False) -> dict[str, Any]:
    spec: dict[str, Any] = {
        "name": "A",
        "signal": "logs",
        "source": "",
        "disabled": False,
        "filter": {"expression": filter_expr},
        "having": {"expression": ""},
        "aggregations": [{"expression": aggregation}],
        "limit": 100,
        "order": [order("__result")],
    }
    if group_by:
        spec["groupBy"] = [{"name": group_by}]
    if step:
        spec["stepInterval"] = None
    return spec


def builder_query(spec: dict[str, Any]) -> dict[str, Any]:
    return {"type": "builder_query", "spec": spec}


def timeseries_panel(title: str, filter_expr: str, *, group_by: str | None = None) -> dict[str, Any]:
    spec = log_spec(filter_expr, "count()", group_by=group_by, step=True)
    return {
        "kind": "Panel",
        "spec": {
            "display": {"name": title},
            "plugin": {
                "kind": "signoz/TimeSeriesPanel",
                "spec": {
                    "visualization": {"timePreference": "global_time"},
                    "legend": {"position": "bottom"},
                    "chartAppearance": {
                        "lineStyle": "solid",
                        "lineInterpolation": "spline",
                        "fillMode": "none",
                    },
                },
            },
            "queries": [
                {
                    "kind": "time_series",
                    "spec": {
                        "plugin": {
                            "kind": "signoz/CompositeQuery",
                            "spec": {"queries": [builder_query(spec)]},
                        }
                    },
                }
            ],
        },
    }


def value_panel(title: str, filter_expr: str) -> dict[str, Any]:
    spec = log_spec(filter_expr, "count()")
    return {
        "kind": "Panel",
        "spec": {
            "display": {"name": title},
            "plugin": {
                "kind": "signoz/ValuePanel",
                "spec": {"visualization": {"timePreference": "global_time"}},
            },
            "queries": [
                {
                    "kind": "scalar",
                    "spec": {
                        "plugin": {
                            "kind": "signoz/CompositeQuery",
                            "spec": {"queries": [builder_query(spec)]},
                        }
                    },
                }
            ],
        },
    }


def grid(title: str, items: list[dict[str, Any]]) -> dict[str, Any]:
    return {"kind": "Grid", "spec": {"display": {"title": title}, "items": items}}


def item(x: int, y: int, width: int, height: int, pid: str) -> dict[str, Any]:
    return {
        "x": x,
        "y": y,
        "width": width,
        "height": height,
        "content": {"$ref": f"#/spec/panels/{pid}"},
    }


def dynamic_variable(name: str, attribute: str, display: str) -> dict[str, Any]:
    return {
        "kind": "ListVariable",
        "spec": {
            "name": name,
            "display": {"name": display, "description": ""},
            "allowMultiple": False,
            "allowAllValue": True,
            "sort": "none",
            "plugin": {
                "kind": "signoz/DynamicVariable",
                "spec": {"name": attribute, "signal": "logs"},
            },
        },
    }


def dashboard_payload(project: str) -> dict[str, Any]:
    volume = panel_id(project, "log-volume")
    errors = panel_id(project, "error-volume")
    warns = panel_id(project, "warn-volume")
    error_count = panel_id(project, "error-count")
    by_severity = panel_id(project, "by-severity")
    by_service = panel_id(project, "by-service")
    return {
        "schemaVersion": "v6",
        "image": "/assets/Icons/bar-chart",
        "tags": [
            {"key": "project", "value": project},
            {"key": "signal", "value": "logs"},
            {"key": "managed-by", "value": "ntbot"},
        ],
        "spec": {
            "display": {
                "name": f"{project} — Logs",
                "description": f"Volume, erros e breakdown de logs OpenTelemetry do projeto {project}.",
            },
            "variables": [
                dynamic_variable("service_name", "service.name", "Service"),
                dynamic_variable("deployment_environment", "deployment.environment", "Environment"),
            ],
            "panels": {
                volume: timeseries_panel("Log volume", ns_filter(project)),
                errors: timeseries_panel("Error logs", error_filter(project)),
                warns: timeseries_panel("Warning logs", warn_filter(project)),
                error_count: value_panel("Error count", error_filter(project)),
                by_severity: timeseries_panel("Logs by severity", ns_filter(project), group_by="severity_text"),
                by_service: timeseries_panel("Logs by service", ns_filter(project), group_by="service.name"),
            },
            "layouts": [
                grid(
                    "Overview",
                    [
                        item(0, 0, 3, 4, error_count),
                        item(3, 0, 9, 4, volume),
                        item(0, 4, 6, 6, errors),
                        item(6, 4, 6, 6, warns),
                    ],
                ),
                grid(
                    "Breakdown",
                    [
                        item(0, 0, 6, 6, by_severity),
                        item(6, 0, 6, 6, by_service),
                    ],
                ),
            ],
        },
    }


def view_payload(name: str, project: str, filter_expr: str, color: str) -> dict[str, Any]:
    return {
        "name": name,
        "category": project,
        "sourcePage": "logs",
        "tags": [project.lower(), "logs", "ntbot-managed"],
        "extraData": json.dumps({"color": color, "version": 1, "format": "table", "maxLines": 1, "fontSize": "small"}),
        "compositeQuery": {
            "queryType": "builder",
            "panelType": "list",
            "queries": [
                {
                    "type": "builder_query",
                    "spec": {
                        "name": "A",
                        "signal": "logs",
                        "stepInterval": 0,
                        "disabled": False,
                        "limit": 100,
                        "filter": {"expression": filter_expr},
                        "having": {"expression": ""},
                        "order": [order("timestamp"), order("id")],
                    },
                }
            ],
        },
    }


def project_views(project: str) -> list[dict[str, Any]]:
    return [
        view_payload(f"{project} — All logs", project, ns_filter(project), "#3b82f6"),
        view_payload(f"{project} — Errors", project, error_filter(project), "#e5484d"),
        view_payload(f"{project} — Warnings", project, warn_filter(project), "#f5a524"),
    ]


class SignozClient:
    def __init__(self, base_url: str, api_key: str) -> None:
        self.base_url = base_url.rstrip("/")
        self.api_key = api_key

    def request(self, method: str, path: str, body: Any | None = None) -> Any:
        url = f"{self.base_url}{path}"
        data = None if body is None else json.dumps(body).encode()
        req = urllib.request.Request(
            url,
            data=data,
            method=method,
            headers={
                "SIGNOZ-API-KEY": self.api_key,
                "Content-Type": "application/json",
                "Accept": "application/json",
            },
        )
        try:
            with urllib.request.urlopen(req, timeout=30) as resp:
                raw = resp.read().decode()
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode(errors="replace")
            raise RuntimeError(f"{method} {path} -> HTTP {exc.code}: {detail}") from exc

    def list_dashboards(self) -> list[dict[str, Any]]:
        items: list[dict[str, Any]] = []
        offset = 0
        while True:
            query = urllib.parse.urlencode({"limit": 50, "offset": offset})
            payload = self.request("GET", f"/api/v2/dashboards?{query}")
            data = _unwrap(payload)
            page = data if isinstance(data, list) else data.get("dashboards") or data.get("items") or []
            items.extend(page)
            if len(page) < 50:
                break
            offset += 50
        return items

    def list_views(self) -> list[dict[str, Any]]:
        for path in ("/api/v2/saved_views?sourcePage=logs&limit=200", "/api/v1/explorer/views"):
            try:
                payload = self.request("GET", path)
                data = _unwrap(payload)
                if isinstance(data, list):
                    return data
                return data.get("data") or data.get("views") or data.get("items") or []
            except RuntimeError:
                continue
        return []


def _unwrap(payload: Any) -> Any:
    if isinstance(payload, dict) and "data" in payload:
        return payload["data"]
    return payload


def dashboard_name(item: dict[str, Any]) -> str:
    return (
        item.get("spec", {}).get("display", {}).get("name")
        or item.get("data", {}).get("title")
        or item.get("title")
        or item.get("name")
        or ""
    )


def dashboard_id(item: dict[str, Any]) -> str | None:
    return item.get("id") or item.get("uuid") or item.get("data", {}).get("id")


def provision(client: SignozClient, dry_run: bool) -> int:
    existing_dashboards = {dashboard_name(item): item for item in client.list_dashboards()}
    existing_views = {item.get("name"): item for item in client.list_views()}
    created = updated = skipped = 0

    for project in PROJECTS:
        payload = dashboard_payload(project)
        name = payload["spec"]["display"]["name"]
        current = existing_dashboards.get(name)
        print(f"dashboard {name}: {'update ' + dashboard_id(current) if current else 'create'}")
        if dry_run:
            skipped += 1
            continue
        if current and dashboard_id(current):
            body = dict(payload)
            body["name"] = current.get("name") or name
            client.request("PUT", f"/api/v2/dashboards/{dashboard_id(current)}", body)
            updated += 1
        else:
            client.request("POST", "/api/v2/dashboards", payload)
            created += 1

        for view in project_views(project):
            current_view = existing_views.get(view["name"])
            print(f"  view {view['name']}: {'update' if current_view else 'create'}")
            if current_view and current_view.get("id"):
                try:
                    client.request("PUT", f"/api/v2/saved_views/{current_view['id']}", view)
                except RuntimeError:
                    client.request("PUT", f"/api/v1/explorer/views/{current_view['id']}", view)
                updated += 1
            else:
                try:
                    client.request("POST", "/api/v2/saved_views", view)
                except RuntimeError:
                    client.request("POST", "/api/v1/explorer/views", view)
                created += 1

    print(f"done created={created} updated={updated} dry_run_skipped={skipped}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default=os.environ.get("SIGNOZ_URL") or os.environ.get("SIGNOZ_API_URL"))
    parser.add_argument("--api-key", default=os.environ.get("SIGNOZ_API_KEY"))
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--print-payloads", action="store_true")
    args = parser.parse_args()

    if args.print_payloads:
        docs = {
            "dashboards": [dashboard_payload(project) for project in PROJECTS],
            "views": [view for project in PROJECTS for view in project_views(project)],
        }
        json.dump(docs, sys.stdout, indent=2)
        print()
        return 0

    if not args.url or not args.api_key:
        print("SIGNOZ_URL and SIGNOZ_API_KEY are required (or pass --url / --api-key).", file=sys.stderr)
        return 2

    return provision(SignozClient(args.url, args.api_key), args.dry_run)


if __name__ == "__main__":
    sys.exit(main())
