using NtBot.Domain.Entities;
using NtBot.Api.Services.Correlation;
using NtBot.Api.Services.GammaExposure;
using NtBot.Api.Services.Wyckoff;

namespace NtBot.Api.Strategies
{
    /// <summary>
    /// Estratégia Quantitativa integrando:
    /// - Correlação NQ/WIN (Global Bias)
    /// - Gamma Exposure (GEX)
    /// - Wyckoff (Estrutura de Mercado)
    /// </summary>
    public class QuantStrategy
    {
        private readonly ILogger<QuantStrategy> _logger;
        private readonly IGlobalCorrelationService _correlationService;
        private readonly IGammaExposureService _gexService;
        private readonly IWyckoffService _wyckoffService;

        // Parâmetros de risco
        private const decimal ATR_STOP_MULTIPLIER = 2.0m;
        private const decimal ATR_TP1_MULTIPLIER = 2.5m;
        private const decimal ATR_TP2_MULTIPLIER = 4.0m;
        private const decimal PARTIAL_EXIT_PERCENT = 0.5m; // 50% no TP1

        // Thresholds para sinais
        private const decimal MIN_CORRELATION = 0.6m; // Mínimo 60% de correlação
        private const decimal MIN_CONFIDENCE = 70m; // Mínimo 70% de confiança
        private const decimal MIN_TREND_STRENGTH = 25m; // ADX > 25 para breakout

        public QuantStrategy(
            ILogger<QuantStrategy> logger,
            IGlobalCorrelationService correlationService,
            IGammaExposureService gexService,
            IWyckoffService wyckoffService)
        {
            _logger = logger;
            _correlationService = correlationService;
            _gexService = gexService;
            _wyckoffService = wyckoffService;
        }

        public string Name => "QuantStrategy - GEX + Correlation + Wyckoff";
        public string Description => "Estratégia quantitativa integrando correlação global, gamma exposure e estrutura Wyckoff";

        /// <summary>
        /// Analisa mercado e gera sinal de trading
        /// </summary>
        public async Task<QuantSignal?> AnalyzeAsync(
            string symbol,
            List<Candle> candles,
            string leaderSymbol = "NQ",
            List<Candle>? leaderCandles = null)
        {
            try
            {
                _logger.LogInformation("Iniciando análise QuantStrategy para {Symbol}", symbol);

                // 1. Obtém dados do líder global (NQ) se não fornecido
                if (leaderCandles == null || !leaderCandles.Any())
                {
                    leaderCandles = await _correlationService.GetLeaderDataAsync(leaderSymbol, 100);
                }

                // 2. Calcula correlação NQ/WIN
                var correlation = await _correlationService.CalculateCorrelationAsync(
                    leaderSymbol,
                    symbol,
                    leaderCandles,
                    candles,
                    lookbackPeriod: 50
                );

                _logger.LogInformation("Correlação {Leader}/{Follower}: {Corr:F2} | Bias: {Bias}",
                    leaderSymbol, symbol, correlation.PearsonCorrelation, correlation.LeaderBias);

                // 3. Obtém dados de opções e calcula GEX
                var currentPrice = candles.OrderBy(c => c.CloseTime).Last().Close;
                var optionsData = await _gexService.GetOptionsDataAsync(symbol);
                var gexData = await _gexService.CalculateGEXAsync(symbol, currentPrice, optionsData);

                _logger.LogInformation("GEX para {Symbol}: {TotalGEX:F0} | Regime: {Regime}",
                    symbol, gexData.TotalGEX, gexData.Regime);

                // 4. Analisa Wyckoff
                var wyckoffAnalysis = await _wyckoffService.AnalyzeAsync(symbol, "5m", candles);

                _logger.LogInformation("Wyckoff {Symbol}: Fase={Phase} | Bias={Bias}",
                    symbol, wyckoffAnalysis.Phase, wyckoffAnalysis.Bias);

                // 5. Verifica se há boa correlação
                if (Math.Abs(correlation.PearsonCorrelation) < MIN_CORRELATION)
                {
                    _logger.LogWarning("Correlação insuficiente: {Corr:F2} < {Min}",
                        correlation.PearsonCorrelation, MIN_CORRELATION);
                    return null;
                }

                // 6. Calcula ATR para gestão de risco
                var atr = CalculateATR(candles, 14);

                // 7. Aplica regras de entrada
                var signal = EvaluateEntryRules(
                    symbol,
                    correlation,
                    gexData,
                    wyckoffAnalysis,
                    currentPrice,
                    atr
                );

                if (signal != null)
                {
                    _logger.LogInformation("✅ Sinal gerado: {Direction} {Symbol} @ {Price} | Confiança: {Conf}%",
                        signal.Direction, symbol, signal.EntryPrice, signal.ConfidenceScore);
                }
                else
                {
                    _logger.LogInformation("❌ Nenhum sinal gerado para {Symbol}", symbol);
                }

                return signal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar estratégia para {Symbol}", symbol);
                throw;
            }
        }

        #region Entry Rules

        private QuantSignal? EvaluateEntryRules(
            string symbol,
            CorrelationData correlation,
            GammaExposureData gex,
            WyckoffAnalysisResult wyckoff,
            decimal currentPrice,
            decimal atr)
        {
            // Estratégia 1: BREAKOUT (Continuação de Tendência)
            var breakoutSignal = EvaluateBreakoutStrategy(
                symbol, correlation, gex, wyckoff, currentPrice, atr);

            if (breakoutSignal != null && breakoutSignal.ConfidenceScore >= MIN_CONFIDENCE)
                return breakoutSignal;

            // Estratégia 2: MEAN REVERSION (Reversão à Média)
            var meanReversionSignal = EvaluateMeanReversionStrategy(
                symbol, correlation, gex, wyckoff, currentPrice, atr);

            if (meanReversionSignal != null && meanReversionSignal.ConfidenceScore >= MIN_CONFIDENCE)
                return meanReversionSignal;

            return null;
        }

        /// <summary>
        /// Regra de Entrada: BREAKOUT
        /// - GlobalBias = Bullish/Bearish
        /// - GEX negativo (mercado com potencial de expansão)
        /// - Preço rompendo Gamma Wall
        /// - Wyckoff em fase de acumulação/distribuição com breakout
        /// </summary>
        private QuantSignal? EvaluateBreakoutStrategy(
            string symbol,
            CorrelationData correlation,
            GammaExposureData gex,
            WyckoffAnalysisResult wyckoff,
            decimal currentPrice,
            decimal atr)
        {
            // Condições para LONG Breakout
            if (correlation.LeaderBias == GlobalBias.BULLISH &&
                (gex.Regime == GEXRegime.NEGATIVE_HIGH || gex.Regime == GEXRegime.NEGATIVE_LOW) &&
                correlation.TrendStrength >= MIN_TREND_STRENGTH &&
                (wyckoff.Phase == WyckoffPhase.ACCUMULATION || wyckoff.Phase == WyckoffPhase.MARKUP) &&
                wyckoff.Bias == MarketBias.BULLISH)
            {
                // Verifica se rompeu resistência (gamma wall)
                var resistanceWall = gex.GammaWalls
                    .Where(w => w.Type == "Resistance" && w.Strike > currentPrice)
                    .OrderBy(w => w.Strike)
                    .FirstOrDefault();

                var aboveResistance = resistanceWall == null || 
                    Math.Abs(resistanceWall.Distance) < 0.5m; // Muito próximo ou acima

                if (aboveResistance)
                {
                    return CreateSignal(
                        symbol,
                        SignalDirection.LONG,
                        StrategyType.BREAKOUT,
                        correlation,
                        gex,
                        wyckoff,
                        currentPrice,
                        atr
                    );
                }
            }

            // Condições para SHORT Breakout
            if (correlation.LeaderBias == GlobalBias.BEARISH &&
                (gex.Regime == GEXRegime.NEGATIVE_HIGH || gex.Regime == GEXRegime.NEGATIVE_LOW) &&
                correlation.TrendStrength >= MIN_TREND_STRENGTH &&
                (wyckoff.Phase == WyckoffPhase.DISTRIBUTION || wyckoff.Phase == WyckoffPhase.MARKDOWN) &&
                wyckoff.Bias == MarketBias.BEARISH)
            {
                // Verifica se rompeu suporte (gamma wall)
                var supportWall = gex.GammaWalls
                    .Where(w => w.Type == "Support" && w.Strike < currentPrice)
                    .OrderByDescending(w => w.Strike)
                    .FirstOrDefault();

                var belowSupport = supportWall == null || 
                    Math.Abs(supportWall.Distance) < 0.5m;

                if (belowSupport)
                {
                    return CreateSignal(
                        symbol,
                        SignalDirection.SHORT,
                        StrategyType.BREAKOUT,
                        correlation,
                        gex,
                        wyckoff,
                        currentPrice,
                        atr
                    );
                }
            }

            return null;
        }

        /// <summary>
        /// Regra de Entrada: MEAN REVERSION
        /// - GEX positivo (mercado com tendência a reversão)
        /// - Preço próximo de Gamma Wall
        /// - Entrar contra o movimento recente
        /// </summary>
        private QuantSignal? EvaluateMeanReversionStrategy(
            string symbol,
            CorrelationData correlation,
            GammaExposureData gex,
            WyckoffAnalysisResult wyckoff,
            decimal currentPrice,
            decimal atr)
        {
            // Só opera mean reversion com GEX positivo
            if (gex.Regime != GEXRegime.POSITIVE_HIGH && gex.Regime != GEXRegime.POSITIVE_LOW)
                return null;

            // Busca gamma wall mais próxima
            var nearestWall = gex.GammaWalls
                .OrderBy(w => Math.Abs(w.Distance))
                .FirstOrDefault();

            if (nearestWall == null || Math.Abs(nearestWall.Distance) > 1.0m)
                return null; // Muito longe da wall

            // LONG Mean Reversion: preço próximo de suporte, momentum negativo recente
            if (nearestWall.Type == "Support" && 
                correlation.LeaderMomentum < 0 &&
                wyckoff.Bias != MarketBias.BEARISH) // Não vai contra tendência forte
            {
                return CreateSignal(
                    symbol,
                    SignalDirection.LONG,
                    StrategyType.MEAN_REVERSION,
                    correlation,
                    gex,
                    wyckoff,
                    currentPrice,
                    atr
                );
            }

            // SHORT Mean Reversion: preço próximo de resistência, momentum positivo recente
            if (nearestWall.Type == "Resistance" && 
                correlation.LeaderMomentum > 0 &&
                wyckoff.Bias != MarketBias.BULLISH)
            {
                return CreateSignal(
                    symbol,
                    SignalDirection.SHORT,
                    StrategyType.MEAN_REVERSION,
                    correlation,
                    gex,
                    wyckoff,
                    currentPrice,
                    atr
                );
            }

            return null;
        }

        #endregion

        #region Signal Creation

        private QuantSignal CreateSignal(
            string symbol,
            SignalDirection direction,
            StrategyType strategyType,
            CorrelationData correlation,
            GammaExposureData gex,
            WyckoffAnalysisResult wyckoff,
            decimal currentPrice,
            decimal atr)
        {
            // Calcula níveis de stop e take profit
            var (stopLoss, takeProfit1, takeProfit2) = CalculateRiskLevels(
                currentPrice,
                direction,
                atr,
                strategyType
            );

            // Calcula score de confiança
            var confidenceScore = CalculateConfidenceScore(
                correlation,
                gex,
                wyckoff,
                strategyType
            );

            var signal = new QuantSignal
            {
                Id = Guid.NewGuid(),
                Symbol = symbol,
                GlobalBias = correlation.LeaderBias,
                GEXRegime = gex.Regime,
                WyckoffPhase = wyckoff.Phase.ToString(),
                Direction = direction,
                StrategyType = strategyType,
                ConfidenceScore = confidenceScore,
                CorrelationStrength = Math.Abs(correlation.PearsonCorrelation) * 100,
                GEXAlignment = strategyType == StrategyType.BREAKOUT 
                    ? gex.ExpansionPotential 
                    : gex.MeanReversionPotential,
                WyckoffAlignment = CalculateWyckoffAlignment(wyckoff, direction),
                EntryPrice = currentPrice,
                StopLoss = stopLoss,
                TakeProfit1 = takeProfit1,
                TakeProfit2 = takeProfit2,
                ATRValue = atr,
                RiskRewardRatio = Math.Abs((takeProfit2 - currentPrice) / (currentPrice - stopLoss)),
                Description = $"{strategyType} {direction} based on {correlation.LeaderBias} bias, GEX {gex.Regime}, Wyckoff {wyckoff.Phase}",
                Status = SignalStatus.PENDING,
                CreatedAt = DateTime.UtcNow
            };

            // Observações para contexto
            signal.Observations.Add($"Correlação NQ/WIN: {correlation.PearsonCorrelation:F2}");
            signal.Observations.Add($"GEX Total: {gex.TotalGEX:F0}");
            signal.Observations.Add($"Gamma Flip: {gex.GammaFlipLevel?.ToString() ?? "N/A"}");
            signal.Observations.Add($"Wyckoff Phase: {wyckoff.Phase}");
            signal.Observations.Add($"Trend Strength: {correlation.TrendStrength:F1}");

            return signal;
        }

        private (decimal stopLoss, decimal tp1, decimal tp2) CalculateRiskLevels(
            decimal currentPrice,
            SignalDirection direction,
            decimal atr,
            StrategyType strategyType)
        {
            // Mean reversion usa stops mais apertados
            var stopMultiplier = strategyType == StrategyType.MEAN_REVERSION 
                ? ATR_STOP_MULTIPLIER * 0.7m 
                : ATR_STOP_MULTIPLIER;

            decimal stopLoss, tp1, tp2;

            if (direction == SignalDirection.LONG)
            {
                stopLoss = currentPrice - (atr * stopMultiplier);
                tp1 = currentPrice + (atr * ATR_TP1_MULTIPLIER);
                tp2 = currentPrice + (atr * ATR_TP2_MULTIPLIER);
            }
            else // SHORT
            {
                stopLoss = currentPrice + (atr * stopMultiplier);
                tp1 = currentPrice - (atr * ATR_TP1_MULTIPLIER);
                tp2 = currentPrice - (atr * ATR_TP2_MULTIPLIER);
            }

            return (stopLoss, tp1, tp2);
        }

        private decimal CalculateConfidenceScore(
            CorrelationData correlation,
            GammaExposureData gex,
            WyckoffAnalysisResult wyckoff,
            StrategyType strategyType)
        {
            decimal score = 0;

            // Peso: Correlação (30%)
            var corrScore = Math.Abs(correlation.PearsonCorrelation) * 30;
            score += corrScore;

            // Peso: GEX Alignment (30%)
            var gexScore = strategyType == StrategyType.BREAKOUT 
                ? gex.ExpansionPotential * 0.3m 
                : gex.MeanReversionPotential * 0.3m;
            score += gexScore;

            // Peso: Wyckoff (20%)
            var wyckoffScore = wyckoff.PhaseConfidence * 0.2m;
            score += wyckoffScore;

            // Peso: Trend Strength (20%)
            var trendScore = strategyType == StrategyType.BREAKOUT 
                ? (correlation.TrendStrength / 100) * 20 
                : (1 - correlation.TrendStrength / 100) * 20;
            score += trendScore;

            return Math.Max(0, Math.Min(100, score));
        }

        private decimal CalculateWyckoffAlignment(WyckoffAnalysisResult wyckoff, SignalDirection direction)
        {
            var alignment = 0m;

            if (direction == SignalDirection.LONG)
            {
                if (wyckoff.Bias == MarketBias.BULLISH) alignment += 50;
                if (wyckoff.Phase == WyckoffPhase.ACCUMULATION || wyckoff.Phase == WyckoffPhase.MARKUP)
                    alignment += 50;
            }
            else if (direction == SignalDirection.SHORT)
            {
                if (wyckoff.Bias == MarketBias.BEARISH) alignment += 50;
                if (wyckoff.Phase == WyckoffPhase.DISTRIBUTION || wyckoff.Phase == WyckoffPhase.MARKDOWN)
                    alignment += 50;
            }

            return alignment;
        }

        #endregion

        #region Utilities

        private decimal CalculateATR(List<Candle> candles, int period)
        {
            if (candles.Count < period + 1)
                return 0;

            var ordered = candles.OrderBy(c => c.CloseTime).ToList();
            var trueRanges = new List<decimal>();

            for (int i = 1; i < ordered.Count; i++)
            {
                var high = ordered[i].High;
                var low = ordered[i].Low;
                var previousClose = ordered[i - 1].Close;

                var tr = Math.Max(
                    high - low,
                    Math.Max(
                        Math.Abs(high - previousClose),
                        Math.Abs(low - previousClose)
                    )
                );

                trueRanges.Add(tr);
            }

            return trueRanges.TakeLast(period).Average();
        }

        #endregion
    }
}
