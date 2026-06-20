using NtBot.Domain.Entities;

namespace NtBot.Api.Services.GammaExposure
{
    /// <summary>
    /// Serviço para cálculo de Gamma Exposure (GEX) baseado em dados de opções
    /// </summary>
    public class GammaExposureService : IGammaExposureService
    {
        private readonly ILogger<GammaExposureService> _logger;
        private const decimal SPOT_GAMMA_MULTIPLIER = 100; // Multiplicador para normalização
        private const decimal GAMMA_WALL_THRESHOLD = 0.15m; // 15% do total GEX

        public GammaExposureService(ILogger<GammaExposureService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Calcula GEX agregado a partir de dados de opções
        /// </summary>
        public async Task<GammaExposureData> CalculateGEXAsync(
            string symbol,
            decimal currentPrice,
            List<OptionData> options)
        {
            try
            {
                if (options == null || !options.Any())
                {
                    _logger.LogWarning("Nenhum dado de opções fornecido para {Symbol}", symbol);
                    return CreateNeutralGEX(symbol, currentPrice);
                }

                // 1. Calcula gamma por strike
                var gammaByStrike = CalculateGammaByStrike(options, currentPrice);

                // 2. Calcula GEX total (net gamma exposure)
                var totalGEX = CalculateTotalGEX(gammaByStrike, currentPrice);
                var netGamma = CalculateNetGamma(gammaByStrike);

                // 3. Identifica Gamma Flip Level
                var gammaFlipLevel = FindGammaFlipLevel(gammaByStrike, currentPrice);

                // 4. Identifica Gamma Walls
                var gammaWalls = IdentifyGammaWalls(gammaByStrike, currentPrice);

                // 5. Determina regime de GEX
                var regime = DetermineGEXRegime(totalGEX, currentPrice, gammaFlipLevel);

                // 6. Calcula potenciais de movimento
                var (expansionPotential, meanReversionPotential) = 
                    CalculateMovementPotentials(regime, gammaWalls, currentPrice);

                return new GammaExposureData
                {
                    Id = Guid.NewGuid(),
                    Symbol = symbol,
                    CurrentPrice = currentPrice,
                    TotalGEX = totalGEX,
                    NetGamma = netGamma,
                    GammaFlipLevel = gammaFlipLevel,
                    GammaWalls = gammaWalls,
                    Regime = regime,
                    ExpansionPotential = expansionPotential,
                    MeanReversionPotential = meanReversionPotential,
                    CalculatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular GEX para {Symbol}", symbol);
                throw;
            }
        }

        /// <summary>
        /// Obtém dados de opções de uma fonte externa
        /// </summary>
        public async Task<List<OptionData>> GetOptionsDataAsync(string symbol, DateTime? expiration = null)
        {
            try
            {
                // TODO: Integrar com fonte real de dados de opções
                // Fontes possíveis:
                // - CBOE DataShop
                // - Interactive Brokers TWS API
                // - TD Ameritrade API
                // - B3 (para opções brasileiras)
                // - HistoricalOptionData.com

                _logger.LogInformation("Obtendo dados de opções para {Symbol}", symbol);

                // Mock de dados para desenvolvimento
                var options = GenerateMockOptionsData(symbol, 120000); // WIN em ~120k

                return await Task.FromResult(options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter dados de opções para {Symbol}", symbol);
                throw;
            }
        }

        #region Private Methods

        private Dictionary<decimal, decimal> CalculateGammaByStrike(
            List<OptionData> options,
            decimal spotPrice)
        {
            var gammaByStrike = new Dictionary<decimal, decimal>();

            foreach (var option in options)
            {
                // Gamma é positiva para MM (market makers que vendem opções)
                // Para traders: 
                // - Calls vendidas (MM compradas) = gamma positiva
                // - Puts vendidas (MM compradas) = gamma positiva
                
                // GEX = Gamma × Open Interest × 100 × Spot²
                var notionalGamma = option.Gamma * option.OpenInterest * SPOT_GAMMA_MULTIPLIER;
                
                // Market makers têm posição oposta aos traders
                // Se traders compram calls, MM vende calls → gamma negativa para o mercado
                var gexContribution = option.Type == OptionType.CALL ? -notionalGamma : notionalGamma;

                if (gammaByStrike.ContainsKey(option.Strike))
                    gammaByStrike[option.Strike] += gexContribution;
                else
                    gammaByStrike[option.Strike] = gexContribution;
            }

            return gammaByStrike;
        }

        private decimal CalculateTotalGEX(Dictionary<decimal, decimal> gammaByStrike, decimal spotPrice)
        {
            // Soma ponderada pela proximidade do strike ao preço atual
            decimal totalGEX = 0;

            foreach (var kvp in gammaByStrike)
            {
                var strike = kvp.Key;
                var gamma = kvp.Value;

                // Peso decai com a distância do strike
                var distance = Math.Abs((strike - spotPrice) / spotPrice);
                var weight = (decimal)Math.Exp(-(double)(distance * 5)); // Decaimento exponencial

                totalGEX += gamma * weight;
            }

            return totalGEX;
        }

        private decimal CalculateNetGamma(Dictionary<decimal, decimal> gammaByStrike)
        {
            return gammaByStrike.Values.Sum();
        }

        private decimal? FindGammaFlipLevel(Dictionary<decimal, decimal> gammaByStrike, decimal currentPrice)
        {
            // Gamma Flip = nível de preço onde GEX muda de positivo para negativo
            var sortedStrikes = gammaByStrike.OrderBy(kvp => kvp.Key).ToList();

            decimal cumulativeGamma = 0;
            decimal? flipLevel = null;

            foreach (var kvp in sortedStrikes)
            {
                var previousCumulative = cumulativeGamma;
                cumulativeGamma += kvp.Value;

                // Detecta mudança de sinal
                if (previousCumulative * cumulativeGamma < 0)
                {
                    flipLevel = kvp.Key;
                    break;
                }
            }

            return flipLevel;
        }

        private List<GammaWall> IdentifyGammaWalls(
            Dictionary<decimal, decimal> gammaByStrike,
            decimal currentPrice)
        {
            var walls = new List<GammaWall>();
            var maxAbsGamma = gammaByStrike.Values.Max(g => Math.Abs(g));

            if (maxAbsGamma == 0)
                return walls;

            foreach (var kvp in gammaByStrike)
            {
                var strike = kvp.Key;
                var gamma = kvp.Value;
                var concentration = Math.Abs(gamma) / maxAbsGamma;

                // Considera "wall" se concentração > threshold
                if (concentration >= GAMMA_WALL_THRESHOLD)
                {
                    var distance = ((strike - currentPrice) / currentPrice) * 100;
                    var type = strike > currentPrice ? "Resistance" : "Support";

                    walls.Add(new GammaWall
                    {
                        Strike = strike,
                        GammaConcentration = concentration * 100,
                        Type = type,
                        Distance = distance
                    });
                }
            }

            return walls.OrderBy(w => Math.Abs(w.Distance)).ToList();
        }

        private GEXRegime DetermineGEXRegime(
            decimal totalGEX,
            decimal currentPrice,
            decimal? gammaFlipLevel)
        {
            // Classificação baseada em magnitude e posição relativa ao flip
            if (totalGEX > 1000)
                return GEXRegime.POSITIVE_HIGH;
            else if (totalGEX > 100)
                return GEXRegime.POSITIVE_LOW;
            else if (totalGEX > -100)
                return GEXRegime.NEUTRAL;
            else if (totalGEX > -1000)
                return GEXRegime.NEGATIVE_LOW;
            else
                return GEXRegime.NEGATIVE_HIGH;
        }

        private (decimal expansionPotential, decimal meanReversionPotential) CalculateMovementPotentials(
            GEXRegime regime,
            List<GammaWall> walls,
            decimal currentPrice)
        {
            decimal expansionPotential = 0;
            decimal meanReversionPotential = 0;

            switch (regime)
            {
                case GEXRegime.NEGATIVE_HIGH:
                    expansionPotential = 90;
                    meanReversionPotential = 10;
                    break;

                case GEXRegime.NEGATIVE_LOW:
                    expansionPotential = 70;
                    meanReversionPotential = 30;
                    break;

                case GEXRegime.NEUTRAL:
                    expansionPotential = 50;
                    meanReversionPotential = 50;
                    break;

                case GEXRegime.POSITIVE_LOW:
                    expansionPotential = 30;
                    meanReversionPotential = 70;
                    break;

                case GEXRegime.POSITIVE_HIGH:
                    expansionPotential = 10;
                    meanReversionPotential = 90;
                    break;
            }

            // Ajusta baseado em proximidade de gamma walls
            var nearestWall = walls.OrderBy(w => Math.Abs(w.Distance)).FirstOrDefault();
            if (nearestWall != null && Math.Abs(nearestWall.Distance) < 1) // < 1%
            {
                meanReversionPotential += 10;
                expansionPotential -= 10;
            }

            return (
                Math.Max(0, Math.Min(100, expansionPotential)),
                Math.Max(0, Math.Min(100, meanReversionPotential))
            );
        }

        private GammaExposureData CreateNeutralGEX(string symbol, decimal currentPrice)
        {
            return new GammaExposureData
            {
                Id = Guid.NewGuid(),
                Symbol = symbol,
                CurrentPrice = currentPrice,
                TotalGEX = 0,
                NetGamma = 0,
                GammaFlipLevel = null,
                GammaWalls = new List<GammaWall>(),
                Regime = GEXRegime.NEUTRAL,
                ExpansionPotential = 50,
                MeanReversionPotential = 50,
                CalculatedAt = DateTime.UtcNow
            };
        }

        private List<OptionData> GenerateMockOptionsData(string symbol, decimal spotPrice)
        {
            var options = new List<OptionData>();
            var random = new Random();
            var strikes = new List<decimal>();

            // Gera strikes em torno do preço spot (±5%)
            for (decimal strike = spotPrice * 0.95m; strike <= spotPrice * 1.05m; strike += spotPrice * 0.005m)
            {
                strikes.Add(Math.Round(strike, 0));
            }

            var expiration = DateTime.UtcNow.AddDays(30);

            foreach (var strike in strikes)
            {
                var moneyness = Math.Abs(spotPrice - strike) / spotPrice;

                // Calls
                options.Add(new OptionData
                {
                    Id = Guid.NewGuid(),
                    Symbol = symbol,
                    Strike = strike,
                    Type = OptionType.CALL,
                    Gamma = (decimal)(0.001 * Math.Exp(-(double)(moneyness * 10))),
                    Delta = strike > spotPrice ? 0.3m : 0.7m,
                    OpenInterest = random.Next(100, 10000),
                    LastPrice = Math.Max(0.01m, spotPrice - strike + (spotPrice * 0.02m)),
                    ImpliedVolatility = 0.15m + (decimal)(random.NextDouble() * 0.1),
                    Expiration = expiration,
                    Timestamp = DateTime.UtcNow
                });

                // Puts
                options.Add(new OptionData
                {
                    Id = Guid.NewGuid(),
                    Symbol = symbol,
                    Strike = strike,
                    Type = OptionType.PUT,
                    Gamma = (decimal)(0.001 * Math.Exp(-(double)(moneyness * 10))),
                    Delta = strike < spotPrice ? -0.3m : -0.7m,
                    OpenInterest = random.Next(100, 10000),
                    LastPrice = Math.Max(0.01m, strike - spotPrice + (spotPrice * 0.02m)),
                    ImpliedVolatility = 0.15m + (decimal)(random.NextDouble() * 0.1),
                    Expiration = expiration,
                    Timestamp = DateTime.UtcNow
                });
            }

            return options;
        }

        #endregion
    }

    public interface IGammaExposureService
    {
        Task<GammaExposureData> CalculateGEXAsync(
            string symbol,
            decimal currentPrice,
            List<OptionData> options);

        Task<List<OptionData>> GetOptionsDataAsync(string symbol, DateTime? expiration = null);
    }
}
