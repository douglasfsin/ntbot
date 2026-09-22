"""Envia uma ordem de teste usando o mesmo MT5Service da ponte, e mostra o resultado bruto."""
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src", "NtBot.Connector.Windows", "python"))

from mt5_service import MT5Service  # noqa: E402

symbol = sys.argv[1] if len(sys.argv) > 1 else "XAUUSD"
side = sys.argv[2] if len(sys.argv) > 2 else "buy"
volume = float(sys.argv[3]) if len(sys.argv) > 3 else 0.01

svc = MT5Service()
if not svc.initialize():
    print("Falha ao inicializar MT5")
    raise SystemExit(1)

result = svc.send_market_order(symbol=symbol, side=side, volume=volume, comment="NTBot-selftest")
print("ORDER:", json.dumps(result, default=str, indent=2))

print("POSITIONS:", json.dumps(svc.get_positions(symbol), default=str, indent=2))
