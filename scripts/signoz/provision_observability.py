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


def v1_filter_items(project: str, extra: list[dict[str, Any]] | None = None) -> dict[str, Any]:
    items = [
        {
            "id": "ns",
            "key": {
                "key": "service.namespace",
                "dataType": "string",
                "type": "resource",
                "isColumn": False,
            },
            "op": "=",
            "value": project,
        }
    ]
    if extra:
        items.extend(extra)
    return {"op": "AND", "items": items}


def v1_severity_item(values: list[str]) -> dict[str, Any]:
    return {
        "id": "sev",
        "key": {
            "key": "severity_text",
            "dataType": "string",
            "type": "",
            "isColumn": True,
        },
        "op": "in",
        "value": values,
    }


def v1_builder_query(project: str, *, operator: str = "count", extra_filters: list[dict[str, Any]] | None = None) -> dict[str, Any]:
    return {
        "dataSource": "logs",
        "queryName": "A",
        "aggregateOperator": operator,
        "aggregateAttribute": {"key": "", "dataType": "", "type": ""},
        "expression": "A",
        "disabled": False,
        "legend": "count" if operator == "count" else "",
        "stepInterval": 60 if operator == "count" else None,
        "filters": v1_filter_items(project, extra_filters),
    }


def v1_dashboard_payload(project: str) -> dict[str, Any]:
    widgets = []
    layout = []
    specs = [
        ("log-volume", "Log volume", "count", None, 0, 0, 12, 4),
        ("error-volume", "Error logs", "count", [v1_severity_item(["ERROR", "FATAL", "Fatal"])], 0, 4, 6, 6),
        ("warn-volume", "Warning logs", "count", [v1_severity_item(["WARN", "WARNING", "Warning"])], 6, 4, 6, 6),
    ]
    for key, title, operator, extra, x, y, w, h in specs:
        widget_id = panel_id(project, f"v1-{key}")
        widgets.append(
            {
                "id": widget_id,
                "title": title,
                "description": f"{project} / service.namespace",
                "panelTypes": "graph",
                "timePreferance": "GLOBAL_TIME",
                "nullZeroValues": "zero",
                "opacity": "1",
                "softMin": 0,
                "softMax": 0,
                "selectedLogFields": [],
                "selectedTracesFields": [],
                "query": {
                    "queryType": "builder",
                    "promQL": [],
                    "clickhouse_sql": [],
                    "builder": {
                        "queryData": [v1_builder_query(project, operator=operator, extra_filters=extra)],
                        "queryFormulas": [],
                    },
                },
            }
        )
        layout.append({"i": widget_id, "x": x, "y": y, "w": w, "h": h, "moved": False, "static": False})
    return {
        "title": f"{project} — Logs",
        "description": f"Volume, erros e warnings de logs OpenTelemetry do projeto {project}.",
        "tags": [project, "logs", "ntbot-managed"],
        "layout": layout,
        "widgets": widgets,
        "variables": {},
        "version": "v3",
    }


def v1_view_payload(name: str, project: str, extra: list[dict[str, Any]] | None, color: str) -> dict[str, Any]:
    query = v1_builder_query(project, operator="noop", extra_filters=extra)
    query["stepInterval"] = None
    return {
        "name": name,
        "category": project,
        "sourcePage": "logs",
        "tags": [project.lower(), "logs", "ntbot-managed"],
        "extraData": json.dumps({"color": color, "version": 1}),
        "compositeQuery": {
            "queryType": "builder",
            "panelType": "list",
            "builderQueries": {"A": query},
        },
    }


def v1_project_views(project: str) -> list[dict[str, Any]]:
    return [
        v1_view_payload(f"{project} — All logs", project, None, "#3b82f6"),
        v1_view_payload(
            f"{project} — Errors",
            project,
            [v1_severity_item(["ERROR", "FATAL", "Fatal"])],
            "#e5484d",
        ),
        v1_view_payload(
            f"{project} — Warnings",
            project,
            [v1_severity_item(["WARN", "WARNING", "Warning"])],
            "#f5a524",
        ),
    ]


def parse_version(raw: Any) -> tuple[int, int]:
    text = ""
    if isinstance(raw, dict):
        text = str(raw.get("version") or raw.get("data") or "")
    else:
        text = str(raw or "")
    digits = [int(part) for part in text.replace("v", "").split(".") if part.isdigit()]
    major = digits[0] if digits else 0
    minor = digits[1] if len(digits) > 1 else 0
    return major, minor


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
                ctype = resp.headers.get("Content-Type", "")
                if not raw:
                    return None
                if "html" in ctype.lower() or raw.lstrip().startswith("<!"):
                    raise RuntimeError(f"{method} {path} -> HTTP {resp.status} HTML (endpoint not on this SigNoz version)")
                try:
                    return json.loads(raw)
                except json.JSONDecodeError as exc:
                    raise RuntimeError(f"{method} {path} -> invalid JSON: {raw[:200]}") from exc
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode(errors="replace")
            raise RuntimeError(f"{method} {path} -> HTTP {exc.code}: {detail}") from exc

    def version(self) -> Any:
        return self.request("GET", "/api/v1/version")

    def list_dashboards(self) -> list[dict[str, Any]]:
        items: list[dict[str, Any]] = []
        for path in ("/api/v1/dashboards", "/api/v2/dashboards?limit=50"):
            try:
                payload = self.request("GET", path)
            except RuntimeError:
                continue
            data = _unwrap(payload)
            if data is None:
                continue
            page = data if isinstance(data, list) else data.get("dashboards") or data.get("items") or []
            items.extend(page)
            if items:
                break
        return items

    def list_views(self) -> list[dict[str, Any]]:
        for path in ("/api/v1/explorer/views?sourcePage=logs", "/api/v1/explorer/views", "/api/v2/saved_views?sourcePage=logs&limit=200"):
            try:
                payload = self.request("GET", path)
            except RuntimeError:
                continue
            data = _unwrap(payload)
            if data is None:
                return []
            if isinstance(data, list):
                return data
            if isinstance(data, dict):
                return data.get("views") or data.get("items") or []
        return []


def _unwrap(payload: Any) -> Any:
    if isinstance(payload, dict) and "data" in payload:
        return payload["data"]
    return payload


def dashboard_name(item: dict[str, Any]) -> str:
    nested = item.get("data") if isinstance(item.get("data"), dict) else {}
    spec = item.get("spec") if isinstance(item.get("spec"), dict) else {}
    display = spec.get("display") if isinstance(spec.get("display"), dict) else {}
    return (
        display.get("name")
        or nested.get("title")
        or item.get("title")
        or item.get("name")
        or ""
    )


def dashboard_id(item: dict[str, Any]) -> str | None:
    nested = item.get("data") if isinstance(item.get("data"), dict) else {}
    return item.get("id") or item.get("uuid") or nested.get("id") or nested.get("uuid")


def provision(client: SignozClient, dry_run: bool) -> int:
    version_payload = client.version()
    major, minor = parse_version(version_payload)
    legacy = major == 0 and minor < 135
    print(f"signoz version={version_payload} legacy_v1={legacy}")

    existing_dashboards = {dashboard_name(item): item for item in client.list_dashboards()}
    existing_views = {item.get("name"): item for item in client.list_views()}
    created = updated = skipped = 0

    for project in PROJECTS:
        payload = v1_dashboard_payload(project) if legacy else dashboard_payload(project)
        name = payload.get("title") or payload.get("spec", {}).get("display", {}).get("name")
        current = existing_dashboards.get(name)
        print(f"dashboard {name}: {'update ' + str(dashboard_id(current)) if current else 'create'}")
        if dry_run:
            skipped += 1
            continue
        if legacy:
            if current and dashboard_id(current):
                client.request("PUT", f"/api/v1/dashboards/{dashboard_id(current)}", payload)
                updated += 1
            else:
                client.request("POST", "/api/v1/dashboards", payload)
                created += 1
        elif current and dashboard_id(current):
            body = dict(payload)
            body["name"] = current.get("name") or name
            client.request("PUT", f"/api/v2/dashboards/{dashboard_id(current)}", body)
            updated += 1
        else:
            client.request("POST", "/api/v2/dashboards", payload)
            created += 1

        views = v1_project_views(project) if legacy else project_views(project)
        for view in views:
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
    parser.add_argument(
        "--url",
        default=os.environ.get("SIGNOZ_URL")
        or os.environ.get("SIGNOZ_API_URL")
        or "http://signoz-eva3s2kbg9a48onb3ws2hvgd.46.225.161.55.sslip.io",
    )
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
