using Microsoft.AspNetCore.Mvc;
using NtBot.Api.Services.Wyckoff;
using NtBot.Api.Services.Macro;
using NtBot.Api.Services.NinjaTrader;

namespace NtBot.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalysisController : ControllerBase
    {
        private readonly IWyckoffService _wyckoffService;
        private readonly IMacroContextService _macroService;
        private readonly INinjaTraderService _ninjaTrader;
        private readonly ILogger<AnalysisController> _logger;

        public AnalysisController(
            IWyckoffService wyckoffService,
            IMacroContextService macroService,
            INinjaTraderService ninjaTrader,
            ILogger<AnalysisController> logger)
        {
            _wyckoffService = wyckoffService;
            _macroService = macroService;
            _ninjaTrader = ninjaTrader;
            _logger = logger;
        }

        /// <summary>
        /// Analisa Wyckoff em um timeframe específico
        /// </summary>
        [HttpGet("wyckoff/{symbol}")]
        public async Task<ActionResult<WyckoffAnalysisResult>> AnalyzeWyckoff(
            string symbol,
            [FromQuery] string timeframe = "5m",
            [FromQuery] int candleCount = 100)
        {
            try
            {
                var to = DateTime.UtcNow;
                var from = timeframe switch
                {
                    "1m" => to.AddHours(-2),
                    "5m" => to.AddHours(-8),
                    "15m" => to.AddHours(-24),
                    "1h" => to.AddDays(-5),
                    "1d" => to.AddDays(-100),
                    _ => to.AddHours(-8)
                };

                var candles = await _ninjaTrader.GetHistoricalCandlesAsync(symbol, timeframe, from, to);
                
                if (candles.Count == 0)
                {
                    return BadRequest(new { error = "Não foi possível obter dados de mercado" });
                }

                var result = await _wyckoffService.AnalyzeAsync(symbol, timeframe, candles);
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing Wyckoff for {Symbol} {Timeframe}", symbol, timeframe);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Analisa contexto macro
        /// </summary>
        [HttpGet("macro/{symbol}")]
        public async Task<ActionResult<MacroContextResult>> AnalyzeMacro(string symbol = "MNQ")
        {
            try
            {
                var result = await _macroService.AnalyzeAsync(symbol);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing macro context for {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Análise completa (Wyckoff + Macro)
        /// </summary>
        [HttpGet("complete/{symbol}")]
        public async Task<ActionResult> CompleteAnalysis(string symbol, [FromQuery] string timeframe = "5m")
        {
            try
            {
                var wyckoffTask = AnalyzeWyckoff(symbol, timeframe, 100);
                var macroTask = AnalyzeMacro(symbol);

                await Task.WhenAll(wyckoffTask, macroTask);

                var wyckoffResult = await wyckoffTask;
                var macroResult = await macroTask;

                return Ok(new
                {
                    symbol,
                    timeframe,
                    timestamp = DateTime.UtcNow,
                    wyckoff = ((ObjectResult)wyckoffResult.Result!).Value,
                    macro = ((ObjectResult)macroResult.Result!).Value,
                    recommendation = GenerateRecommendation(
                        ((ObjectResult)wyckoffResult.Result!).Value as WyckoffAnalysisResult,
                        ((ObjectResult)macroResult.Result!).Value as MacroContextResult)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in complete analysis for {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private object GenerateRecommendation(WyckoffAnalysisResult? wyckoff, MacroContextResult? macro)
        {
            if (wyckoff == null || macro == null)
            {
                return new { action = "WAIT", reason = "Dados insuficientes" };
            }

            // Lógica simples de recomendação
            if (macro.RiskMode == RiskMode.BLOCKED)
            {
                return new
                {
                    action = "BLOCK",
                    reason = "Risco macro elevado",
                    details = "Volatilidade extrema ou evento de alto impacto"
                };
            }

            var wyckoffBullish = wyckoff.Bias == MarketBias.BULLISH && 
                                 (wyckoff.Event == WyckoffEvent.SPRING || wyckoff.Event == WyckoffEvent.SOS);
            
            var wyckoffBearish = wyckoff.Bias == MarketBias.BEARISH && 
                                 (wyckoff.Event == WyckoffEvent.UPTHRUST || wyckoff.Event == WyckoffEvent.SOW);

            var macroBullish = macro.Bias == MacroBias.BULLISH;
            var macroBearish = macro.Bias == MacroBias.BEARISH;

            // Alinhamento perfeito
            if (wyckoffBullish && macroBullish && wyckoff.EventConfidence > 70)
            {
                return new
                {
                    action = "LONG",
                    confidence = Math.Min(wyckoff.EventConfidence, macro.ConfidenceScore),
                    reason = $"Wyckoff {wyckoff.Event} + Macro Bullish alinhados",
                    riskMode = macro.RiskMode.ToString()
                };
            }

            if (wyckoffBearish && macroBearish && wyckoff.EventConfidence > 70)
            {
                return new
                {
                    action = "SHORT",
                    confidence = Math.Min(wyckoff.EventConfidence, macro.ConfidenceScore),
                    reason = $"Wyckoff {wyckoff.Event} + Macro Bearish alinhados",
                    riskMode = macro.RiskMode.ToString()
                };
            }

            // Conflito
            if ((wyckoffBullish && macroBearish) || (wyckoffBearish && macroBullish))
            {
                return new
                {
                    action = "WAIT",
                    reason = "Conflito entre Wyckoff e Macro",
                    details = $"Wyckoff: {wyckoff.Bias}, Macro: {macro.Bias}"
                };
            }

            return new
            {
                action = "WAIT",
                reason = "Condições insuficientes para entrada",
                wyckoffPhase = wyckoff.Phase.ToString(),
                wyckoffConfidence = wyckoff.PhaseConfidence,
                macroBias = macro.Bias.ToString()
            };
        }
    }
}
