using MarketData.API.MT5Worker;
using MarketData.API.Worker;
using Microsoft.AspNetCore.Mvc;

namespace MarketData.API.Controllers
{
    /// <summary>
    /// Endpoint REST para ingestão de dados enviados pelo MetaTrader 5.
    ///
    /// O Expert Advisor (EA) no MT5 deve enviar dados via WebRequest para:
    ///   POST /api/mt5/tick  — preço de último negócio ou tópico específico
    ///   POST /api/mt5/book  — snapshot do book (bid/ask)
    ///   GET  /api/mt5/status — estado do worker
    /// </summary>
    [ApiController]
    [Route("api/mt5")]
    public class MT5Controller : ControllerBase
    {
        private readonly MT5Worker.MT5Worker _worker;

        public MT5Controller(MT5Worker.MT5Worker worker)
        {
            _worker = worker;
        }
        /// <summary>
        /// Recebe lote de ticks do MT5.
        /// </summary>
        [HttpPost("ticks")]
        public IActionResult PostTicks(
            [FromBody] List<MT5TickPayload> payloads)
        {
            if (payloads == null || payloads.Count == 0)
                return BadRequest("Payload vazio.");

            foreach (var payload in payloads)
            {
                if (string.IsNullOrWhiteSpace(payload.Ticker))
                    continue;

                _worker.IngestTick(
                    payload.Ticker,
                    payload.Topic ?? "ULT",
                    payload.Value
                );
            }

            Console.WriteLine(
                $"Batch recebido: {payloads.Count}"
            );

            return Ok(new
            {
                success = true,
                count = payloads.Count
            });
        }
        /// <summary>
        /// Recebe um tick de preço do MT5.
        /// Body: { "Ticker": "EURUSD", "Topic": "ULT", "Value": 1.0823 }
        /// </summary>
        [HttpPost("tick")]
        public IActionResult PostTick([FromBody] MT5TickPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Ticker))
                return BadRequest("Ticker obrigatório.");

            _worker.IngestTick(payload.Ticker, payload.Topic ?? "ULT", payload.Value);
            return Ok();
        }

        /// <summary>
        /// Recebe um snapshot de book do MT5.
        /// Body: { "Ticker": "EURUSD", "Bid": 1.0822, "Ask": 1.0824, "BidVolume": 500, "AskVolume": 300 }
        /// </summary>
        [HttpPost("book")]
        public IActionResult PostBook([FromBody] MT5BookPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Ticker))
                return BadRequest("Ticker obrigatório.");

            _worker.IngestBook(payload);
            return Ok();
        }

        /// <summary>
        /// Retorna o snapshot atual de um ticker.
        /// </summary>
        [HttpGet("{ticker}")]
        public IActionResult GetSnapshot(string ticker)
        {
            var snap = _worker.GetSnapshot(ticker);
            if (snap == null) return NotFound($"Nenhum dado para '{ticker}'.");
            return Ok(snap);
        }

        /// <summary>
        /// Retorna todos os snapshots em cache.
        /// </summary>
        [HttpGet("snapshots")]
        public IActionResult GetAllSnapshots()
        {
            return Ok(_worker.GetAllSnapshots());
        }

        /// <summary>
        /// Healthcheck do MT5Worker.
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                status = "running",
                tickers = _worker.GetAllSnapshots().Keys,
                timestamp = DateTime.UtcNow
            });
        }
    }
}
