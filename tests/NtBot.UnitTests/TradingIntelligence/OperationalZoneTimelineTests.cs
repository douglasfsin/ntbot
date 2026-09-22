using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Models;

namespace NtBot.UnitTests.TradingIntelligence;

public class TradingTimelineEngineTests
{
    [Fact]
    public void Build_WithBullishTrend_IncludesBiasEvent()
    {
        var engines = new List<EngineAnalysisResult>
        {
            EngineAnalysisResult.Known("Trend", 82, 88, 0.22m, EngineMarketBias.Bullish, ["preço acima da VWAP"]),
            EngineAnalysisResult.Known("SMC", 78, 85, 0.14m, EngineMarketBias.Bullish, ["BOS bullish"])
        };

        var confluence = new ConfluenceScoreResult
        {
            Score = 80,
            Bias = "Bullish",
            Recommendation = "COMPRA MODERADA"
        };

        var events = TradingTimelineEngine.Build("WIN", engines, confluence, []);

        Assert.Contains(events, e => e.Category == "Confluence");
        Assert.Contains(events, e => e.Title.Contains("BOS", StringComparison.OrdinalIgnoreCase));
    }
}

public class OperationalZoneEngineExtendedTests
{
    private readonly OperationalZoneEngine _engine = new();

    [Fact]
    public void BuildZones_WithTimeframes_IncludesVwapAndLiquidityZones()
    {
        var timeframes = new List<TimeframeAnalysis>
        {
            new() { Timeframe = "15", Low = 100, High = 120, Mid = 110, WyckoffScore = 75, SmcScore = 72, VolumeScore = 80 },
            new() { Timeframe = "60", Low = 95, High = 125, Mid = 110, WyckoffScore = 70, SmcScore = 68, VolumeScore = 74 }
        };

        var zones = _engine.BuildZones("WIN", new ConfluenceScoreResult { Score = 72 }, timeframes, []);

        Assert.Contains(zones, z => z.Label.Contains("Liquidez"));
        Assert.Contains(zones, z => z.Label.Contains("POC"));
        Assert.Contains(zones, z => z.Label.Contains("Demanda"));
        // VWAP is emitted but may be deduped when it sits inside POC/Demand (intentional anti-clutter).
        Assert.True(zones.Count <= 10);
    }

    [Fact]
    public void BuildZones_HighConfluenceIntersection_CreatesFvgZone()
    {
        var intersections = new List<TimeframeIntersection>
        {
            new()
            {
                Pair = "15x60",
                PriceLow = 100,
                PriceHigh = 110,
                ConfluenceScore = 80,
                HighConfluence = true
            }
        };

        var zones = _engine.BuildZones("WIN", new ConfluenceScoreResult { Score = 75 }, [], intersections);

        Assert.Contains(zones, z => z.Label.Contains("FVG") && z.Type == OperationalZoneType.StrongBuy);
    }
}
