using NtBot.Domain.Entities;
using NtBot.Api.Services.NinjaTrader;

namespace NtBot.Api.Services.Macro
{
    public class MacroContextService : IMacroContextService
    {
        private readonly ILogger<MacroContextService> _logger;
        private readonly INinjaTraderService _ninjaTraderService;
        
        public MacroContextService(
            ILogger<MacroContextService> logger,
            INinjaTraderService ninjaTraderService)
        {
            _logger = logger;
            _ninjaTraderService = ninjaTraderService;
        }

        public async Task<MacroContextResult> AnalyzeAsync(string primarySymbol = "MNQ")
        {
            var result = new MacroContextResult
            {
                AnalysisTime = DateTime.UtcNow
            };

            try
            {
                // 1. Analisa ES (S&P 500) como referência principal
                var esBias = await GetDailyBiasAsync("ES");
                
                // 2. Analisa DXY (Dollar Index)
                var dxyBias = await GetDailyBiasAsync("DXY");
                
                // 3. Analisa VIX (volatilidade)
                result.VIXLevel = await GetVIXLevelAsync();
                result.VolatilityRegime = DetermineVolatilityRegime(result.VIXLevel);
                
                // 4. Correlações
                result.Correlations = await GetCorrelationsAsync(primarySymbol, new List<string> { "ES", "NQ", "DXY" });
                
                // 5. Risk-on / Risk-off
                result.IsRiskOn = DetermineRiskOnOff(esBias, dxyBias, result.VIXLevel);
                
                // 6. Bias final
                result.Bias = DetermineFinalBias(esBias, result.IsRiskOn, result.Correlations);
                
                // 7. Risk Mode
                result.RiskMode = await GetRiskModeAsync();
                
                // 8. Confidence
                result.ConfidenceScore = CalculateConfidence(result);
                
                // 9. Observações
                AddObservations(result, esBias, dxyBias);
                
                _logger.LogInformation("Macro analysis: Bias={Bias}, RiskMode={RiskMode}, VIX={VIX}",
                    result.Bias, result.RiskMode, result.VIXLevel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in macro analysis");
                result.RiskMode = RiskMode.BLOCKED;
            }

            return result;
        }

        public async Task<MacroBias> GetDailyBiasAsync(string symbol)
        {
            // Analisa tendência no Daily
            var candles = await _ninjaTraderService.GetHistoricalCandlesAsync(symbol, "1d", 
                DateTime.UtcNow.AddDays(-50), DateTime.UtcNow);
            
            if (candles.Count < 20)
                return MacroBias.NEUTRAL;

            var recent = candles.TakeLast(20).ToList();
            var ema20 = CalculateEMA(recent, 20);
            var ema50 = CalculateEMA(candles, 50);
            
            var lastPrice = recent[^1].Close;
            
            // Bullish se acima de ambas EMAs e EMA20 > EMA50
            if (lastPrice > ema20 && lastPrice > ema50 && ema20 > ema50)
                return MacroBias.BULLISH;
            
            // Bearish se abaixo de ambas EMAs e EMA20 < EMA50
            if (lastPrice < ema20 && lastPrice < ema50 && ema20 < ema50)
                return MacroBias.BEARISH;
            
            return MacroBias.NEUTRAL;
        }

        public async Task<RiskMode> GetRiskModeAsync()
        {
            var vixLevel = await GetVIXLevelAsync();
            
            // VIX > 30 = Alta volatilidade, bloqueia
            if (vixLevel > 30)
                return RiskMode.BLOCKED;
            
            // VIX 20-30 = Volatilidade elevada, reduz
            if (vixLevel > 20)
                return RiskMode.REDUCED;
            
            return RiskMode.NORMAL;
        }

        public async Task<Dictionary<string, decimal>> GetCorrelationsAsync(string symbol, List<string> referenceSymbols)
        {
            var correlations = new Dictionary<string, decimal>();
            
            // Pega dados dos últimos 30 dias
            var primaryData = await _ninjaTraderService.GetHistoricalCandlesAsync(symbol, "1h",
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
                var correlation = CalculateCorrelation(primaryReturns, refReturns);
                correlations[refSymbol] = correlation;
            }

            return correlations;
        }

        private async Task<decimal> GetVIXLevelAsync()
        {
            var candle = await _ninjaTraderService.GetLatestCandleAsync("VIX", "1d");
            return candle?.Close ?? 15m; // Default se não conseguir
        }

        private VolatilityRegime DetermineVolatilityRegime(decimal vixLevel)
        {
            if (vixLevel > 35) return VolatilityRegime.EXTREME;
            if (vixLevel > 25) return VolatilityRegime.HIGH;
            if (vixLevel < 12) return VolatilityRegime.LOW;
            return VolatilityRegime.NORMAL;
        }

        private bool DetermineRiskOnOff(MacroBias esBias, MacroBias dxyBias, decimal vixLevel)
        {
            // Risk-on: ES bullish, DXY bearish, VIX baixo
            var riskOnScore = 0;
            if (esBias == MacroBias.BULLISH) riskOnScore++;
            if (dxyBias == MacroBias.BEARISH) riskOnScore++;
            if (vixLevel < 18) riskOnScore++;
            
            return riskOnScore >= 2;
        }

        private MacroBias DetermineFinalBias(MacroBias esBias, bool isRiskOn, Dictionary<string, decimal> correlations)
        {
            // Se ES é bullish e correlação positiva com MNQ/NQ → Bullish
            if (esBias == MacroBias.BULLISH && isRiskOn)
                return MacroBias.BULLISH;
            
            if (esBias == MacroBias.BEARISH && !isRiskOn)
                return MacroBias.BEARISH;
            
            return MacroBias.NEUTRAL;
        }

        private decimal CalculateConfidence(MacroContextResult result)
        {
            var confidence = 50m;
            
            if (result.Bias != MacroBias.NEUTRAL) confidence += 20;
            if (result.VolatilityRegime == VolatilityRegime.NORMAL) confidence += 15;
            if (result.RiskMode == RiskMode.NORMAL) confidence += 15;
            
            return Math.Min(100, confidence);
        }

        private void AddObservations(MacroContextResult result, MacroBias esBias, MacroBias dxyBias)
        {
            result.Observations.Add($"ES Bias: {esBias}");
            result.Observations.Add($"DXY Bias: {dxyBias}");
            result.Observations.Add($"VIX: {result.VIXLevel:F2} ({result.VolatilityRegime})");
            result.Observations.Add($"Market: {(result.IsRiskOn ? "Risk-On" : "Risk-Off")}");
        }

        private decimal CalculateEMA(List<Candle> candles, int period)
        {
            if (candles.Count < period)
                return candles.Average(c => c.Close);

            var multiplier = 2m / (period + 1);
            var ema = candles.Take(period).Average(c => c.Close);

            foreach (var candle in candles.Skip(period))
            {
                ema = (candle.Close - ema) * multiplier + ema;
            }

            return ema;
        }

        private List<decimal> CalculateReturns(List<Candle> candles)
        {
            var returns = new List<decimal>();
            for (int i = 1; i < candles.Count; i++)
            {
                var ret = (candles[i].Close - candles[i - 1].Close) / candles[i - 1].Close;
                returns.Add(ret);
            }
            return returns;
        }

        private decimal CalculateCorrelation(List<decimal> x, List<decimal> y)
        {
            var n = Math.Min(x.Count, y.Count);
            if (n < 2) return 0;

            var xArray = x.Take(n).ToList();
            var yArray = y.Take(n).ToList();

            var xMean = xArray.Average();
            var yMean = yArray.Average();

            var numerator = 0m;
            var xSumSquares = 0m;
            var ySumSquares = 0m;

            for (int i = 0; i < n; i++)
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
