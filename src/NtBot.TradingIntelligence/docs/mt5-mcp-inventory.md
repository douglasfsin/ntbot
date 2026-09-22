# MetaTrader 5 MCP inventory (Cursor)

Probed on 2026-08-02 against local HTTP MCP endpoints provided for NtBot / XAUUSD analysis.

| Server id (Cursor) | URL | Status at probe |
| --- | --- | --- |
| `mt5-metaeditor` | `http://127.0.0.1:22345/mcp` | **Unreachable** (connection refused) |
| `mt5-terminal` | `http://127.0.0.1:22346/mcp` | **Up** — `MetaTrader 5 MCP` v1.0, protocol `2025-06-18` |

Auth: `Authorization: Bearer <token>` (see `.cursor/mcp.json`; example uses `${env:MT5_MCP_BEARER}`).

## Symbol allowlist (agents / MCP)

Runtime MT5 paths (**Connector** `mt5_config.json` → `MT5_SYMBOLS`, **MarketData.API** `MT5:Symbols`, **NtBot.Api** `Quant:Mt5Symbols`) share one allowlist — **not** the full Market Watch:

`XAUUSD`, `EURUSD`, `NZDUSD`, `USDJPY`, `GBPUSD`, `USDBRL`, `USOUSD`, `VIX`, `USDMXN`, `UKOUSD`

Cursor agents calling MCP tools (`get_marketwatch_symbols`, `get_chart_history`, `get_chart_ticks_history`, `add_marketwatch_symbol`, `chart_open`, `trade_*`, etc.) **must pass only allowlisted symbols**. Do not call `get_marketwatch_symbols` without `symbol` (or with wildcards / high `limit`) to dump the entire watch. Prefer explicit `{ "symbol": "XAUUSD" }` (or another allowlisted ticker).

---

## Cursor config

| File | Purpose |
| --- | --- |
| `.cursor/mcp.json` | Project MCP registration (gitignored — may hold Bearer) |
| `.cursor/mcp.json.example` | Commit-safe template with `${env:MT5_MCP_BEARER}` |

Existing global MCP lives in `%USERPROFILE%\.cursor\mcp.json` (n8n, coolify). Project file merges with global; project names take precedence on collision.

**Reload:** Cursor often does not hot-load new project MCP mid-session. After creating/updating `.cursor/mcp.json`, open **Customize → MCP**, refresh/toggle the servers, or restart Cursor. Until then, agents may not see `mt5-*` in `GetMcpTools`.

**Pre-flight (terminal MCP):** Every new MCP session should call `get_workspace_info` first. File tools honor `read_roots` / `write_roots` from that result.

## Probe notes (terminal `:22346`)

- Transport: streamable HTTP JSON-RPC; session via `Mcp-Session-Id` response header.
- Capabilities: `tools` only (`listChanged: false`). `resources/list` and `prompts/list` returned empty arrays.
- Sample XAUUSD: `get_marketwatch_symbols` returned live bid/ask (~4068/4069), contract size 100, Digits 2.
- Sample OHLCV: `get_chart_history` with ISO datetimes (`datetime_from` < `datetime_to`) returned H1 bars with OHLCV + tick_volume + spread. History available from ~2004 for this broker symbol.
- Datetime formats: ISO-8601 preferred for chart/history tools; `datetime_to` must be **greater than** `datetime_from` (exclusive end for some history tools).

## MetaEditor `:22345`

Not reachable during inventory. Config is registered anyway so Cursor can connect when MetaEditor MCP is started. Expect IDE/compiler-oriented tools when up; the **terminal** server already exposes many workspace/file tools plus trading/market tools (see below).

---

## Tool inventory — `mt5-terminal` (40 tools)

### Workspace / files / network

| Tool | Params (required*) | Notes |
| --- | --- | --- |
| `get_workspace_info` | — | MQL5 roots, compiler, permissions, capabilities |
| `get_time_information` | — | UTC, local, trade-server last known time |
| `list_directory` | `path*`, `max_depth`, `show_hidden` | |
| `find_files_by_glob` | `path*`, `pattern*`, `max_results`, `show_hidden` | |
| `find_files_by_name_keyword` | `path*`, `keyword*`, `case_sensitive`, `max_results` | |
| `read_file` | `path*`, `offset`, `length` | |
| `read_binary_file` | `path*`, `offset`, `length` | |
| `read_file_by_lines` | `path*`, `start_line`, `end_line`, `max_lines`, `encoding` | |
| `create_new_folder` | `path*` | |
| `create_new_file` | `path*`, `content`, `encoding`, `overwrite`, `bom` | |
| `write_file` | `path*`, `content*`, `encoding`, `overwrite`, `bom` | |
| `write_binary_file` | `path*`, `data*`, `offset`, `overwrite`, `truncate` | hex body |
| `delete_file` | `path*`, `missing_ok` | file only |
| `replace_text_in_file` | `path*`, `search*`, `replace*`, `start_line`, `end_line`, `all` | |
| `search_text` | `path*`, `query*`, `file_glob*`, `case_sensitive`, `max_results` | |
| `search_regex` | `path*`, `pattern*`, `file_glob*`, `case_sensitive`, `max_results` | |
| `send_web_request` | `method*`, `url*`, `headers`, `timeout`, `body_format`, `body`, `input_filename`, `output_filename` | HTTP only; never trades |

### Charts / indicators / EAs

| Tool | Params (required*) | Notes |
| --- | --- | --- |
| `chart_open` | `symbol*`, `period*` | periods: M1…MN1 |
| `chart_close` | `chart_id*` | |
| `chart_apply_template` | `chart_id*`, `template_filename*` | under `MQL5\Profiles\Templates` |
| `list_open_charts` | — | includes attached indicators/EAs |
| `list_available_indicators` | `filter`, `indicator_type` | built-in + compiled custom |
| `list_available_expert_advisors` | `filter`, `expert_advisor_type` | |

### Market data (XAUUSD-relevant)

| Tool | Params (required*) | XAUUSD usefulness |
| --- | --- | --- |
| `get_marketwatch_symbols` | `symbol`, `include_hidden`, `limit` | Spec, bid/ask, highs/lows, swaps, volumes, contract size |
| `add_marketwatch_symbol` | `symbol*`, `show` | Ensure symbol selected before history |
| `remove_marketwatch_symbol` | `symbol*` | UI only |
| `get_chart_history` | `datetime_from*`, `datetime_to*`, `symbol*`, `period*`, `limit` | **Primary OHLCV** for TI engines |
| `get_chart_ticks_history` | `datetime_from*`, `datetime_to*`, `symbol*`, `limit` | Tick bid/ask for microstructure / spread |
| `get_time_information` | — | Align session/server clocks for gold sessions |

### Account / positions / history (read-only)

| Tool | Params (required*) | Notes |
| --- | --- | --- |
| `get_trading_account_info` | — | Balance, leverage, currency, etc. |
| `get_trading_open_positions` | `include_orders`, `symbol` | Live risk context |
| `get_trading_history_positions` | `datetime_from`, `datetime_to`, `symbol`, `limit` | Aggregated closed positions |
| `get_trading_history_orders` | `datetime_from`, `datetime_to`, `symbol`, `include_orders`, `include_orders_canceled`, `include_deals`, `limit` | Orders + deals |

### Trading (destructive — explicit user instruction only)

| Tool | Params (required*) |
| --- | --- |
| `trade_send_market_order` | `symbol*`, `type*`, `volume*`, `sl`, `tp`, `comment` |
| `trade_send_pending_order` | `symbol*`, `type*`, `volume*`, `price*`, `stoplimit`, `sl`, `tp`, `filling_type`, `expiration_type`, `expiration_time`, `comment` |
| `trade_modify_sl_tp` | `symbol*`, `position_ticket` xor `order_ticket`, `sl`, `tp` |
| `trade_delete_order` | `symbol*`, `order_ticket*` |
| `trade_close_single_position` | `symbol*`, `position_ticket*` |
| `trade_close_by_position` | `symbol*`, `position_ticket*`, `position_ticket_by*` |

### Strategy tester

| Tool | Params (required*) |
| --- | --- |
| `tester_run_backtest` | `config_path*`, `inputs_path` |
| `tester_get_status` | `run_id*` |

---

## Sketch: wiring into NtBot TradingIntelligence

Today TI / market candles prefer the **Python MT5 host** at `Quant:Mt5ApiUrl` → `http://localhost:8228` (`GET /api/ohlcv/{symbol}?timeframe=&count=`), started by `NtBot.Connector.Windows` (`Mt5PythonHost` / `Mt5Port` 8228).

| Path | Role | Fit for TI |
| --- | --- | --- |
| Python host `:8228` | Stable REST for OHLCV used by `MarketCandleService` | Keep as **production/default** candle source |
| MCP terminal `:22346` | Richer local agent surface (ticks, Market Watch specs, charts, tester, optional trade) | Use for **dev agents**, richer XAUUSD research, and optional side-channel enrichment — not a drop-in replace for `:8228` without a client |
| MCP metaeditor `:22345` | IDE/compile (when running) | EA/script authoring; not candle pipeline |

**Suggested next steps (no large UI rewrite):**

1. Keep `:8228` as the canonical OHLCV feed for engines/UI.
2. Optionally add a thin `IMt5McpClient` (HTTP JSON-RPC + session header) behind a feature flag for:
   - `get_chart_ticks_history` / live `get_marketwatch_symbols` enrichment on XAUUSD snapshots
   - agent/mentor tooling that already runs in Cursor via MCP
3. Do **not** call `trade_*` from TI automation unless product explicitly opts in with hard safety gates.
4. Start MetaEditor MCP (22345) when compile/file workflows are needed; re-probe and append its tool list to this doc.

### XAUUSD analysis cheat-sheet (terminal MCP)

```text
get_workspace_info
add_marketwatch_symbol { symbol: "XAUUSD" }   # if missing
get_marketwatch_symbols { symbol: "XAUUSD" }
get_chart_history {
  symbol: "XAUUSD", period: "H1"|"M15"|"D1",
  datetime_from: "2026-07-01T00:00:00Z",
  datetime_to:   "2026-08-03T00:00:00Z",
  limit: 500
}
get_chart_ticks_history { symbol: "XAUUSD", datetime_from, datetime_to, limit }
get_time_information
```
