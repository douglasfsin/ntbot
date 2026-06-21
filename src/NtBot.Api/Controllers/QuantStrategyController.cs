using Microsoft.AspNetCore.Mvc;
using NtBot.Api.Dtos;
using NtBot.Api.Services.Macro;
using NtBot.Api.Services.MarketData;
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
        private readonly IMacroOrderGate _macroGate;
        private readonly IMarketCandleService _candleService;

        public QuantStrategyController(
            ILogger<QuantStrategyController> logger,
            QuantStrategy quantStrategy,
            IGlobalCorrelationService correlationService,
            IGammaExposureService gexService,
            IMacroOrderGate macroGate,
            IMarketCandleService candleService)
        {
            _logger = logger;
            _quantStrategy = quantStrategy;
            _correlationService = correlationService;
            _gexService = gexService;
            _macroGate = macroGate;
            _candleService = candleService;
        }

        /// <summary>
        /// Analisa e gera sinal de trading para o ativo especificado
        /// </summary>
        [HttpPost("analyze")]
        public async Task<ActionResult<QuantSignal>> Analyze(
            [FromBody] AnalyzeRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Analisando {Symbol} com líder {Leader}",
                    request.Symbol, request.LeaderSymbol);

                var candlesResult = await _candleService.GetCandlesAsync(request.Symbol, 100, cancellationToken: cancellationToken);
                var leaderResult = await _candleService.GetCandlesAsync(request.LeaderSymbol, 100, cancellationToken: cancellationToken);

                if (!candlesResult.HasSufficientData(50))
                {
                    return Ok(new
                    {
                        message = "Dados OHLCV insuficientes para o ativo",
                        symbol = request.Symbol,
                        source = candlesResult.Source,
                        candles = candlesResult.Candles.Count
                    });
                }

                var candles = candlesResult.Candles.ToList();
                var leaderCandles = leaderResult.Candles.ToList();

                var signal = await _quantStrategy.AnalyzeAsync(
                    request.Symbol,
                    candles,
                    request.LeaderSymbol,
                    leaderCandles
                );

                if (signal == null)
                {
                    return Ok(new { message = "Nenhum sinal gerado no momento", candleSource = candlesResult.Source });
                }

                if (request.TenantId is not null)
                {
                    var direction = signal.Direction == SignalDirection.LONG
                        ? TradeDirection.LONG
                        : TradeDirection.SHORT;
                    var gate = await _macroGate.EvaluateAsync(request.TenantId.Value, request.Symbol, direction);
                    if (!gate.Allowed)
                    {
                        return Ok(new
                        {
                            message = "Sinal rejeitado pelo filtro macro",
                            reason = gate.Reason,
                            signal
                        });
                    }
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
            [FromQuery] int lookback = 50,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var leaderResult = await _candleService.GetCandlesAsync(leaderSymbol, lookback + 10, cancellationToken: cancellationToken);
                var followerResult = await _candleService.GetCandlesAsync(followerSymbol, lookback + 10, cancellationToken: cancellationToken);

                if (!leaderResult.HasSufficientData(lookback) || !followerResult.HasSufficientData(lookback))
                {
                    return Ok(new
                    {
                        message = "Dados insuficientes para correlação",
                        leaderSource = leaderResult.Source,
                        followerSource = followerResult.Source
                    });
                }

                var correlation = await _correlationService.CalculateCorrelationAsync(
                    leaderSymbol,
                    followerSymbol,
                    leaderResult.Candles.ToList(),
                    followerResult.Candles.ToList(),
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
            [FromQuery] string symbol,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var candlesResult = await _candleService.GetCandlesAsync(symbol, 10, cancellationToken: cancellationToken);
                if (candlesResult.Candles.Count == 0)
                    return NotFound(new { message = "Preço indisponível", source = candlesResult.Source });

                var currentPrice = candlesResult.Candles.OrderBy(c => c.CloseTime).Last().Close;
                var optionsData = await _gexService.GetOptionsDataAsync(symbol, spotPrice: currentPrice);
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
            [FromQuery] string symbol,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var candlesResult = await _candleService.GetCandlesAsync(symbol, 5, cancellationToken: cancellationToken);
                var spot = candlesResult.Candles.LastOrDefault()?.Close;
                var options = await _gexService.GetOptionsDataAsync(symbol, spotPrice: spot);
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
            [FromQuery] string leaderSymbol = "NQ",
            CancellationToken cancellationToken = default)
        {
            try
            {
                var candlesResult = await _candleService.GetCandlesAsync(symbol, 100, cancellationToken: cancellationToken);
                var leaderResult = await _candleService.GetCandlesAsync(leaderSymbol, 100, cancellationToken: cancellationToken);

                if (!candlesResult.HasSufficientData(50))
                {
                    var available = await _candleService.GetAvailableSymbolsAsync(50, cancellationToken: cancellationToken);
                    var hint = available.Count > 0
                        ? $" Candles no banco: {string.Join(", ", available)}."
                        : " Nenhum candle no banco — reinicie o connector Windows (build recente) com MT5 aberto.";

                    return Ok(new DashboardData
                    {
                        Symbol = symbol,
                        LeaderSymbol = leaderSymbol,
                        Timestamp = DateTime.UtcNow,
                        CandleSource = candlesResult.Source,
                        LeaderCandleSource = leaderResult.Source,
                        DataAvailable = false,
                        AvailableSymbols = available.ToList(),
                        Message = $"Aguardando candles MT5 para {symbol}/{leaderSymbol}.{hint}"
                    });
                }

                var candles = candlesResult.Candles.ToList();
                var leaderCandles = leaderResult.Candles.ToList();
                var currentPrice = candles.OrderBy(c => c.CloseTime).Last().Close;

                var correlation = leaderCandles.Count >= 50
                    ? await _correlationService.CalculateCorrelationAsync(
                        leaderSymbol,
                        symbol,
                        leaderCandles,
                        candles,
                        50)
                    : null;

                var optionsData = await _gexService.GetOptionsDataAsync(symbol, spotPrice: currentPrice);
                var gexData = await _gexService.CalculateGEXAsync(symbol, currentPrice, optionsData);

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
                    RecentCandles = candles.OrderByDescending(c => c.CloseTime).Take(20).ToList(),
                    CandleSource = candlesResult.Source,
                    LeaderCandleSource = leaderResult.Source,
                    DataAvailable = true
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
        /// Símbolos com candles suficientes no banco
        /// </summary>
        [HttpGet("symbols")]
        public async Task<ActionResult<IReadOnlyList<string>>> GetAvailableSymbols(
            [FromQuery] int minimum = 50,
            CancellationToken cancellationToken = default)
        {
            var symbols = await _candleService.GetAvailableSymbolsAsync(minimum, cancellationToken: cancellationToken);
            return Ok(symbols);
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
                var signals = new List<QuantSignal>();
                return Ok(signals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter histórico de sinais");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    #region Request/Response Models

    public class AnalyzeRequest
    {
        public string Symbol { get; set; } = "WINFUT";
        public string LeaderSymbol { get; set; } = "NQ";
        public Guid? TenantId { get; set; }
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
        public string CandleSource { get; set; } = "unavailable";
        public string LeaderCandleSource { get; set; } = "unavailable";
        public bool DataAvailable { get; set; } = true;
        public string? Message { get; set; }
        public List<string> AvailableSymbols { get; set; } = [];
    }

    #endregion
}

