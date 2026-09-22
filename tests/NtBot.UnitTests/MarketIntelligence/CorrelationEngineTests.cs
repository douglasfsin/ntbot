using NtBot.MarketIntelligence.Engine;
using NtBot.MarketIntelligence.Models;

namespace NtBot.UnitTests.MarketIntelligence;

public class CorrelationEngineTests
{
    private readonly CorrelationEngine _engine = new();

    [Fact]
    public void BuildAssetImpacts_UsesRealWinHistory_WhenAvailable()
    {
        var winHistory = BuildSeries(100m, 0.002, 90);
        var petrHistory = BuildSeries(40m, 0.003, 90);

        var history = new Dictionary<string, IReadOnlyList<PriceHistoryPoint>>
        {
            ["WIN"] = winHistory,
            ["PETR4"] = petrHistory,
            ["^GSPC"] = BuildSeries(5000m, -0.001, 90)
        };

        var impacts = _engine.BuildAssetImpacts(history);
        var win = Assert.Single(impacts, i => i.Asset == "WIN");

        Assert.Contains(win.Factors, f => f.Label == "PETR4");
        Assert.True(Math.Abs(win.Factors.First(f => f.Label == "PETR4").Correlation) > 0);
    }

    [Fact]
    public void BuildAssetImpacts_DynamicWeights_NotFixed()
    {
        var history = new Dictionary<string, IReadOnlyList<PriceHistoryPoint>>
        {
            ["WIN"] = BuildSeries(100m, 0.002, 90),
            ["PETR4"] = BuildSeries(40m, 0.003, 90),
            ["VALE3"] = BuildSeries(70m, -0.004, 90)
        };

        var impacts = _engine.BuildAssetImpacts(history);
        var win = Assert.Single(impacts, i => i.Asset == "WIN");

        var petr = win.Factors.First(f => f.Label == "PETR4");
        var vale = win.Factors.First(f => f.Label == "VALE3");
        Assert.NotEqual(petr.Weight, vale.Weight);
    }

    private static List<PriceHistoryPoint> BuildSeries(decimal start, double dailyDrift, int days)
    {
        var list = new List<PriceHistoryPoint>();
        var price = start;
        for (var i = 0; i < days; i++)
        {
            list.Add(new PriceHistoryPoint { Date = DateTime.UtcNow.Date.AddDays(-days + i), Close = price });
            price *= 1m + (decimal)dailyDrift;
        }

        return list;
    }
}
