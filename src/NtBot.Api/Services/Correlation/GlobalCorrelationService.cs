using NtBot.Domain.Entities;

namespace NtBot.Api.Services.Correlation
{
    /// <summary>
    /// Serviço para análise de correlação entre NQ (Nasdaq) e WIN (Ibovespa)
    /// </summary>
    public class GlobalCorrelationService : IGlobalCorrelationService
    {
        private readonly ILogger<GlobalCorrelationService> _logger;
        private readonly HttpClient _httpClient;

        public GlobalCorrelationService(
            ILogger<GlobalCorrelationService> logger,
            HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Calcula correlação entre NQ e WIN
        /// </summary>
        public async Task<CorrelationData> CalculateCorrelationAsync(
            string leaderSymbol,
            string followerSymbol,
            List<Candle> leaderCandles,
            List<Candle> followerCandles,
            int lookbackPeriod = 50)
        {
            try
            {
                if (leaderCandles.Count < lookbackPeriod || followerCandles.Count < lookbackPeriod)
                {
                    _logger.LogWarning("Dados insuficientes para calcular correlação");
                    return new CorrelationData
                    {
                        Symbol1 = leaderSymbol,
                        Symbol2 = followerSymbol,
                        PearsonCorrelation = 0,
                        LeaderBias = GlobalBias.NEUTRAL
                    };
                }

                // Alinha dados por timestamp
                var alignedData = AlignTimeframes(leaderCandles, followerCandles, lookbackPeriod);

                // Calcula correlação de Pearson
                var pearson = CalculatePearsonCorrelation(alignedData.leader, alignedData.follower);
                var spearman = CalculateSpearmanCorrelation(alignedData.leader, alignedData.follower);

                // Analisa direção e momentum do líder (NQ)
                var leaderBias = DetermineLeaderBias(leaderCandles);
                var leaderMomentum = CalculateMomentum(leaderCandles, 20);
                var ema20 = CalculateEMA(leaderCandles, 20);
                var ema50 = CalculateEMA(leaderCandles, 50);

                // Força da tendência baseada em ADX ou similar
                var trendStrength = CalculateTrendStrength(leaderCandles);

                return new CorrelationData
                {
                    Id = Guid.NewGuid(),
                    Symbol1 = leaderSymbol,
                    Symbol2 = followerSymbol,
                    PearsonCorrelation = pearson,
                    SpearmanCorrelation = spearman,
                    LookbackPeriod = lookbackPeriod,
                    LeaderBias = leaderBias,
                    LeaderMomentum = leaderMomentum,
                    LeaderEMA20 = ema20,
                    LeaderEMA50 = ema50,
                    TrendStrength = trendStrength,
                    CalculatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular correlação entre {Leader} e {Follower}",
                    leaderSymbol, followerSymbol);
                throw;
            }
        }

        /// <summary>
        /// Obtém dados históricos do NQ (líder global)
        /// </summary>
        public async Task<List<Candle>> GetLeaderDataAsync(string symbol, int periods, string timeframe = "5m")
        {
            try
            {
                // TODO: Integrar com fonte de dados real (NinjaTrader, Yahoo Finance, etc.)
                // Por enquanto, retorna estrutura mock
                _logger.LogInformation("Obtendo dados de {Symbol} para {Periods} períodos", symbol, periods);

                // Implementação real conectaria com:
                // - NinjaTrader via ATI
                // - Yahoo Finance API
                // - Alpha Vantage
                // - Interactive Brokers TWS
                
                var candles = new List<Candle>();
                var baseTime = DateTime.UtcNow.AddMinutes(-periods * 5);

                for (int i = 0; i < periods; i++)
                {
                    candles.Add(new Candle
                    {
                        Symbol = symbol,
                        CloseTime = baseTime.AddMinutes(i * 5),
                        OpenTime = baseTime.AddMinutes(i * 5),
                        Open = 16000 + (decimal)(new Random().NextDouble() * 100),
                        High = 16050 + (decimal)(new Random().NextDouble() * 100),
                        Low = 15950 + (decimal)(new Random().NextDouble() * 100),
                        Close = 16000 + (decimal)(new Random().NextDouble() * 100),
                        Volume = 1000 + new Random().Next(5000)
                    });
                }

                return await Task.FromResult(candles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter dados de {Symbol}", symbol);
                throw;
            }
        }

        #region Private Methods

        private (List<decimal> leader, List<decimal> follower) AlignTimeframes(
            List<Candle> leaderCandles,
            List<Candle> followerCandles,
            int lookback)
        {
            var leaderReturns = leaderCandles
                .OrderBy(c => c.CloseTime)
                .TakeLast(lookback)
                .Select(c => c.Close)
                .ToList();

            var followerReturns = followerCandles
                .OrderBy(c => c.CloseTime)
                .TakeLast(lookback)
                .Select(c => c.Close)
                .ToList();

            return (leaderReturns, followerReturns);
        }

        private decimal CalculatePearsonCorrelation(List<decimal> x, List<decimal> y)
        {
            if (x.Count != y.Count || x.Count == 0)
                return 0;

            var n = x.Count;
            var meanX = x.Average();
            var meanY = y.Average();

            var numerator = x.Zip(y, (xi, yi) => (xi - meanX) * (yi - meanY)).Sum();
            var denomX = (decimal)Math.Sqrt((double)x.Sum(xi => (xi - meanX) * (xi - meanX)));
            var denomY = (decimal)Math.Sqrt((double)y.Sum(yi => (yi - meanY) * (yi - meanY)));

            if (denomX == 0 || denomY == 0)
                return 0;

            return numerator / (denomX * denomY);
        }

        private decimal CalculateSpearmanCorrelation(List<decimal> x, List<decimal> y)
        {
            // Implementação simplificada - ranqueia valores e calcula Pearson dos ranks
            var rankedX = RankData(x);
            var rankedY = RankData(y);
            return CalculatePearsonCorrelation(rankedX, rankedY);
        }

        private List<decimal> RankData(List<decimal> data)
        {
            return data
                .Select((value, index) => new { value, index })
                .OrderBy(x => x.value)
                .Select((x, rank) => (decimal)(rank + 1))
                .ToList();
        }

        private GlobalBias DetermineLeaderBias(List<Candle> candles)
        {
            if (candles.Count < 50)
                return GlobalBias.NEUTRAL;

            var ema20 = CalculateEMA(candles, 20);
            var ema50 = CalculateEMA(candles, 50);
            var lastClose = candles.OrderBy(c => c.CloseTime).Last().Close;

            // Lógica:
            // - Preço acima de EMA20 > EMA50 = BULLISH
            // - Preço abaixo de EMA20 < EMA50 = BEARISH
            // - Caso contrário = NEUTRAL

            if (lastClose > ema20 && ema20 > ema50)
                return GlobalBias.BULLISH;
            else if (lastClose < ema20 && ema20 < ema50)
                return GlobalBias.BEARISH;
            else
                return GlobalBias.NEUTRAL;
        }

        private decimal CalculateMomentum(List<Candle> candles, int period)
        {
            if (candles.Count < period + 1)
                return 0;

            var ordered = candles.OrderBy(c => c.CloseTime).ToList();
            var current = ordered.Last().Close;
            var past = ordered[^period].Close;

            return ((current - past) / past) * 100;
        }

        private decimal CalculateEMA(List<Candle> candles, int period)
        {
            if (candles.Count < period)
                return 0;

            var ordered = candles.OrderBy(c => c.CloseTime).ToList();
            var multiplier = 2.0m / (period + 1);
            var ema = ordered.Take(period).Average(c => c.Close);

            for (int i = period; i < ordered.Count; i++)
            {
                ema = (ordered[i].Close - ema) * multiplier + ema;
            }

            return ema;
        }

        private decimal CalculateTrendStrength(List<Candle> candles)
        {
            // Implementação simplificada de ADX (Average Directional Index)
            // Retorna valor entre 0-100
            // > 25 = tendência forte
            // < 20 = mercado lateral

            if (candles.Count < 14)
                return 0;

            var ordered = candles.OrderBy(c => c.CloseTime).TakeLast(14).ToList();
            
            // Calcula movimentos direcionais
            decimal sumPlus = 0, sumMinus = 0;
            for (int i = 1; i < ordered.Count; i++)
            {
                var plusDM = Math.Max(ordered[i].High - ordered[i - 1].High, 0);
                var minusDM = Math.Max(ordered[i - 1].Low - ordered[i].Low, 0);

                if (plusDM > minusDM)
                    sumPlus += plusDM;
                else
                    sumMinus += minusDM;
            }

            var totalMovement = sumPlus + sumMinus;
            if (totalMovement == 0)
                return 0;

            // ADX simplificado
            var dx = Math.Abs((sumPlus - sumMinus) / totalMovement) * 100;
            return dx;
        }

        #endregion
    }

    public interface IGlobalCorrelationService
    {
        Task<CorrelationData> CalculateCorrelationAsync(
            string leaderSymbol,
            string followerSymbol,
            List<Candle> leaderCandles,
            List<Candle> followerCandles,
            int lookbackPeriod = 50);

        Task<List<Candle>> GetLeaderDataAsync(string symbol, int periods, string timeframe = "5m");
    }
}
