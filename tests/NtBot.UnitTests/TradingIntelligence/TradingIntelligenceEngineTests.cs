using NtBot.Domain.Entities;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Models;

namespace NtBot.UnitTests.TradingIntelligence;

public class ConfluenceEngineTests
{
    private readonly ConfluenceEngine _engine = new();

    [Fact]
    public void Calculate_AllNeutralScores_ReturnsNeutralBand()
    {
        var result = _engine.Calculate(new EngineScoreInput
        {
            Asset = "WIN",
            MacroScore = 50,
            DriverScore = 50,
            WyckoffScore = 50,
            SmcScore = 50,
            VolumeScore = 50,
            MomentumScore = 50,
            CorrelationScore = 50,
            LiquidityScore = 50,
            CalendarScore = 50
        });

        Assert.InRange(result.Score, 45, 55);
        Assert.Equal("Neutra", result.Classification);
    }

    [Fact]
    public void Calculate_StrongBullishInputs_ReturnsHighScore()
    {
        var result = _engine.Calculate(new EngineScoreInput
        {
            Asset = "WIN",
            MacroScore = 90,
            DriverScore = 88,
            WyckoffScore = 85,
            SmcScore = 82,
            VolumeScore = 80,
            MomentumScore = 78,
            CorrelationScore = 75,
            LiquidityScore = 70,
            CalendarScore = 65
        });

        Assert.True(result.Score >= 80);
        Assert.Contains("Alta", result.Classification);
    }

    [Fact]
    public void Calculate_StrongBearishInputs_ReturnsLowScore()
    {
        var result = _engine.Calculate(new EngineScoreInput
        {
            Asset = "WIN",
            MacroScore = 15,
            DriverScore = 20,
            WyckoffScore = 18,
            SmcScore = 22,
            VolumeScore = 25,
            MomentumScore = 20,
            CorrelationScore = 30,
            LiquidityScore = 28,
            CalendarScore = 35
        });

        Assert.True(result.Score <= 25);
        Assert.True(result.Classification is "Fraca" or "Muito Fraca");
    }
}

public class SmcEngineTests
{
    private readonly SmcEngine _engine = new();

    [Fact]
    public void Analyze_InsufficientCandles_ReturnsNeutralDefault()
    {
        var result = _engine.Analyze(BuildTrend(10, bullish: true));

        Assert.Equal(50, result.Score);
        Assert.Contains("insuficientes", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_BullishTrend_ReturnsBullishBiasAndHigherScore()
    {
        var result = _engine.Analyze(BuildTrend(40, bullish: true));

        Assert.Equal(SmcStructureBias.Bullish, result.Bias);
        Assert.True(result.Score > 50);
    }

    [Fact]
    public void Analyze_BearishTrend_ReturnsBearishBiasAndLowerScore()
    {
        var result = _engine.Analyze(BuildTrend(40, bullish: false));

        Assert.Equal(SmcStructureBias.Bearish, result.Bias);
        Assert.True(result.Score < 50);
    }

    private static List<Candle> BuildTrend(int count, bool bullish)
    {
        var candles = new List<Candle>();
        var price = 1000m;
        for (var i = 0; i < count; i++)
        {
            var delta = bullish ? 5m + i * 0.2m : -5m - i * 0.2m;
            var open = price;
            var close = price + delta;
            candles.Add(new Candle
            {
                OpenTime = DateTime.UtcNow.AddMinutes(i * 5),
                Open = open,
                Close = close,
                High = Math.Max(open, close) + 2,
                Low = Math.Min(open, close) - 2,
                Volume = 1000 + i * 10
            });
            price = close;
        }

        return candles;
    }
}

public class TimeframeIntersectionEngineTests
{
    [Fact]
    public void Calculate_NoOverlap_ReturnsEmpty()
    {
        var timeframes = new List<TimeframeAnalysis>
        {
            new() { Timeframe = "5", Low = 100, High = 110, WyckoffScore = 80, SmcScore = 80, VolumeScore = 80 },
            new() { Timeframe = "15", Low = 200, High = 220, WyckoffScore = 80, SmcScore = 80, VolumeScore = 80 }
        };

        var result = TimeframeIntersectionEngine.Calculate(timeframes);

        Assert.Empty(result);
    }

    [Fact]
    public void Calculate_StrongOverlapWithAlignedScores_MarksHighConfluence()
    {
        var timeframes = new List<TimeframeAnalysis>
        {
            new() { Timeframe = "5", Low = 100, High = 200, WyckoffScore = 75, SmcScore = 78, VolumeScore = 72 },
            new() { Timeframe = "15", Low = 120, High = 180, WyckoffScore = 76, SmcScore = 80, VolumeScore = 74 }
        };

        var result = TimeframeIntersectionEngine.Calculate(timeframes);

        Assert.NotEmpty(result);
        var pair = Assert.Single(result, r => r.Pair == "5x15");
        Assert.True(pair.ConfluenceScore >= 70);
        Assert.True(pair.HighConfluence);
    }
}

public class SpecialistAgentEngineTests
{
    [Fact]
    public void BuildInsights_IncludesEngineAgentsAndAssetSpecialist()
    {
        var snapshot = new TradingIntelligenceSnapshot
        {
            Asset = "WIN",
            Confluence = new ConfluenceScoreResult
            {
                Score = 72,
                Classification = "Alta",
                Recommendation = "Compra moderada",
                Explanation = "Teste"
            },
            HeatMap =
            [
                new TradingIntelligenceHeatCell { Engine = "Macro", Score = 70, Weight = 0.2m },
                new TradingIntelligenceHeatCell { Engine = "SMC", Score = 68, Weight = 0.15m }
            ],
            OperationalZones =
            [
                new OperationalZone { Label = "Zona A", Type = OperationalZoneType.ModerateBuy }
            ]
        };

        var insights = SpecialistAgentEngine.BuildInsights("WIN", snapshot);

        Assert.Contains(insights, i => i.AgentId == "macro-agent");
        Assert.Contains(insights, i => i.AgentId == "smc-agent");
        Assert.Contains(insights, i => i.AgentId == "asset-win");
    }
}
