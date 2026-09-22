"""Diagnóstico rápido da ponte MT5: conexão, conta, símbolo e permissão de trade."""
import json
import sys

import MetaTrader5 as mt5

SYMBOL = sys.argv[1] if len(sys.argv) > 1 else "XAUUSD"

report = {}

if not mt5.initialize():
    print(json.dumps({"initialize": False, "last_error": mt5.last_error()}, default=str, indent=2))
    sys.exit(1)

report["initialize"] = True

terminal = mt5.terminal_info()
report["terminal"] = {
    "connected": getattr(terminal, "connected", None),
    "trade_allowed": getattr(terminal, "trade_allowed", None),
    "build": getattr(terminal, "build", None),
    "path": getattr(terminal, "path", None),
} if terminal else None

account = mt5.account_info()
report["account"] = {
    "login": getattr(account, "login", None),
    "server": getattr(account, "server", None),
    "balance": getattr(account, "balance", None),
    "equity": getattr(account, "equity", None),
    "trade_allowed": getattr(account, "trade_allowed", None),
    "trade_expert": getattr(account, "trade_expert", None),
    "margin_free": getattr(account, "margin_free", None),
} if account else None

info = mt5.symbol_info(SYMBOL)
if info is None:
    mt5.symbol_select(SYMBOL, True)
    info = mt5.symbol_info(SYMBOL)

report["symbol"] = {
    "name": SYMBOL,
    "found": info is not None,
    "visible": getattr(info, "visible", None),
    "trade_mode": getattr(info, "trade_mode", None),
    "filling_mode": getattr(info, "filling_mode", None),
    "volume_min": getattr(info, "volume_min", None),
    "volume_step": getattr(info, "volume_step", None),
    "stops_level": getattr(info, "trade_stops_level", None),
} if info else {"name": SYMBOL, "found": False}

tick = mt5.symbol_info_tick(SYMBOL)
report["tick"] = {
    "bid": getattr(tick, "bid", None),
    "ask": getattr(tick, "ask", None),
    "last": getattr(tick, "last", None),
} if tick else None

report["last_error"] = mt5.last_error()

print(json.dumps(report, default=str, indent=2))
mt5.shutdown()
