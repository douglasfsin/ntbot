"""
Configurações MT5 — NTBot Connector Windows.
Variáveis de ambiente (definidas pelo host C#) ou defaults locais.
"""

import os

_raw_symbols = os.getenv("MT5_SYMBOLS", "XAUUSD,EURUSD,NZDUSD").strip()
SYMBOLS = [s.strip().upper() for s in _raw_symbols.split(",") if s.strip()]

MT5_CONFIG = {
    "path": os.getenv("MT5_PATH", "").strip(),
    "login": int(os.getenv("MT5_LOGIN", "0") or "0"),
    "password": os.getenv("MT5_PASSWORD", "").strip(),
    "server": os.getenv("MT5_SERVER", "").strip(),
    "timeout": int(os.getenv("MT5_TIMEOUT", "60000")),
}

FLASK_CONFIG = {
    "host": os.getenv("FLASK_HOST", "127.0.0.1"),
    "port": int(os.getenv("FLASK_PORT", "8228")),
    "debug": os.getenv("FLASK_DEBUG", "false").lower() in ("1", "true", "yes"),
}

STREAM_CONFIG = {
    "tick_interval": float(os.getenv("MT5_TICK_INTERVAL", "0.5")),
    "book_depth": int(os.getenv("MT5_BOOK_DEPTH", "10")),
}

HISTORY_CONFIG = {
    "default_bars": 100,
    "max_bars": 5000,
}
