"""Envia order_send cru para descobrir o retcode real do broker (sem guards da aplicação)."""
import json
import sys

import MetaTrader5 as mt5

symbol = sys.argv[1] if len(sys.argv) > 1 else "XAUUSD"
volume = float(sys.argv[2]) if len(sys.argv) > 2 else 0.01

if not mt5.initialize():
    print("initialize falhou:", mt5.last_error())
    raise SystemExit(1)

mt5.symbol_select(symbol, True)
info = mt5.symbol_info(symbol)
tick = mt5.symbol_info_tick(symbol)

req = {
    "action": mt5.TRADE_ACTION_DEAL,
    "symbol": symbol,
    "volume": volume,
    "type": mt5.ORDER_TYPE_BUY,
    "price": tick.ask,
    "deviation": 30,
    "magic": 260805,
    "comment": "NTBot-raw",
    "type_time": mt5.ORDER_TIME_GTC,
    "type_filling": mt5.ORDER_FILLING_IOC,
}

check = mt5.order_check(req)
print("ORDER_CHECK:", json.dumps(check._asdict() if check else None, default=str, indent=2))

result = mt5.order_send(req)
print("ORDER_SEND:", json.dumps(result._asdict() if result else None, default=str, indent=2))
print("LAST_ERROR:", mt5.last_error())
print("TERMINAL trade_allowed:", mt5.terminal_info().trade_allowed)

mt5.shutdown()
