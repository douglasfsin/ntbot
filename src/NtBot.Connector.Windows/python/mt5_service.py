"""
Serviço de integração com MetaTrader5
"""

import os
import MetaTrader5 as mt5
import pandas as pd
from datetime import datetime, timezone
from typing import Optional
import logging

from config import MT5_CONFIG, SYMBOLS, SYMBOL_ALIASES, STREAM_CONFIG, HISTORY_CONFIG

logger = logging.getLogger(__name__)

_MT5_ERROR_HINTS = {
    -1: "Falha genérica. Verifique se o terminal MT5 está aberto.",
    -2: "Parâmetros inválidos em initialize(). Revise MT5_PATH, login e servidor.",
    -5: "Versão incompatível entre o pacote Python MetaTrader5 e o terminal. Atualize ambos.",
    -6: (
        "Autorização falhou. Abra o MT5, faça login manualmente e use o terminal64.exe "
        "da MESMA instalação em MT5_PATH. Se usar credenciais, confira login/senha/servidor."
    ),
    -8: (
        "Negociação algorítmica desabilitada. No MT5: Ferramentas → Opções → "
        "Expert Advisors → marque 'Permitir negociação algorítmica'."
    ),
    -10003: "IPC não inicializado — o terminal MT5 não está em execução ou o caminho está errado.",
    -10005: "Timeout ao conectar no terminal MT5.",
}


def _discover_terminal_paths() -> list[str]:
    """Localiza instalações do terminal64.exe (corretoras usam pastas diferentes)."""
    paths: list[str] = []

    configured = (MT5_CONFIG.get("path") or os.getenv("MT5_PATH", "")).strip()
    if configured:
        paths.append(configured)

    scan_roots: list[str] = []
    for key in ("ProgramFiles", "ProgramFiles(x86)"):
        value = os.environ.get(key)
        if value and os.path.isdir(value):
            scan_roots.append(value)

    for root in scan_roots:
        try:
            for entry in os.scandir(root):
                if not entry.is_dir():
                    continue
                if "metatrader" not in entry.name.lower():
                    continue
                candidate = os.path.join(entry.path, "terminal64.exe")
                if os.path.isfile(candidate):
                    paths.append(candidate)
        except OSError:
            continue

    for default in (
        r"C:\Program Files\MetaTrader 5\terminal64.exe",
        r"C:\Program Files (x86)\MetaTrader 5\terminal64.exe",
    ):
        if os.path.isfile(default):
            paths.append(default)

    unique: list[str] = []
    seen: set[str] = set()
    for path in paths:
        normalized = os.path.normcase(os.path.abspath(path))
        if normalized in seen:
            continue
        seen.add(normalized)
        unique.append(path)
    return unique


def _format_mt5_error(error: tuple) -> str:
    code, message = error
    hint = _MT5_ERROR_HINTS.get(code, "")
    if hint:
        return f"({code}, '{message}') — {hint}"
    return f"({code}, '{message}')"


class MT5Service:
    _instance = None
    _initialized = False
    _terminal_path: Optional[str] = None
    _resolve_cache: dict[str, str] = {}

    def __new__(cls):
        if cls._instance is None:
            cls._instance = super().__new__(cls)
            cls._instance._resolve_cache = {}
        return cls._instance

    def _ensure_visible(self, mt5_symbol: str) -> None:
        info = mt5.symbol_info(mt5_symbol)
        if info is not None and not info.visible:
            mt5.symbol_select(mt5_symbol, True)

    def resolve_symbol(self, logical: str) -> Optional[str]:
        """Resolve nome lógico (config) para o símbolo exato no MT5 da corretora."""
        logical = logical.upper().strip()
        if not logical:
            return None

        cached = self._resolve_cache.get(logical)
        if cached and mt5.symbol_info(cached) is not None:
            return cached

        if not self._initialized and not self.initialize():
            return None

        alias = SYMBOL_ALIASES.get(logical)
        if alias:
            info = mt5.symbol_info(alias)
            if info is not None:
                self._ensure_visible(alias)
                self._resolve_cache[logical] = alias
                return alias

        direct = mt5.symbol_info(logical)
        if direct is not None:
            self._ensure_visible(logical)
            self._resolve_cache[logical] = logical
            return logical

        suffixes = (".m", "m", ".", ".pro", "pro", "#", ".i", "i", ".c", "c", ".raw", "raw", ".a", "a", ".e", "e")
        for suffix in suffixes:
            candidate = f"{logical}{suffix}"
            info = mt5.symbol_info(candidate)
            if info is not None:
                self._ensure_visible(candidate)
                self._resolve_cache[logical] = candidate
                logger.info("Símbolo %s resolvido para %s no MT5", logical, candidate)
                return candidate

        for sym in mt5.symbols_get() or []:
            name = sym.name
            upper = name.upper()
            if upper == logical:
                self._ensure_visible(name)
                self._resolve_cache[logical] = name
                return name
            if upper.startswith(logical) and len(upper) <= len(logical) + 5:
                self._ensure_visible(name)
                self._resolve_cache[logical] = name
                logger.info("Símbolo %s resolvido para %s no MT5 (busca)", logical, name)
                return name

        logger.warning("Símbolo %s não encontrado no MT5", logical)
        self._resolve_cache.pop(logical, None)
        return None

    def _try_initialize(self, path: Optional[str], use_credentials: bool) -> bool:
        cfg = MT5_CONFIG
        kwargs: dict = {"timeout": cfg.get("timeout", 60000)}

        if path:
            kwargs["path"] = path

        if use_credentials:
            kwargs["login"] = int(cfg["login"])
            kwargs["password"] = cfg["password"]
            kwargs["server"] = cfg["server"]

        return bool(mt5.initialize(**kwargs))

    def initialize(self) -> bool:
        """Inicializa conexão com o MetaTrader5."""
        if self._initialized:
            return True

        cfg = MT5_CONFIG
        login = int(cfg.get("login") or 0)
        password = (cfg.get("password") or "").strip()
        server = (cfg.get("server") or "").strip()
        use_credentials = login > 0 and bool(password) and bool(server)

        if login > 0 and not use_credentials:
            logger.warning(
                "MT5_LOGIN definido, mas MT5_PASSWORD ou MT5_SERVER estão vazios. "
                "Usando sessão já aberta no terminal."
            )

        candidates = _discover_terminal_paths()
        attempts: list[tuple[Optional[str], tuple]] = []

        # 1) Tenta cada terminal encontrado anexando à sessão logada
        for path in candidates:
            mt5.shutdown()
            if self._try_initialize(path, use_credentials=False):
                self._terminal_path = path
                return self._mark_connected(path, "sessão do terminal")

            attempts.append((path, mt5.last_error()))
            mt5.shutdown()

        # 2) Com credenciais explícitas (conta/servidor)
        if use_credentials:
            for path in candidates or [None]:
                if self._try_initialize(path, use_credentials=True):
                    self._terminal_path = path
                    return self._mark_connected(path, f"login {login} @ {server}")

                attempts.append((path, mt5.last_error()))
                mt5.shutdown()

        # 3) Última tentativa: initialize padrão sem path
        if self._try_initialize(None, use_credentials=False):
            self._terminal_path = None
            return self._mark_connected(None, "initialize padrão")

        attempts.append((None, mt5.last_error()))
        mt5.shutdown()

        logger.error("Falha ao inicializar MT5 após %d tentativa(s).", len(attempts))
        for path, error in attempts:
            label = path or "sem path explícito"
            logger.error("  • %s → %s", label, _format_mt5_error(error))

        if not candidates:
            logger.error(
                "Nenhum terminal64.exe encontrado. Defina MT5_PATH apontando para o "
                "terminal da sua corretora (ex.: C:\\Program Files\\MetaTrader 5\\terminal64.exe)."
            )
        elif not use_credentials:
            logger.error(
                "Dica: abra o MetaTrader 5, faça login na conta e confirme que "
                "'Permitir negociação algorítmica' está ativo em Ferramentas → Opções → Expert Advisors."
            )
        else:
            logger.error(
                "Dica: confira login, senha master e nome exato do servidor no terminal MT5."
            )

        return False

    def _mark_connected(self, path: Optional[str], mode: str) -> bool:
        info = mt5.terminal_info()
        acc = mt5.account_info()
        if info is None:
            logger.error("MT5 initialize retornou True, mas terminal_info() veio vazio.")
            mt5.shutdown()
            return False

        logger.info(
            "MT5 conectado via %s | Terminal: %s | Build: %s | Path: %s",
            mode,
            info.name,
            info.build,
            path or "(auto)",
        )
        if acc:
            logger.info("Conta ativa: %s @ %s", acc.login, acc.server)

        self._initialized = True
        return True

    def shutdown(self):
        """Encerra conexão com o MetaTrader5."""
        if self._initialized:
            mt5.shutdown()
            self._initialized = False
            self._terminal_path = None
            logger.info("MT5 desconectado.")

    def is_connected(self) -> bool:
        if not self._initialized:
            return False
        try:
            return mt5.terminal_info() is not None
        except Exception:
            return False

    def validate_symbol(self, symbol: str) -> bool:
        """Valida se o símbolo está na config e existe no MT5."""
        symbol = symbol.upper()
        if symbol not in SYMBOLS:
            return False
        return self.resolve_symbol(symbol) is not None

    # -------------------------------------------------------------------------
    # TICK (preço em tempo real)
    # -------------------------------------------------------------------------
    def get_tick(self, symbol: str) -> Optional[dict]:
        """Retorna o tick mais recente do símbolo."""
        logical = symbol.upper()
        if logical not in SYMBOLS:
            return None

        resolved = self.resolve_symbol(logical)
        if not resolved:
            return None

        tick = mt5.symbol_info_tick(resolved)
        if tick is None:
            return None

        info = mt5.symbol_info(resolved)
        digits = info.digits if info else 5

        return {
            "symbol": logical,
            "mt5_symbol": resolved,
            "time": datetime.fromtimestamp(tick.time, tz=timezone.utc).isoformat(),
            "time_msc": tick.time_msc,
            "bid": tick.bid,
            "ask": tick.ask,
            "last": tick.last,
            "spread": round((tick.ask - tick.bid) * 10 ** digits, 1),
            "volume": tick.volume,
            "volume_real": tick.volume_real,
            "flags": tick.flags,
        }

    # -------------------------------------------------------------------------
    # SYMBOL INFO (informações completas do símbolo)
    # -------------------------------------------------------------------------
    def get_symbol_info(self, symbol: str) -> Optional[dict]:
        """Retorna informações detalhadas do símbolo."""
        logical = symbol.upper()
        if logical not in SYMBOLS:
            return None

        resolved = self.resolve_symbol(logical)
        if not resolved:
            return None

        info = mt5.symbol_info(resolved)
        if info is None:
            return None

        return {
            "symbol": logical,
            "mt5_symbol": resolved,
            "description": info.description,
            "currency_base": info.currency_base,
            "currency_profit": info.currency_profit,
            "digits": info.digits,
            "point": info.point,
            "tick_size": info.trade_tick_size,
            "tick_value": info.trade_tick_value,
            "contract_size": info.trade_contract_size,
            "volume_min": info.volume_min,
            "volume_max": info.volume_max,
            "volume_step": info.volume_step,
            "spread": info.spread,
            "spread_float": bool(info.spread_float),
            "session_open": info.session_open,
            "session_close": info.session_close,
            "bid": info.bid,
            "ask": info.ask,
            "last": info.last,
            "volume": info.volume,
            "volumehigh": info.volumehigh,
            "volumelow": info.volumelow,
        }

    # -------------------------------------------------------------------------
    # BOOK (profundidade de mercado / DOM)
    # -------------------------------------------------------------------------
    def get_book(self, symbol: str) -> Optional[dict]:
        """Retorna o book de ordens (profundidade de mercado)."""
        symbol = symbol.upper()
        if not self.validate_symbol(symbol):
            return None

        if not mt5.market_book_add(symbol):
            logger.warning(f"Não foi possível habilitar book para {symbol}: {mt5.last_error()}")
            return None

        book = mt5.market_book_get(symbol)
        if book is None:
            return None

        bids = []
        asks = []
        for entry in book:
            item = {
                "price": entry.price,
                "volume": entry.volume,
                "volume_dbl": entry.volume_dbl,
            }
            if entry.type == mt5.BOOK_TYPE_SELL:
                asks.append(item)
            elif entry.type == mt5.BOOK_TYPE_BUY:
                bids.append(item)

        depth = STREAM_CONFIG["book_depth"]
        return {
            "symbol": symbol,
            "time": datetime.now(tz=timezone.utc).isoformat(),
            "bids": sorted(bids, key=lambda x: x["price"], reverse=True)[:depth],
            "asks": sorted(asks, key=lambda x: x["price"])[:depth],
            "total_bid_volume": round(sum(b["volume"] for b in bids), 2),
            "total_ask_volume": round(sum(a["volume"] for a in asks), 2),
        }

    def release_book(self, symbol: str):
        """Libera o book de ordens do símbolo."""
        mt5.market_book_release(symbol.upper())

    # -------------------------------------------------------------------------
    # OHLCV (dados históricos)
    # -------------------------------------------------------------------------
    _TIMEFRAMES = {
        "M1":  mt5.TIMEFRAME_M1,
        "M5":  mt5.TIMEFRAME_M5,
        "M15": mt5.TIMEFRAME_M15,
        "M30": mt5.TIMEFRAME_M30,
        "H1":  mt5.TIMEFRAME_H1,
        "H4":  mt5.TIMEFRAME_H4,
        "D1":  mt5.TIMEFRAME_D1,
        "W1":  mt5.TIMEFRAME_W1,
        "MN1": mt5.TIMEFRAME_MN1,
    }

    def get_ohlcv(self, symbol: str, timeframe: str = "M1", count: int = None) -> Optional[dict]:
        """Retorna dados OHLCV históricos."""
        logical = symbol.upper()
        if not self._initialized and not self.initialize():
            return None

        resolved = self.resolve_symbol(logical)
        if not resolved:
            logger.warning("Símbolo %s não encontrado no MT5", logical)
            return None

        tf = self._TIMEFRAMES.get(timeframe.upper())
        if tf is None:
            return None

        if count is None:
            count = HISTORY_CONFIG["default_bars"]
        count = min(count, HISTORY_CONFIG["max_bars"])

        rates = mt5.copy_rates_from_pos(resolved, tf, 0, count)
        if rates is None or len(rates) == 0:
            return None

        df = pd.DataFrame(rates)
        df["time"] = pd.to_datetime(df["time"], unit="s", utc=True)
        df["time"] = df["time"].dt.strftime("%Y-%m-%dT%H:%M:%SZ")

        candles = df[["time", "open", "high", "low", "close", "tick_volume", "real_volume", "spread"]].to_dict(orient="records")

        return {
            "symbol": logical,
            "mt5_symbol": resolved,
            "timeframe": timeframe.upper(),
            "count": len(candles),
            "candles": candles,
        }

    # -------------------------------------------------------------------------
    # VOLUME em tempo real (tick volume acumulado da vela atual)
    # -------------------------------------------------------------------------
    def get_volume(self, symbol: str) -> Optional[dict]:
        """Retorna volume atual (vela M1 corrente) e informações de volume do dia."""
        symbol = symbol.upper()
        if not self.validate_symbol(symbol):
            return None

        rates = mt5.copy_rates_from_pos(symbol, mt5.TIMEFRAME_M1, 0, 1)
        info = mt5.symbol_info(symbol)
        tick = mt5.symbol_info_tick(symbol)

        if rates is None or info is None or tick is None:
            return None

        current = rates[0]
        return {
            "symbol": symbol,
            "time": datetime.now(tz=timezone.utc).isoformat(),
            "tick_volume_current_bar": int(current["tick_volume"]),
            "real_volume_current_bar": float(current["real_volume"]),
            "volume_today": int(info.volume),
            "volume_high_today": int(info.volumehigh),
            "volume_low_today": int(info.volumelow),
            "last_tick_volume": float(tick.volume_real),
        }

    # -------------------------------------------------------------------------
    # VARIAÇÃO intraday (referência: fechamento D-1)
    # -------------------------------------------------------------------------
    def get_variation(self, symbol: str) -> Optional[dict]:
        symbol = symbol.upper()
        if not self.validate_symbol(symbol):
            return None

        tick = self.get_tick(symbol)
        if tick is None:
            return None

        rates = mt5.copy_rates_from_pos(symbol, mt5.TIMEFRAME_D1, 0, 2)
        prev_close = None
        if rates is not None and len(rates) >= 2:
            prev_close = float(rates[0]["close"])

        last = float(tick.get("last") or 0)
        if last <= 0:
            last = (float(tick["bid"]) + float(tick["ask"])) / 2.0

        variation_pct = None
        if prev_close and prev_close > 0 and last > 0:
            variation_pct = round((last - prev_close) / prev_close * 100, 4)

        return {
            "symbol": symbol,
            "time": tick["time"],
            "time_msc": tick["time_msc"],
            "last": last,
            "previous_close": prev_close,
            "variation_pct": variation_pct,
        }

    # -------------------------------------------------------------------------
    # CALENDÁRIO ECONÔMICO
    # -------------------------------------------------------------------------
    def get_economic_calendar(self, days_back: int = 1, days_ahead: int = 14) -> list[dict]:
        """Retorna eventos do calendário econômico do MetaTrader5."""
        from datetime import timedelta

        if not self._initialized and not self.initialize():
            return []

        date_from = datetime.now(tz=timezone.utc) - timedelta(days=days_back)
        date_to = datetime.now(tz=timezone.utc) + timedelta(days=days_ahead)

        events = mt5.calendar_get(date_from, date_to)
        if events is None:
            logger.warning("calendar_get retornou vazio: %s", mt5.last_error())
            return []

        result = []
        for event in events:
            importance = int(getattr(event, "importance", 0) or 0)
            impact = "HIGH" if importance >= 2 else "MEDIUM" if importance == 1 else "LOW"
            event_time = datetime.fromtimestamp(int(event.time), tz=timezone.utc)

            result.append({
                "event_name": str(getattr(event, "name", "") or ""),
                "country": str(getattr(event, "country", "") or ""),
                "currency": str(getattr(event, "currency", "") or ""),
                "impact": impact,
                "event_time": event_time.isoformat(),
                "actual": _format_calendar_value(getattr(event, "actual_value", None)),
                "forecast": _format_calendar_value(getattr(event, "forecast_value", None)),
                "previous": _format_calendar_value(getattr(event, "previous_value", None)),
            })

        result.sort(key=lambda item: item["event_time"])
        return result

    # -------------------------------------------------------------------------
    # STATUS
    # -------------------------------------------------------------------------
    def get_status(self) -> dict:
        """Retorna status geral da conexão."""
        if not self._initialized:
            return {"connected": False}

        term = mt5.terminal_info()
        acc = mt5.account_info()

        return {
            "connected": True,
            "terminal_path": self._terminal_path,
            "terminal": {
                "name": term.name if term else None,
                "build": term.build if term else None,
                "connected": bool(term.connected) if term else False,
            },
            "account": {
                "login": acc.login if acc else None,
                "server": acc.server if acc else None,
                "currency": acc.currency if acc else None,
                "balance": acc.balance if acc else None,
            } if acc else None,
            "available_symbols": SYMBOLS,
            "resolved_symbols": {
                s: self.resolve_symbol(s) for s in SYMBOLS
            },
        }


def _format_calendar_value(value) -> str | None:
    if value is None:
        return None
    try:
        if isinstance(value, float) and value != value:
            return None
    except TypeError:
        pass
    return str(value)
