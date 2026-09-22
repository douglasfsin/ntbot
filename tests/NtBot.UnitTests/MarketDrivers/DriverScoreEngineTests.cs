using NtBot.MarketDrivers.Configuration;
using NtBot.MarketDrivers.Engine;
using NtBot.MarketDrivers.Models;
using NtBot.MarketIntelligence.Models;
using NtBot.Macro.DTO;

namespace NtBot.UnitTests.MarketDrivers;

public class DriverScoreEngineTests
{
    private readonly DriverScoreEngine _engine = new();

    [Fact]
    public void Calculate_NoAvailableDrivers_ReturnsLowConfidence()
    {
        var context = new MarketDriverContext
        {
            Asset = "WIN",
            Overview = new MarketOverview(),
            QuantScore = new QuantScore { Score = 50 },
            Macro = new MacroSnapshot { Confidence = 0, MacroScore = MacroRegimeLabel.Unknown },
            Correlation = new CorrelationResult()
        };
        var drivers = new List<MarketDriver>
        {
            new()
            {
                Symbol = "PETR4",
                Category = MarketDriverCategory.Correlacao,
                Name = "PETR4",
                Description = "PETR4 indisponível.",
                Impact = DriverImpactLevel.Neutral,
                Weight = 0.2m,
                Confidence = 0.3m
            }
        };

        var result = _engine.Calculate(context, drivers);

        Assert.Equal("AGUARDAR DADOS", result.Recommendation);
        Assert.True(result.Confidence < 40);
        Assert.Equal(0, result.KnownComponentCount);
    }

    [Fact]
    public void Calculate_StrongBullishDrivers_NotDilutedByMissingComponents()
    {
        var context = BuildContext();
        var drivers = new List<MarketDriver>
        {
            BuildDriver("PETR4", MarketDriverCategory.Correlacao, DriverImpactLevel.VeryPositive, 0.3m),
            BuildDriver("VALE3", MarketDriverCategory.Commodities, DriverImpactLevel.Positive, 0.25m),
            BuildDriver("MOM", MarketDriverCategory.Momentum, DriverImpactLevel.Positive, 0.2m)
        };

        var result = _engine.Calculate(context, drivers);

        Assert.True(result.Score >= 75);
        Assert.True(result.KnownComponentCount >= 2);
    }

    private static MarketDriver BuildDriver(
        string symbol,
        MarketDriverCategory category,
        DriverImpactLevel impact,
        decimal weight) =>
        new()
        {
            Symbol = symbol,
            Category = category,
            Name = symbol,
            Impact = impact,
            Weight = weight,
            Variation = 1.5m,
            Description = $"{symbol} ▲ 1.5%",
            Confidence = 0.8m
        };

    private static MarketDriverContext BuildContext() =>
        new()
        {
            Asset = "WIN",
            Overview = new MarketOverview
            {
                Vix = new MarketSnapshot { Symbol = "^VIX", Price = 16, ChangePercent = -2 }
            },
            QuantScore = new QuantScore { Score = 72 },
            Macro = new MacroSnapshot { Confidence = 70, MacroScore = MacroRegimeLabel.Bullish },
            Correlation = new CorrelationResult(),
            AssetImpact = new AssetImpactResult
            {
                Asset = "WIN",
                ImpactScore = 0.6,
                Factors = [new ImpactFactor { Symbol = "PETR4", Label = "PETR4", Correlation = 0.7, Weight = 0.2 }]
            }
        };
}
