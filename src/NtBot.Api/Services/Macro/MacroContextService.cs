using NtBot.Api.Services.NinjaTrader;
using NtBot.Macro.Configuration;
using NtBot.Macro.Services;

namespace NtBot.Api.Services.Macro
{
    public class MacroContextService : IMacroContextService
    {
        private readonly ILogger<MacroContextService> _logger;
        private readonly INinjaTraderService _ninjaTraderService;
        private readonly IMacroIntelligenceService _macroIntelligence;

        public MacroContextService(
            ILogger<MacroContextService> logger,
            INinjaTraderService ninjaTraderService,
            IMacroIntelligenceService macroIntelligence)
        {
            _logger = logger;
            _ninjaTraderService = ninjaTraderService;
            _macroIntelligence = macroIntelligence;
        }

        public async Task<MacroContextResult> AnalyzeAsync(string primarySymbol = "MNQ")
        {
            var normalized = MacroSymbolAliases.Normalize(primarySymbol);

            try
            {
                var snapshot = await _macroIntelligence.GetCurrentSnapshotAsync(normalized);
                Dictionary<string, decimal>? correlations = null;

                try
                {
                    correlations = await GetCorrelationsAsync(normalized, ["ES", "NQ", "DXY"]);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Correlações NinjaTrader indisponíveis para {Symbol}", normalized);
                }

                var result = MacroSnapshotMapper.ToContextResult(snapshot, correlations);

                _logger.LogInformation(
                    "Macro analysis (unified): Bias={Bias}, RiskMode={RiskMode}, VIX={VIX}",
                    result.Bias, result.RiskMode, result.VIXLevel);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in unified macro analysis");
                return new MacroContextResult
                {
                    AnalysisTime = DateTime.UtcNow,
                    RiskMode = RiskMode.BLOCKED,
                    Observations = ["Falha ao obter snapshot macro"]
                };
            }
        }

        public async Task<MacroBias> GetDailyBiasAsync(string symbol)
        {
            var snapshot = await _macroIntelligence.GetCurrentSnapshotAsync(MacroSymbolAliases.Normalize(symbol));
            return MacroSnapshotMapper.ToContextResult(snapshot).Bias;
        }

        public async Task<RiskMode> GetRiskModeAsync()
        {
            var snapshot = await _macroIntelligence.GetCurrentSnapshotAsync();
            return MacroSnapshotMapper.ToContextResult(snapshot).RiskMode;
        }

        public async Task<Dictionary<string, decimal>> GetCorrelationsAsync(string symbol, List<string> referenceSymbols)
        {
            var correlations = new Dictionary<string, decimal>();
            var normalized = MacroSymbolAliases.Normalize(symbol);

            var primaryData = await _ninjaTraderService.GetHistoricalCandlesAsync(normalized, "1h",
                DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

            if (primaryData.Count < 20)
                return correlations;

            var primaryReturns = CalculateReturns(primaryData);

            foreach (var refSymbol in referenceSymbols)
            {
                var refData = await _ninjaTraderService.GetHistoricalCandlesAsync(refSymbol, "1h",
                    DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

                if (refData.Count < 20)
                    continue;

                var refReturns = CalculateReturns(refData);
                correlations[refSymbol] = CalculateCorrelation(primaryReturns, refReturns);
            }

            return correlations;
        }

        private static List<decimal> CalculateReturns(List<Domain.Entities.Candle> candles)
        {
            var returns = new List<decimal>();
            for (var i = 1; i < candles.Count; i++)
            {
                var ret = (candles[i].Close - candles[i - 1].Close) / candles[i - 1].Close;
                returns.Add(ret);
            }

            return returns;
        }

        private static decimal CalculateCorrelation(List<decimal> x, List<decimal> y)
        {
            var n = Math.Min(x.Count, y.Count);
            if (n < 2) return 0;

            var xArray = x.Take(n).ToList();
            var yArray = y.Take(n).ToList();
            var xMean = xArray.Average();
            var yMean = yArray.Average();

            decimal numerator = 0, xSumSquares = 0, ySumSquares = 0;
            for (var i = 0; i < n; i++)
            {
                var xDiff = xArray[i] - xMean;
                var yDiff = yArray[i] - yMean;
                numerator += xDiff * yDiff;
                xSumSquares += xDiff * xDiff;
                ySumSquares += yDiff * yDiff;
            }

            if (xSumSquares == 0 || ySumSquares == 0)
                return 0;

            return numerator / (decimal)Math.Sqrt((double)(xSumSquares * ySumSquares));
        }
    }
}
