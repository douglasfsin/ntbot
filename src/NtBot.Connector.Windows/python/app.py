"""
API Flask para dados em tempo real do MetaTrader5
Símbolos suportados: XAUUSD, EURUSD, NZDUSD
"""

import json
import time
import logging
from flask import Flask, jsonify, request, Response, abort
from flask_cors import CORS

from config import FLASK_CONFIG, SYMBOLS, STREAM_CONFIG
from mt5_service import MT5Service

# ---------------------------------------------------------------------------
# Configuração de logging
# ---------------------------------------------------------------------------
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Inicialização
# ---------------------------------------------------------------------------
app = Flask(__name__)
CORS(app)  # Permite chamadas cross-origin (dashboard web, etc.)

mt5 = MT5Service()


@app.before_request
def ensure_connected():
    """Garante que o MT5 está conectado antes de qualquer requisição."""
    if not mt5.is_connected():
        if not mt5.initialize():
            abort(503, description="MetaTrader5 não está disponível. Verifique se o terminal MT5 está aberto.")


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
def _validate_symbol(symbol: str) -> str:
    symbol = symbol.upper()
    if symbol not in SYMBOLS:
        abort(404, description=f"Símbolo '{symbol}' não configurado. Disponíveis: {SYMBOLS}")
    return symbol


def _error(msg: str, code: int = 400):
    return jsonify({"error": msg}), code


# ---------------------------------------------------------------------------
# Rotas REST
# ---------------------------------------------------------------------------

@app.get("/")
def index():
    """Health check e lista de rotas."""
    return jsonify({
        "status": "online",
        "available_symbols": SYMBOLS,
        "endpoints": {
            "status":       "GET /api/status",
            "symbols":      "GET /api/symbols",
            "symbol_info":  "GET /api/symbols/<symbol>",
            "ticker":       "GET /api/ticker/<symbol>",
            "book":         "GET /api/book/<symbol>",
            "volume":       "GET /api/volume/<symbol>",
            "ohlcv":        "GET /api/ohlcv/<symbol>?timeframe=M1&count=100",
            "variation":    "GET /api/variation/<symbol>",
            "stream_tick":  "GET /api/stream/tick/<symbol>   (SSE)",
            "stream_book":  "GET /api/stream/book/<symbol>   (SSE)",
            "stream_all":   "GET /api/stream/all/<symbol>    (SSE — tick + book + volume)",
            "stream_var":   "GET /api/stream/variation/<symbol> (SSE — var intraday + D-1)",
            "calendar":     "GET /api/calendar?days_back=1&days_ahead=14",
        },
    })


@app.get("/api/status")
def status():
    """Status da conexão com o MetaTrader5."""
    return jsonify(mt5.get_status())


@app.get("/api/symbols")
def list_symbols():
    """Lista os símbolos configurados."""
    return jsonify({"symbols": SYMBOLS})


@app.get("/api/symbols/<symbol>")
def symbol_info(symbol: str):
    """Informações detalhadas do símbolo (contrato, spreads, limites, etc.)."""
    symbol = _validate_symbol(symbol)
    data = mt5.get_symbol_info(symbol)
    if data is None:
        return _error(f"Não foi possível obter informações para {symbol}", 502)
    return jsonify(data)


@app.get("/api/ticker/<symbol>")
def ticker(symbol: str):
    """
    Retorna o último tick do símbolo.
    Campos: bid, ask, last, spread, volume, time
    """
    symbol = _validate_symbol(symbol)
    data = mt5.get_tick(symbol)
    if data is None:
        return _error(f"Tick não disponível para {symbol}", 502)
    return jsonify(data)


@app.get("/api/book/<symbol>")
def book(symbol: str):
    """
    Retorna o book de ordens (profundidade de mercado / DOM).
    Query param: depth (int, padrão = config BOOK_DEPTH)
    """
    symbol = _validate_symbol(symbol)
    data = mt5.get_book(symbol)
    if data is None:
        return _error(
            f"Book não disponível para {symbol}. "
            "Verifique se sua corretora fornece profundidade de mercado.",
            502,
        )
    return jsonify(data)


@app.get("/api/volume/<symbol>")
def volume(symbol: str):
    """
    Retorna informações de volume:
    - tick_volume_current_bar: volume de ticks na vela M1 atual
    - real_volume_current_bar: volume real na vela M1 atual
    - volume_today / volume_high_today / volume_low_today
    """
    symbol = _validate_symbol(symbol)
    data = mt5.get_volume(symbol)
    if data is None:
        return _error(f"Volume não disponível para {symbol}", 502)
    return jsonify(data)


@app.get("/api/ohlcv/<symbol>")
def ohlcv(symbol: str):
    """
    Retorna velas OHLCV históricas.
    Query params:
      - timeframe: M1, M5, M15, M30, H1, H4, D1, W1, MN1 (padrão: M1)
      - count: número de velas (padrão: 100, máximo: 5000)
    """
    symbol = _validate_symbol(symbol)
    timeframe = request.args.get("timeframe", "M1").upper()
    try:
        count = int(request.args.get("count", 100))
    except ValueError:
        return _error("Parâmetro 'count' deve ser um inteiro", 400)

    valid_tf = ["M1", "M5", "M15", "M30", "H1", "H4", "D1", "W1", "MN1"]
    if timeframe not in valid_tf:
        return _error(f"Timeframe inválido. Use: {valid_tf}", 400)

    data = mt5.get_ohlcv(symbol, timeframe, count)
    if data is None:
        return _error(f"Dados OHLCV não disponíveis para {symbol}/{timeframe}", 502)
    return jsonify(data)


@app.get("/api/variation/<symbol>")
def variation(symbol: str):
    """
    Retorna a variação intraday e diária do ativo.
    """
    symbol = _validate_symbol(symbol)
    data = mt5.get_variation(symbol)
    if data is None:
        return _error(f"Variação não disponível para {symbol}", 502)
    return jsonify(data)


@app.get("/api/calendar")
def calendar():
    """
    Retorna eventos do calendário econômico do MetaTrader5.
    Query params:
      - days_back: dias no passado (padrão 1)
      - days_ahead: dias no futuro (padrão 14)
    """
    try:
        days_back = int(request.args.get("days_back", 1))
        days_ahead = int(request.args.get("days_ahead", 14))
    except ValueError:
        return _error("Parâmetros days_back/days_ahead devem ser inteiros", 400)

    data = mt5.get_economic_calendar(days_back=days_back, days_ahead=days_ahead)
    return jsonify(data)


# ---------------------------------------------------------------------------
# Streaming SSE (Server-Sent Events)
# ---------------------------------------------------------------------------

def _sse_event(event_type: str, data: dict) -> str:
    """Formata uma mensagem SSE."""
    payload = json.dumps(data, default=str)
    return f"event: {event_type}\ndata: {payload}\n\n"


def _stream_tick(symbol: str):
    """Gerador SSE para ticks em tempo real."""
    interval = STREAM_CONFIG["tick_interval"]
    last_time_msc = None

    while True:
        try:
            tick = mt5.get_tick(symbol)
            if tick and tick.get("time_msc") != last_time_msc:
                last_time_msc = tick["time_msc"]
                yield _sse_event("tick", tick)
        except GeneratorExit:
            break
        except Exception as e:
            yield _sse_event("error", {"message": str(e)})
        time.sleep(interval)


def _stream_book(symbol: str):
    """Gerador SSE para book de ordens em tempo real."""
    interval = STREAM_CONFIG["tick_interval"]

    while True:
        try:
            book_data = mt5.get_book(symbol)
            if book_data:
                yield _sse_event("book", book_data)
        except GeneratorExit:
            mt5.release_book(symbol)
            break
        except Exception as e:
            yield _sse_event("error", {"message": str(e)})
        time.sleep(interval)


def _stream_all(symbol: str):
    """Gerador SSE combinado: tick + book + volume."""
    interval = STREAM_CONFIG["tick_interval"]
    last_time_msc = None

    while True:
        try:
            tick = mt5.get_tick(symbol)
            if tick:
                if tick.get("time_msc") != last_time_msc:
                    last_time_msc = tick["time_msc"]
                    yield _sse_event("tick", tick)

            book_data = mt5.get_book(symbol)
            if book_data:
                yield _sse_event("book", book_data)

            vol = mt5.get_volume(symbol)
            if vol:
                yield _sse_event("volume", vol)

            var_data = mt5.get_variation(symbol)
            if var_data:
                yield _sse_event("variation", var_data)

        except GeneratorExit:
            mt5.release_book(symbol)
            break
        except Exception as e:
            yield _sse_event("error", {"message": str(e)})
        time.sleep(interval)


def _stream_variation(symbol: str):
    """Gerador SSE para métricas de variação em tempo real."""
    interval = STREAM_CONFIG["tick_interval"]
    last_time_msc = None

    while True:
        try:
            var_data = mt5.get_variation(symbol)
            if var_data and var_data.get("time_msc") != last_time_msc:
                last_time_msc = var_data["time_msc"]
                yield _sse_event("variation", var_data)
        except GeneratorExit:
            break
        except Exception as e:
            yield _sse_event("error", {"message": str(e)})
        time.sleep(interval)


@app.get("/api/stream/tick/<symbol>")
def stream_tick(symbol: str):
    """
    SSE — stream de ticks em tempo real.
    Conecte via EventSource no browser:
      const es = new EventSource('/api/stream/tick/XAUUSD');
      es.addEventListener('tick', e => console.log(JSON.parse(e.data)));
    """
    symbol = _validate_symbol(symbol)
    return Response(
        _stream_tick(symbol),
        mimetype="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "X-Accel-Buffering": "no",  # desativa buffer no nginx
        },
    )


@app.get("/api/stream/book/<symbol>")
def stream_book(symbol: str):
    """
    SSE — stream do book de ordens em tempo real.
    """
    symbol = _validate_symbol(symbol)
    return Response(
        _stream_book(symbol),
        mimetype="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "X-Accel-Buffering": "no",
        },
    )


@app.get("/api/stream/all/<symbol>")
def stream_all(symbol: str):
    """
    SSE — stream completo: tick + book + volume em tempo real.
    Eventos: 'tick', 'book', 'volume', 'error'
    """
    symbol = _validate_symbol(symbol)
    return Response(
        _stream_all(symbol),
        mimetype="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "X-Accel-Buffering": "no",
        },
    )


@app.get("/api/stream/variation/<symbol>")
def stream_variation(symbol: str):
    """
    SSE — stream de variação em tempo real.
    Eventos: 'variation', 'error'
    """
    symbol = _validate_symbol(symbol)
    return Response(
        _stream_variation(symbol),
        mimetype="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "X-Accel-Buffering": "no",
        },
    )


# ---------------------------------------------------------------------------
# Error handlers
# ---------------------------------------------------------------------------

@app.errorhandler(400)
def bad_request(e):
    return jsonify({"error": str(e.description)}), 400


@app.errorhandler(404)
def not_found(e):
    return jsonify({"error": str(e.description)}), 404


@app.errorhandler(503)
def service_unavailable(e):
    return jsonify({"error": str(e.description)}), 503


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    logger.info("Iniciando API MetaTrader5...")
    if not mt5.initialize():
        logger.error("Falha ao conectar no MT5. Certifique-se de que o terminal está aberto.")
    else:
        logger.info(f"Símbolos configurados: {SYMBOLS}")
        logger.info(f"Servidor rodando em http://{FLASK_CONFIG['host']}:{FLASK_CONFIG['port']}")
        app.run(
            host=FLASK_CONFIG["host"],
            port=FLASK_CONFIG["port"],
            debug=FLASK_CONFIG["debug"],
            threaded=True,
        )
