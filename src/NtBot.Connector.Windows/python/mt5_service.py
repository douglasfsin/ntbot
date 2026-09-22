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


_RETCODE_OK = (
    mt5.TRADE_RETCODE_DONE,
    mt5.TRADE_RETCODE_DONE_PARTIAL,
    mt5.TRADE_RETCODE_PLACED,
)


def _filling_candidates(info) -> list[int]:
    """Ordena os modos de preenchimento aceitos pelo símbolo, do mais provável ao fallback."""
    modes: list[int] = []
    flags = getattr(info, "filling_mode", 0) or 0
    if flags & 2:
        modes.append(mt5.ORDER_FILLING_IOC)
    if flags & 1:
        modes.append(mt5.ORDER_FILLING_FOK)
    for fallback in (mt5.ORDER_FILLING_IOC, mt5.ORDER_FILLING_FOK, mt5.ORDER_FILLING_RETURN):
        if fallback not in modes:
            modes.append(fallback)
    return modes


def _normalize_volume(info, volume: float) -> float:
    """Ajusta o volume ao passo/mínimo/máximo do símbolo — evita retcode 10014."""
    try:
        volume = float(volume)
    except (TypeError, ValueError):
        return 0.0

    step = float(getattr(info, "volume_step", 0.01) or 0.01)
    vmin = float(getattr(info, "volume_min", step) or step)
    vmax = float(getattr(info, "volume_max", 0) or 0)

    volume = max(volume, vmin)
    if vmax > 0:
        volume = min(volume, vmax)

    steps = round(volume / step)
    volume = steps * step
    if volume < vmin:
        volume = vmin

    decimals = max(0, len(str(step).split(".")[-1])) if "." in str(step) else 0
    return round(volume, decimals or 2)


def _sanitize_stop(info, tick, is_buy: bool, value, is_stop_loss: bool):
    """
    Descarta SL/TP no lado errado do preço ou dentro da distância mínima do broker
    (retcode 10016 - Invalid stops). Melhor enviar sem stop do que ter a ordem recusada.
    """
    if value is None:
        return None
    try:
        value = float(value)
    except (TypeError, ValueError):
        return None
    if value <= 0:
        return None

    point = float(getattr(info, "point", 0) or 0)
    digits = int(getattr(info, "digits", 2) or 2)
    min_distance = float(getattr(info, "trade_stops_level", 0) or 0) * point
    reference = tick.ask if is_buy else tick.bid

    below = value < reference
    should_be_below = is_buy if is_stop_loss else not is_buy
    if below != should_be_below:
        return None

    if min_distance > 0 and abs(reference - value) < min_distance:
        return None

    return round(value, digits)


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
        """Resolve nome lógico (allowlist SYMBOLS) para o símbolo exato no MT5 da corretora."""
        logical = logical.upper().strip()
        if not logical:
            return None

        # Never resolve / symbol_select outside the configured allowlist.
        if logical not in SYMBOLS:
            logger.debug("Símbolo %s fora da allowlist MT5_SYMBOLS=%s", logical, SYMBOLS)
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

        # Suffix/prefix match only against names that start with the allowlisted logical
        # (scan is for broker naming quirks — does not subscribe the full Market Watch).
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
                # Botão "Algo Trading" da UI: se False, todo order_send volta com retcode 10027.
                "trade_allowed": bool(term.trade_allowed) if term else False,
            },
            "account": {
                "login": acc.login if acc else None,
                "server": acc.server if acc else None,
                "currency": acc.currency if acc else None,
                "balance": acc.balance if acc else None,
                "equity": acc.equity if acc else None,
                "trade_allowed": bool(acc.trade_allowed) if acc else None,
                "trade_expert": bool(acc.trade_expert) if acc else None,
            } if acc else None,
            "available_symbols": SYMBOLS,
            "resolved_symbols": {
                s: self.resolve_symbol(s) for s in SYMBOLS
            },
        }

    # -------------------------------------------------------------------------
    # TRADE
    # -------------------------------------------------------------------------
    def send_market_order(
        self,
        symbol: str,
        side: str,
        volume: float,
        sl: float | None = None,
        tp: float | None = None,
        comment: str = "NTBot",
    ) -> dict:
        resolved = self.resolve_symbol(symbol) or symbol
        self._ensure_visible(resolved)
        info = mt5.symbol_info(resolved)
        if info is None:
            return {"ok": False, "error": f"Símbolo {resolved} não encontrado"}

        tick = mt5.symbol_info_tick(resolved)
        if tick is None:
            return {"ok": False, "error": "Tick indisponível"}

        terminal = mt5.terminal_info()
        if terminal is not None and not terminal.trade_allowed:
            return {
                "ok": False,
                "error": "AutoTrading desabilitado no terminal MT5 (habilite o botão Algo Trading).",
            }

        is_buy = str(side).lower() in ("buy", "long", "compra")
        order_type = mt5.ORDER_TYPE_BUY if is_buy else mt5.ORDER_TYPE_SELL
        price = tick.ask if is_buy else tick.bid
        if not price:
            return {"ok": False, "error": "Preço indisponível para o símbolo"}

        volume = _normalize_volume(info, volume)
        if volume <= 0:
            return {"ok": False, "error": "Volume inválido para o símbolo"}

        sl = _sanitize_stop(info, tick, is_buy, sl, is_stop_loss=True)
        tp = _sanitize_stop(info, tick, is_buy, tp, is_stop_loss=False)

        base_request = {
            "action": mt5.TRADE_ACTION_DEAL,
            "symbol": resolved,
            "volume": volume,
            "type": order_type,
            "price": price,
            "deviation": 30,
            "magic": 260805,
            "comment": (comment or "NTBot")[:31],
            "type_time": mt5.ORDER_TIME_GTC,
        }
        if sl is not None:
            base_request["sl"] = sl
        if tp is not None:
            base_request["tp"] = tp

        result = None
        attempts = []
        for filling in _filling_candidates(info):
            request = dict(base_request, type_filling=filling)
            result = mt5.order_send(request)
            if result is None:
                attempts.append(f"filling={filling}: {_format_mt5_error(mt5.last_error())}")
                continue
            if result.retcode in _RETCODE_OK:
                break
            attempts.append(f"filling={filling}: {result.retcode} {result.comment}")
            # Só vale reenviar quando o motivo é modo de preenchimento.
            if result.retcode != mt5.TRADE_RETCODE_INVALID_FILL:
                break

        if result is None:
            return {"ok": False, "error": "; ".join(attempts) or _format_mt5_error(mt5.last_error())}

        ok = result.retcode in _RETCODE_OK
        return {
            "ok": ok,
            "success": ok,
            "retcode": result.retcode,
            "deal": result.deal,
            "order": result.order,
            "ticket": result.order or result.deal,
            "volume": result.volume,
            "price": result.price,
            "message": "Ordem executada" if ok else f"retcode {result.retcode}: {result.comment}",
            "error": None if ok else "; ".join(attempts) or result.comment,
        }

    def _close_single(self, pos, close_vol: float):
        tick = mt5.symbol_info_tick(pos.symbol)
        if tick is None:
            return None, "tick indisponível"

        info = mt5.symbol_info(pos.symbol)
        order_type = mt5.ORDER_TYPE_SELL if pos.type == mt5.POSITION_TYPE_BUY else mt5.ORDER_TYPE_BUY
        price = tick.bid if order_type == mt5.ORDER_TYPE_SELL else tick.ask
        base_request = {
            "action": mt5.TRADE_ACTION_DEAL,
            "symbol": pos.symbol,
            "volume": close_vol,
            "type": order_type,
            "position": pos.ticket,
            "price": price,
            "deviation": 30,
            "magic": 260805,
            "comment": "NTBot-close",
            "type_time": mt5.ORDER_TIME_GTC,
        }

        result = None
        for filling in _filling_candidates(info):
            result = mt5.order_send(dict(base_request, type_filling=filling))
            if result is None:
                continue
            if result.retcode in _RETCODE_OK or result.retcode != mt5.TRADE_RETCODE_INVALID_FILL:
                break

        if result is None:
            return None, _format_mt5_error(mt5.last_error())
        if result.retcode not in _RETCODE_OK:
            return None, f"{result.retcode} {result.comment}"
        return result, None

    def close_positions(self, symbol: str, volume: float | None = None) -> dict:
        resolved = self.resolve_symbol(symbol) or symbol
        positions = mt5.positions_get(symbol=resolved)
        if positions is None:
            return {"ok": False, "error": _format_mt5_error(mt5.last_error())}
        if len(positions) == 0:
            return {"ok": True, "success": True, "message": "Nenhuma posição aberta", "closed": 0}

        closed = 0
        errors = []
        remaining = volume
        for pos in positions:
            close_vol = float(pos.volume) if remaining is None else min(float(pos.volume), float(remaining))
            if close_vol <= 0:
                continue
            result, error = self._close_single(pos, close_vol)
            if error is not None:
                errors.append(f"{pos.ticket}:{error}")
            else:
                closed += 1
                if remaining is not None:
                    remaining -= close_vol

        ok = closed > 0 or len(errors) == 0
        return {
            "ok": ok,
            "success": ok,
            "closed": closed,
            "message": f"{closed} posição(ões) fechada(s)" + (f" · falhas: {'; '.join(errors)}" if errors else ""),
            "error": "; ".join(errors) if errors else None,
        }

    def get_positions(self, symbol: str | None = None) -> list[dict]:
        positions = mt5.positions_get(symbol=self.resolve_symbol(symbol) or symbol) if symbol else mt5.positions_get()
        if positions is None:
            return []
        result = []
        for pos in positions:
            tick = mt5.symbol_info_tick(pos.symbol)
            if tick is None:
                current = pos.price_current
            else:
                current = tick.bid if pos.type == mt5.POSITION_TYPE_BUY else tick.ask
            result.append({
                "symbol": pos.symbol,
                "direction": "Buy" if pos.type == mt5.POSITION_TYPE_BUY else "Sell",
                "volume": float(pos.volume),
                "entryPrice": float(pos.price_open),
                "currentPrice": float(current or pos.price_current),
                "profit": float(pos.profit),
                "stopLoss": float(pos.sl) if pos.sl else None,
                "takeProfit": float(pos.tp) if pos.tp else None,
                "ticket": int(pos.ticket),
            })
        return result

    def modify_position_stops(
        self,
        ticket: int,
        symbol: str | None = None,
        sl: float | None = None,
        tp: float | None = None,
    ) -> dict:
        """Altera SL/TP de uma posição aberta (TRADE_ACTION_SLTP)."""
        positions = mt5.positions_get(ticket=ticket)
        if positions is None or len(positions) == 0:
            # fallback por símbolo
            resolved = self.resolve_symbol(symbol) or symbol if symbol else None
            all_pos = mt5.positions_get(symbol=resolved) if resolved else mt5.positions_get()
            if all_pos is None:
                return {"ok": False, "error": _format_mt5_error(mt5.last_error())}
            positions = [p for p in all_pos if int(p.ticket) == int(ticket)]
            if not positions:
                return {"ok": False, "error": f"Posição {ticket} não encontrada"}

        pos = positions[0]
        info = mt5.symbol_info(pos.symbol)
        tick = mt5.symbol_info_tick(pos.symbol)
        if info is None or tick is None:
            return {"ok": False, "error": "Símbolo/tick indisponível"}

        is_buy = pos.type == mt5.POSITION_TYPE_BUY
        new_sl = float(sl) if sl is not None else (float(pos.sl) if pos.sl else None)
        new_tp = float(tp) if tp is not None else (float(pos.tp) if pos.tp else None)

        if new_sl is not None:
            new_sl = _sanitize_stop(info, tick, is_buy, new_sl, is_stop_loss=True)
        if new_tp is not None:
            new_tp = _sanitize_stop(info, tick, is_buy, new_tp, is_stop_loss=False)

        request = {
            "action": mt5.TRADE_ACTION_SLTP,
            "symbol": pos.symbol,
            "position": int(pos.ticket),
            "magic": 260805,
        }
        if new_sl is not None:
            request["sl"] = new_sl
        if new_tp is not None:
            request["tp"] = new_tp

        result = mt5.order_send(request)
        if result is None:
            return {"ok": False, "error": _format_mt5_error(mt5.last_error())}

        ok = result.retcode in _RETCODE_OK
        return {
            "ok": ok,
            "success": ok,
            "retcode": result.retcode,
            "ticket": int(pos.ticket),
            "sl": new_sl,
            "tp": new_tp,
            "message": "SL/TP atualizado" if ok else f"retcode {result.retcode}: {result.comment}",
            "error": None if ok else (result.comment or _format_mt5_error(mt5.last_error())),
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
