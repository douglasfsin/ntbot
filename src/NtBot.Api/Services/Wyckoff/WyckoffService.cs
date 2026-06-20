using NtBot.Domain.Entities;

namespace NtBot.Api.Services.Wyckoff
{
    /// <summary>
    /// Implementação completa do motor de análise Wyckoff
    /// </summary>
    public class WyckoffService : IWyckoffService
    {
        private readonly ILogger<WyckoffService> _logger;
        
        // Parâmetros configuráveis
        private const decimal SPRING_PENETRATION_PERCENT = 0.002m; // 0.2% de penetração
        private const decimal REJECTION_RATIO = 0.7m; // 70% de rejeição do candle
        private const int MIN_RANGE_CANDLES = 10;
        private const decimal RANGE_ATR_MULTIPLIER = 0.5m;
        private const int VOLUME_LOOKBACK = 20;

        public WyckoffService(ILogger<WyckoffService> logger)
        {
            _logger = logger;
        }

        public async Task<WyckoffAnalysisResult> AnalyzeAsync(string symbol, string timeframe, List<Candle> candles)
        {
            if (candles == null || candles.Count < 50)
            {
                return new WyckoffAnalysisResult
                {
                    Symbol = symbol,
                    Timeframe = timeframe,
                    AnalysisTime = DateTime.UtcNow,
                    Phase = WyckoffPhase.UNKNOWN,
                    Bias = MarketBias.NEUTRAL,
                    Observations = new List<string> { "Dados insuficientes para análise Wyckoff" }
                };
            }

            var result = new WyckoffAnalysisResult
            {
                Symbol = symbol,
                Timeframe = timeframe,
                AnalysisTime = DateTime.UtcNow
            };

            // 1. Calcula ATR para contexto de volatilidade
            var atr = CalculateATR(candles, 14);
            
            // 2. Identifica range atual
            var (isRange, rangeHigh, rangeLow, rangeCandles) = await IdentifyRangeAsync(candles);
            result.RangeHigh = rangeHigh;
            result.RangeLow = rangeLow;
            result.RangeCandles = rangeCandles;
            
            // 3. Analisa volume
            result.VolumeConfirmation = await IsVolumeDivergentAsync(candles);
            var latestCandle = candles[^1];
            result.CurrentVolume = latestCandle.Volume;
            result.AverageVolume = (decimal)candles.TakeLast(VOLUME_LOOKBACK).Average(c => c.Volume);
            
            if (latestCandle.Delta.HasValue)
            {
                result.VolumeDeltaAverage = (decimal)candles
                    .Where(c => c.Delta.HasValue)
                    .TakeLast(VOLUME_LOOKBACK)
                    .Average(c => c.Delta!.Value);
            }
            
            // 4. Identifica níveis estruturais
            result.KeyLevels = await IdentifyStructureLevelsAsync(candles);
            
            // 5. Detecta fase
            result.Phase = DetectPhase(candles, isRange, rangeCandles, atr);
            result.PhaseConfidence = CalculatePhaseConfidence(result.Phase, candles, isRange);
            
            // 6. Detecta eventos específicos
            await DetectEventsAsync(result, candles, isRange, rangeHigh, rangeLow);
            
            // 7. Determina bias
            result.Bias = DetermineBias(candles, result.Phase, result.Event);
            
            // 8. Adiciona observações
            AddObservations(result, candles, isRange, atr);
            
            _logger.LogInformation(
                "Wyckoff analysis for {Symbol} {Timeframe}: Phase={Phase}, Event={Event}, Bias={Bias}",
                symbol, timeframe, result.Phase, result.Event, result.Bias);

            return result;
        }

        public async Task<(bool detected, decimal confidence)> DetectSpringAsync(List<Candle> candles)
        {
            if (candles.Count < 20) return (false, 0);

            var recentCandles = candles.TakeLast(10).ToList();
            var priorLow = candles.TakeLast(50).Min(c => c.Low);
            var latestCandle = recentCandles[^1];

            // Critérios para Spring:
            // 1. Penetração abaixo do low anterior
            // 2. Fechamento acima do low (rejeição)
            // 3. Volume acima da média
            // 4. Delta positivo (se disponível)

            var penetratedLow = latestCandle.Low < priorLow * (1 - SPRING_PENETRATION_PERCENT);
            var rejectedBack = latestCandle.Close > priorLow;
            var wickRatio = (decimal)((latestCandle.Close - latestCandle.Low) / (latestCandle.High - latestCandle.Low));
            var strongRejection = wickRatio >= REJECTION_RATIO;

            var avgVolume = candles.TakeLast(VOLUME_LOOKBACK).Average(c => c.Volume);
            var highVolume = latestCandle.Volume > avgVolume * 1.2;

            var positiveDelta = latestCandle.Delta.HasValue && latestCandle.Delta.Value > 0;

            // Cálculo de confiança
            var confidence = 0m;
            if (penetratedLow) confidence += 25;
            if (rejectedBack) confidence += 25;
            if (strongRejection) confidence += 20;
            if (highVolume) confidence += 15;
            if (positiveDelta) confidence += 15;

            var detected = confidence >= 60; // Mínimo 60% de confiança

            _logger.LogDebug("Spring detection: {Detected} (confidence: {Confidence}%)", detected, confidence);

            return await Task.FromResult((detected, confidence));
        }

        public async Task<(bool detected, decimal confidence)> DetectUpthrustAsync(List<Candle> candles)
        {
            if (candles.Count < 20) return (false, 0);

            var recentCandles = candles.TakeLast(10).ToList();
            var priorHigh = candles.TakeLast(50).Max(c => c.High);
            var latestCandle = recentCandles[^1];

            // Critérios para Upthrust (inverso do Spring):
            // 1. Penetração acima do high anterior
            // 2. Fechamento abaixo do high (rejeição)
            // 3. Volume acima da média
            // 4. Delta negativo (se disponível)

            var penetratedHigh = latestCandle.High > priorHigh * (1 + SPRING_PENETRATION_PERCENT);
            var rejectedBack = latestCandle.Close < priorHigh;
            var wickRatio = (decimal)((latestCandle.High - latestCandle.Close) / (latestCandle.High - latestCandle.Low));
            var strongRejection = wickRatio >= REJECTION_RATIO;

            var avgVolume = candles.TakeLast(VOLUME_LOOKBACK).Average(c => c.Volume);
            var highVolume = latestCandle.Volume > avgVolume * 1.2;

            var negativeDelta = latestCandle.Delta.HasValue && latestCandle.Delta.Value < 0;

            // Cálculo de confiança
            var confidence = 0m;
            if (penetratedHigh) confidence += 25;
            if (rejectedBack) confidence += 25;
            if (strongRejection) confidence += 20;
            if (highVolume) confidence += 15;
            if (negativeDelta) confidence += 15;

            var detected = confidence >= 60;

            _logger.LogDebug("Upthrust detection: {Detected} (confidence: {Confidence}%)", detected, confidence);

            return await Task.FromResult((detected, confidence));
        }

        public async Task<(bool isRange, decimal high, decimal low, int candles)> IdentifyRangeAsync(List<Candle> candles)
        {
            if (candles.Count < MIN_RANGE_CANDLES * 2)
                return (false, 0, 0, 0);

            var lookback = Math.Min(100, candles.Count);
            var recentCandles = candles.TakeLast(lookback).ToList();
            var atr = CalculateATR(recentCandles, 14);

            // Procura por range (lateralização)
            // Critérios:
            // 1. Highs e Lows próximos (dentro de ATR * multiplicador)
            // 2. Mínimo de X candles no range
            // 3. ATR decrescente (volatilidade caindo)

            var high = recentCandles.Take(MIN_RANGE_CANDLES).Max(c => c.High);
            var low = recentCandles.Take(MIN_RANGE_CANDLES).Min(c => c.Low);
            var rangeSize = high - low;
            
            var maxRangeSize = atr * 2m; // Range pode ter no máximo 2x o ATR
            
            if (rangeSize > maxRangeSize)
                return await Task.FromResult((false, 0, 0, 0));

            // Conta quantos candles estão dentro do range
            var candlesInRange = 0;
            foreach (var candle in recentCandles)
            {
                if (candle.High <= high * 1.01m && candle.Low >= low * 0.99m)
                {
                    candlesInRange++;
                    // Atualiza high/low se necessário
                    if (candle.High > high) high = candle.High;
                    if (candle.Low < low) low = candle.Low;
                }
                else
                {
                    // Saiu do range, reinicia contagem
                    if (candlesInRange >= MIN_RANGE_CANDLES)
                        break;
                    candlesInRange = 0;
                    high = candle.High;
                    low = candle.Low;
                }
            }

            var isRange = candlesInRange >= MIN_RANGE_CANDLES;

            _logger.LogDebug("Range identification: {IsRange} (candles: {Candles}, high: {High}, low: {Low})",
                isRange, candlesInRange, high, low);

            return await Task.FromResult((isRange, high, low, candlesInRange));
        }

        public async Task<bool> IsVolumeDivergentAsync(List<Candle> candles)
        {
            if (candles.Count < VOLUME_LOOKBACK * 2)
                return false;

            var recentCandles = candles.TakeLast(VOLUME_LOOKBACK).ToList();
            var avgVolume = candles.TakeLast(VOLUME_LOOKBACK * 2).Average(c => c.Volume);

            // Divergência: preço fazendo novos highs/lows mas volume está caindo
            var recentHigh = recentCandles.Max(c => c.High);
            var priorHigh = candles.TakeLast(VOLUME_LOOKBACK * 2).Take(VOLUME_LOOKBACK).Max(c => c.High);
            
            var recentLow = recentCandles.Min(c => c.Low);
            var priorLow = candles.TakeLast(VOLUME_LOOKBACK * 2).Take(VOLUME_LOOKBACK).Min(c => c.Low);

            var recentAvgVolume = recentCandles.Average(c => c.Volume);
            var priorAvgVolume = candles.TakeLast(VOLUME_LOOKBACK * 2).Take(VOLUME_LOOKBACK).Average(c => c.Volume);

            // Divergência de alta: novo low mas volume menor
            var bullishDivergence = recentLow < priorLow && recentAvgVolume < priorAvgVolume * 0.8;

            // Divergência de baixa: novo high mas volume menor
            var bearishDivergence = recentHigh > priorHigh && recentAvgVolume < priorAvgVolume * 0.8;

            var divergent = bullishDivergence || bearishDivergence;

            _logger.LogDebug("Volume divergence: {Divergent} (bullish: {Bullish}, bearish: {Bearish})",
                divergent, bullishDivergence, bearishDivergence);

            return await Task.FromResult(divergent);
        }

        public async Task<List<StructureLevel>> IdentifyStructureLevelsAsync(List<Candle> candles)
        {
            var levels = new List<StructureLevel>();
            
            if (candles.Count < 50)
                return levels;

            // Identifica swing highs e swing lows
            var swingHighs = new List<(decimal price, DateTime time)>();
            var swingLows = new List<(decimal price, DateTime time)>();

            for (int i = 5; i < candles.Count - 5; i++)
            {
                var candle = candles[i];
                var priorHigh = candles.Skip(i - 5).Take(5).Max(c => c.High);
                var nextHigh = candles.Skip(i + 1).Take(5).Max(c => c.High);
                
                if (candle.High >= priorHigh && candle.High >= nextHigh)
                {
                    swingHighs.Add((candle.High, candle.OpenTime));
                }

                var priorLow = candles.Skip(i - 5).Take(5).Min(c => c.Low);
                var nextLow = candles.Skip(i + 1).Take(5).Min(c => c.Low);
                
                if (candle.Low <= priorLow && candle.Low <= nextLow)
                {
                    swingLows.Add((candle.Low, candle.OpenTime));
                }
            }

            // Agrupa níveis próximos (dentro de 0.5%)
            var groupedHighs = GroupNearbyLevels(swingHighs, 0.005m);
            var groupedLows = GroupNearbyLevels(swingLows, 0.005m);

            // Converte para StructureLevel
            foreach (var group in groupedHighs)
            {
                levels.Add(new StructureLevel
                {
                    Type = "RESISTANCE",
                    Price = group.avgPrice,
                    Touches = group.touches.Count,
                    FirstTouch = group.touches.Min(t => t.time),
                    LastTouch = group.touches.Max(t => t.time),
                    Strength = Math.Min(100, group.touches.Count * 20) // Mais toques = mais forte
                });
            }

            foreach (var group in groupedLows)
            {
                levels.Add(new StructureLevel
                {
                    Type = "SUPPORT",
                    Price = group.avgPrice,
                    Touches = group.touches.Count,
                    FirstTouch = group.touches.Min(t => t.time),
                    LastTouch = group.touches.Max(t => t.time),
                    Strength = Math.Min(100, group.touches.Count * 20)
                });
            }

            _logger.LogDebug("Identified {Count} structure levels", levels.Count);

            return await Task.FromResult(levels.OrderByDescending(l => l.Strength).ToList());
        }

        #region Helper Methods

        private decimal CalculateATR(List<Candle> candles, int period)
        {
            if (candles.Count < period + 1)
                return 0;

            var trueRanges = new List<decimal>();
            for (int i = 1; i < candles.Count; i++)
            {
                var current = candles[i];
                var previous = candles[i - 1];
                
                var tr = Math.Max(
                    current.High - current.Low,
                    Math.Max(
                        Math.Abs(current.High - previous.Close),
                        Math.Abs(current.Low - previous.Close)
                    )
                );
                
                trueRanges.Add(tr);
            }

            return trueRanges.TakeLast(period).Average();
        }

        private WyckoffPhase DetectPhase(List<Candle> candles, bool isRange, int rangeCandles, decimal atr)
        {
            if (!isRange)
            {
                // Não está em range, está em tendência
                var trendCandles = candles.TakeLast(20).ToList();
                var priceChange = trendCandles[^1].Close - trendCandles[0].Close;
                
                if (priceChange > atr * 3)
                    return WyckoffPhase.MARKUP;
                else if (priceChange < -atr * 3)
                    return WyckoffPhase.MARKDOWN;
                else
                    return WyckoffPhase.RANGING;
            }

            // Está em range, precisa identificar se é acumulação ou distribuição
            // Usa volume profile e delta para determinar
            var recentCandles = candles.TakeLast(rangeCandles).ToList();
            var totalBuyVolume = recentCandles.Where(c => c.BuyVolume.HasValue).Sum(c => c.BuyVolume!.Value);
            var totalSellVolume = recentCandles.Where(c => c.SellVolume.HasValue).Sum(c => c.SellVolume!.Value);

            if (totalBuyVolume > totalSellVolume * 1.2m)
                return WyckoffPhase.ACCUMULATION;
            else if (totalSellVolume > totalBuyVolume * 1.2m)
                return WyckoffPhase.DISTRIBUTION;
            else
                return WyckoffPhase.RANGING;
        }

        private decimal CalculatePhaseConfidence(WyckoffPhase phase, List<Candle> candles, bool isRange)
        {
            // Confiança baseada em:
            // - Clareza do range
            // - Volume profile consistency
            // - Número de candles na fase
            
            if (phase == WyckoffPhase.UNKNOWN)
                return 0;

            var confidence = 50m; // Base

            if (isRange)
                confidence += 20;

            // Adiciona confiança se volume está consistente
            var recentVolumes = candles.TakeLast(10).Select(c => c.Volume).ToList();
            var volumeStdDev = CalculateStandardDeviation(recentVolumes);
            var volumeAvg = recentVolumes.Average();
            
            if ((decimal)volumeStdDev < (decimal)volumeAvg * 0.3m) // Volume consistente
                confidence += 15;

            return Math.Min(100, confidence);
        }

        private async Task DetectEventsAsync(WyckoffAnalysisResult result, List<Candle> candles, bool isRange, decimal rangeHigh, decimal rangeLow)
        {
            if (!isRange)
                return;

            // Detecta Spring
            var (springDetected, springConfidence) = await DetectSpringAsync(candles);
            if (springDetected && springConfidence > result.EventConfidence)
            {
                result.Event = WyckoffEvent.SPRING;
                result.EventConfidence = springConfidence;
            }

            // Detecta Upthrust
            var (upthrustDetected, upthrustConfidence) = await DetectUpthrustAsync(candles);
            if (upthrustDetected && upthrustConfidence > result.EventConfidence)
            {
                result.Event = WyckoffEvent.UPTHRUST;
                result.EventConfidence = upthrustConfidence;
            }

            // Detecta BC/SC (Buying/Selling Climax) - volume extremo
            var latestCandle = candles[^1];
            var avgVolume = candles.TakeLast(50).Average(c => c.Volume);
            
            if (latestCandle.Volume > avgVolume * 2.5)
            {
                var isDown = latestCandle.Close < latestCandle.Open;
                if (isDown && latestCandle.Delta.HasValue && latestCandle.Delta.Value < 0)
                {
                    result.Event = WyckoffEvent.SC;
                    result.EventConfidence = 75;
                }
                else if (!isDown && latestCandle.Delta.HasValue && latestCandle.Delta.Value > 0)
                {
                    result.Event = WyckoffEvent.BC;
                    result.EventConfidence = 75;
                }
            }
        }

        private MarketBias DetermineBias(List<Candle> candles, WyckoffPhase phase, WyckoffEvent? wyckoffEvent)
        {
            // Bias baseado em fase e evento
            if (phase == WyckoffPhase.ACCUMULATION || phase == WyckoffPhase.MARKUP)
                return MarketBias.BULLISH;
            
            if (phase == WyckoffPhase.DISTRIBUTION || phase == WyckoffPhase.MARKDOWN)
                return MarketBias.BEARISH;

            // Se detectou Spring → Bullish
            if (wyckoffEvent == WyckoffEvent.SPRING || wyckoffEvent == WyckoffEvent.SOS)
                return MarketBias.BULLISH;

            // Se detectou Upthrust → Bearish
            if (wyckoffEvent == WyckoffEvent.UPTHRUST || wyckoffEvent == WyckoffEvent.SOW)
                return MarketBias.BEARISH;

            return MarketBias.NEUTRAL;
        }

        private void AddObservations(WyckoffAnalysisResult result, List<Candle> candles, bool isRange, decimal atr)
        {
            if (isRange)
            {
                result.Observations.Add($"Mercado em range há {result.RangeCandles} candles");
                result.Observations.Add($"Range: {result.RangeLow:F2} - {result.RangeHigh:F2}");
            }

            if (result.VolumeConfirmation)
            {
                result.Observations.Add("Divergência de volume detectada");
            }

            if (result.EventConfidence > 70)
            {
                result.Observations.Add($"Evento Wyckoff {result.Event} com alta confiança ({result.EventConfidence:F0}%)");
            }

            result.Observations.Add($"ATR (14): {atr:F2}");
            result.Observations.Add($"Níveis estruturais identificados: {result.KeyLevels.Count}");
        }

        private List<(decimal avgPrice, List<(decimal price, DateTime time)> touches)> GroupNearbyLevels(
            List<(decimal price, DateTime time)> levels, decimal threshold)
        {
            var groups = new List<(decimal avgPrice, List<(decimal price, DateTime time)> touches)>();
            var sorted = levels.OrderBy(l => l.price).ToList();

            foreach (var level in sorted)
            {
                var existingGroup = groups.FirstOrDefault(g => 
                    Math.Abs(g.avgPrice - level.price) / level.price <= threshold);

                if (existingGroup.touches != null)
                {
                    existingGroup.touches.Add(level);
                    // Recalcula média
                    var index = groups.IndexOf(existingGroup);
                    groups[index] = (existingGroup.touches.Average(t => t.price), existingGroup.touches);
                }
                else
                {
                    groups.Add((level.price, new List<(decimal, DateTime)> { level }));
                }
            }

            return groups.Where(g => g.touches.Count >= 2).ToList(); // Apenas níveis com 2+ toques
        }

        private decimal CalculateStandardDeviation(List<long> values)
        {
            var avg = values.Average();
            var sumOfSquares = values.Sum(v => Math.Pow((double)(v - avg), 2));
            return (decimal)Math.Sqrt(sumOfSquares / values.Count);
        }

        #endregion
    }
}
