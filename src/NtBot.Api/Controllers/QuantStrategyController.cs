using Microsoft.AspNetCore.Mvc;
using NtBot.Domain.Entities;
using NtBot.Api.Services.Correlation;
using NtBot.Api.Services.GammaExposure;
using NtBot.Api.Strategies;

namespace NtBot.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuantStrategyController : ControllerBase
    {
        private readonly ILogger<QuantStrategyController> _logger;
        private readonly QuantStrategy _quantStrategy;
        private readonly IGlobalCorrelationService _correlationService;
        private readonly IGammaExposureService _gexService;

        public QuantStrategyController(
            ILogger<QuantStrategyController> logger,
            QuantStrategy quantStrategy,
            IGlobalCorrelationService correlationService,
            IGammaExposureService gexService)
        {
            _logger = logger;
            _quantStrategy = quantStrategy;
            _correlationService = correlationService;
            _gexService = gexService;
        }

        /// <summary>
        /// Analisa e gera sinal de trading para o ativo especificado
        /// </summary>
        /// <param name="symbol">Símbolo do ativo (ex: WINFUT, WDOFUT)</param>
        /// <param name="leaderSymbol">Símbolo do líder global (padrão: NQ)</param>
        /// <returns>Sinal de trading ou null se não houver oportunidade</returns>
        [HttpPost("analyze")]
        public async Task<ActionResult<QuantSignal>> Analyze(
            [FromBody] AnalyzeRequest request)
        {
            try
            {
                _logger.LogInformation("Analisando {Symbol} com líder {Leader}",
                    request.Symbol, request.LeaderSymbol);

                // TODO: Obter candles do banco de dados ou NinjaTrader
                // Por enquanto, usando dados mock
                var candles = GenerateMockCandles(request.Symbol, 100);
                var leaderCandles = GenerateMockCandles(request.LeaderSymbol, 100);

                var signal = await _quantStrategy.AnalyzeAsync(
                    request.Symbol,
                    candles,
                    request.LeaderSymbol,
                    leaderCandles
                );

                if (signal == null)
                {
                    return Ok(new { message = "Nenhum sinal gerado no momento" });
                }

                return Ok(signal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar {Symbol}", request.Symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Obtém dados de correlação entre NQ e WIN/WDO
        /// </summary>
        [HttpGet("correlation")]
        public async Task<ActionResult<CorrelationData>> GetCorrelation(
            [FromQuery] string leaderSymbol = "NQ",
            [FromQuery] string followerSymbol = "WINFUT",
            [FromQuery] int lookback = 50)
        {
            try
            {
                var leaderCandles = GenerateMockCandles(leaderSymbol, lookback + 10);
                var followerCandles = GenerateMockCandles(followerSymbol, lookback + 10);

                var correlation = await _correlationService.CalculateCorrelationAsync(
                    leaderSymbol,
                    followerSymbol,
                    leaderCandles,
                    followerCandles,
                    lookback
                );

                return Ok(correlation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular correlação");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Obtém dados de Gamma Exposure (GEX) para o ativo
        /// </summary>
        [HttpGet("gex")]
        public async Task<ActionResult<GammaExposureData>> GetGEX(
            [FromQuery] string symbol)
        {
            try
            {
                var candles = GenerateMockCandles(symbol, 10);
                var currentPrice = candles.OrderBy(c => c.CloseTime).Last().Close;

                var optionsData = await _gexService.GetOptionsDataAsync(symbol);
                var gexData = await _gexService.CalculateGEXAsync(symbol, currentPrice, optionsData);

                return Ok(gexData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter GEX para {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Obtém dados de opções para visualização
        /// </summary>
        [HttpGet("options")]
        public async Task<ActionResult<List<OptionData>>> GetOptions(
            [FromQuery] string symbol)
        {
            try
            {
                var options = await _gexService.GetOptionsDataAsync(symbol);
                return Ok(options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter opções para {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Realiza análise completa com todos os componentes
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<ActionResult<DashboardData>> GetDashboard(
            [FromQuery] string symbol = "WINFUT",
            [FromQuery] string leaderSymbol = "NQ")
        {
            try
            {
                var candles = GenerateMockCandles(symbol, 100);
                var leaderCandles = GenerateMockCandles(leaderSymbol, 100);
                var currentPrice = candles.OrderBy(c => c.CloseTime).Last().Close;

                // Correlação
                var correlation = await _correlationService.CalculateCorrelationAsync(
                    leaderSymbol,
                    symbol,
                    leaderCandles,
                    candles,
                    50
                );

                // GEX
                var optionsData = await _gexService.GetOptionsDataAsync(symbol);
                var gexData = await _gexService.CalculateGEXAsync(symbol, currentPrice, optionsData);

                // Sinal
                var signal = await _quantStrategy.AnalyzeAsync(symbol, candles, leaderSymbol, leaderCandles);

                var dashboard = new DashboardData
                {
                    Symbol = symbol,
                    LeaderSymbol = leaderSymbol,
                    CurrentPrice = currentPrice,
                    Timestamp = DateTime.UtcNow,
                    Correlation = correlation,
                    GEX = gexData,
                    Signal = signal,
                    RecentCandles = candles.OrderByDescending(c => c.CloseTime).Take(20).ToList()
                };

                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter dashboard para {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Obtém histórico de sinais
        /// </summary>
        [HttpGet("signals/history")]
        public async Task<ActionResult<List<QuantSignal>>> GetSignalHistory(
            [FromQuery] string? symbol = null,
            [FromQuery] int limit = 50)
        {
            try
            {
                // TODO: Implementar persistência em banco de dados
                // Por enquanto retorna lista vazia
                var signals = new List<QuantSignal>();
                return Ok(signals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter histórico de sinais");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #region Helper Methods

        private List<Candle> GenerateMockCandles(string symbol, int count)
        {
            var candles = new List<Candle>();
            var random = new Random();
            var basePrice = symbol.Contains("WIN") ? 120000m : 16000m;
            var baseTime = DateTime.UtcNow.AddMinutes(-count * 5);

            for (int i = 0; i < count; i++)
            {
                var open = basePrice + (decimal)(random.NextDouble() * 1000 - 500);
                var close = open + (decimal)(random.NextDouble() * 200 - 100);
                var high = Math.Max(open, close) + (decimal)(random.NextDouble() * 100);
                var low = Math.Min(open, close) - (decimal)(random.NextDouble() * 100);

                candles.Add(new Candle
                {
                    Symbol = symbol,
                    CloseTime = baseTime.AddMinutes(i * 5),
                    OpenTime = baseTime.AddMinutes(i * 5),
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = 1000 + random.Next(5000)
                });

                basePrice = close; // Continua do fechamento anterior
            }

            return candles;
        }

        #endregion
    }

    #region Request/Response Models

    public class AnalyzeRequest
    {
        public string Symbol { get; set; } = "WINFUT";
        public string LeaderSymbol { get; set; } = "NQ";
    }

    public class DashboardData
    {
        public string Symbol { get; set; } = string.Empty;
        public string LeaderSymbol { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public DateTime Timestamp { get; set; }
        public CorrelationData? Correlation { get; set; }
        public GammaExposureData? GEX { get; set; }
        public QuantSignal? Signal { get; set; }
        public List<Candle> RecentCandles { get; set; } = new();
    }

    #endregion
}
