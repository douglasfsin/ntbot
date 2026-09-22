using MarketData.API.Data;
using MarketData.API.Worker;
using Microsoft.AspNetCore.Mvc;

namespace MarketData.API.Controllers
{
    [ApiController]
    [Route("api/market")]
    public class MarketController : ControllerBase
    {
        private readonly IMarketDataProvider _marketDataProvider;
        private readonly MarketDataWorker _worker;

        public MarketController(IMarketDataProvider marketDataProvider, MarketDataWorker worker)
        {
            _marketDataProvider = marketDataProvider;
            _worker = worker;
        }

        [HttpGet("{ticker}")]
        public async Task<IActionResult> Get(string ticker)
        {
            var data = MarketCache.Get(ticker);
            return Ok(data);
        }

        [HttpGet("StartSimulator")]
        public async Task<IActionResult> StartSimulator()
        {
            _ = _marketDataProvider.StartAsync(CancellationToken.None);

            return new OkResult();
        }

        [HttpGet("StopSimulator")]
        public async Task<IActionResult> StopSimulator()
        {
            _ = _marketDataProvider.StopAsync(CancellationToken.None);
            return new OkResult();
        }

        [HttpPost("simulation/start")]
        public async Task<IActionResult> StartSimulation([FromBody] SimulationRequest request)
        {
            await _marketDataProvider.StartSimulationAsync(request.Date, CancellationToken.None);
            return Ok();
        }

        [HttpPost("simulation/stop")]
        public IActionResult StopSimulation()
        {
            _worker.StopSimulation();
            return Ok();
        }
    }

    public class SimulationRequest
    {
        public string Date { get; set; } = "";
    }
}
