using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Models;

namespace NtBot.UnitTests.TradingIntelligence;

public class ConfluenceEngineTests
{
    private readonly ConfluenceEngine _engine = new();

    [Fact]
    public void Calculate_AllUnknownEngines_ReturnsInsufficientData()
    {
        var result = _engine.Calculate(new InstitutionalConfluenceInput
        {
            Asset = "WIN",
            Engines =
            [
                EngineAnalysisResult.Unknown("Trend", InstitutionalWeights.Trend, "sem candles"),
                EngineAnalysisResult.Unknown("Momentum", InstitutionalWeights.Momentum, "sem candles"),
                EngineAnalysisResult.Unknown("Wyckoff", InstitutionalWeights.Wyckoff, "sem candles")
            ]
        });

        Assert.Equal("Dados Insuficientes", result.Classification);
        Assert.Equal("AGUARDAR DADOS", result.Recommendation);
        Assert.Equal(0, result.KnownEngineCount);
    }

    [Fact]
    public void Calculate_OnlyBullishKnownEngines_ReturnsHighScoreNotDilutedByUnknown()
    {
        var result = _engine.Calculate(new InstitutionalConfluenceInput
        {
            Asset = "WIN",
            Engines =
            [
                EngineAnalysisResult.Known("Trend", 85, 90, InstitutionalWeights.Trend, EngineMarketBias.Bullish, ["acima VWAP"]),
                EngineAnalysisResult.Known("Momentum", 82, 88, InstitutionalWeights.Momentum, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("SMC", 80, 85, InstitutionalWeights.Smc, EngineMarketBias.Bullish),
                EngineAnalysisResult.Unknown("Correlação", InstitutionalWeights.Correlation, "sem dados")
            ]
        });

        Assert.True(result.Score >= 78);
        Assert.Equal("Bullish", result.Bias);
        Assert.Equal(3, result.KnownEngineCount);
    }

    [Fact]
    public void Calculate_StrongBearishInputs_ReturnsLowScore()
    {
        var result = _engine.Calculate(new InstitutionalConfluenceInput
        {
            Asset = "WIN",
            Engines =
            [
                EngineAnalysisResult.Known("Trend", 18, 85, InstitutionalWeights.Trend, EngineMarketBias.Bearish),
                EngineAnalysisResult.Known("Momentum", 20, 80, InstitutionalWeights.Momentum, EngineMarketBias.Bearish),
                EngineAnalysisResult.Known("Wyckoff", 22, 78, InstitutionalWeights.Wyckoff, EngineMarketBias.Bearish),
                EngineAnalysisResult.Known("SMC", 25, 82, InstitutionalWeights.Smc, EngineMarketBias.Bearish),
                EngineAnalysisResult.Known("Volume", 28, 75, InstitutionalWeights.Volume, EngineMarketBias.Bearish),
                EngineAnalysisResult.Known("Drivers", 20, 70, InstitutionalWeights.Drivers, EngineMarketBias.Bearish),
                EngineAnalysisResult.Known("Macro", 15, 72, InstitutionalWeights.Macro, EngineMarketBias.Bearish),
                EngineAnalysisResult.Known("Correlação", 30, 65, InstitutionalWeights.Correlation, EngineMarketBias.Bearish)
            ]
        });

        Assert.True(result.Score <= 28);
        Assert.Contains("VENDA", result.Recommendation);
    }

    [Fact]
    public void Calculate_LegacyInput_StillWorks()
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
    }
}

public class TrendEngineTests
{
    private readonly TrendEngine _engine = new();

    [Fact]
    public void Analyze_InsufficientCandles_ReturnsUnknown()
    {
        var result = _engine.Analyze("WIN", BuildTrend(10, bullish: true));
        Assert.Equal(EngineDataStatus.Unknown, result.Status);
        Assert.Null(result.Score);
    }

    [Fact]
    public void Analyze_BullishTrend_ReturnsHigherScore()
    {
        var result = _engine.Analyze("WIN", BuildTrend(40, bullish: true));
        Assert.Equal(EngineDataStatus.Known, result.Status);
        Assert.True(result.Score > 55);
        Assert.Equal(EngineMarketBias.Bullish, result.Bias);
    }

    private static List<NtBot.Domain.Entities.Candle> BuildTrend(int count, bool bullish)
    {
        var candles = new List<NtBot.Domain.Entities.Candle>();
        var price = 1000m;
        for (var i = 0; i < count; i++)
        {
            var delta = bullish ? 5m + i * 0.2m : -5m - i * 0.2m;
            var open = price;
            var close = price + delta;
            candles.Add(new NtBot.Domain.Entities.Candle
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

    private static List<NtBot.Domain.Entities.Candle> BuildTrend(int count, bool bullish)
    {
        var candles = new List<NtBot.Domain.Entities.Candle>();
        var price = 1000m;
        for (var i = 0; i < count; i++)
        {
            var delta = bullish ? 5m + i * 0.2m : -5m - i * 0.2m;
            var open = price;
            var close = price + delta;
            candles.Add(new NtBot.Domain.Entities.Candle
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

    [Fact]
    public void Calculate_DuplicateAndAliasTimeframeKeys_DoesNotThrow()
    {
        var timeframes = new List<TimeframeAnalysis>
        {
            new() { Timeframe = "5", Low = 100, High = 200, WyckoffScore = 75, SmcScore = 78, VolumeScore = 72 },
            new() { Timeframe = "5", Low = 101, High = 199, WyckoffScore = 70, SmcScore = 71, VolumeScore = 69 },
            new() { Timeframe = "M5", Low = 102, High = 198, WyckoffScore = 74, SmcScore = 76, VolumeScore = 73 },
            new() { Timeframe = "15", Low = 120, High = 180, WyckoffScore = 76, SmcScore = 80, VolumeScore = 74 },
            new() { Timeframe = "M15", Low = 118, High = 182, WyckoffScore = 72, SmcScore = 74, VolumeScore = 70 }
        };

        var result = TimeframeIntersectionEngine.Calculate(timeframes);

        Assert.NotEmpty(result);
        Assert.Contains(result, r => r.Pair == "5x15");
    }
}

public class OperationalZoneEngineTests
{
    private readonly OperationalZoneEngine _engine = new();

    [Fact]
    public void BuildZones_HighConfluenceIntersection_CreatesStrongBuyZone()
    {
        var confluence = new ConfluenceScoreResult { Score = 75, Classification = "Alta" };
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

        var zones = _engine.BuildZones("WIN", confluence, [], intersections);

        Assert.Contains(zones, z => z.Label.Contains("FVG") && z.Type == OperationalZoneType.StrongBuy);
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
                new TradingIntelligenceHeatCell { Engine = "Macro", Score = 70, Weight = 0.10m },
                new TradingIntelligenceHeatCell { Engine = "Structure", Score = 68, Weight = 0.30m }
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

public class RiskEngineTests
{
    private readonly RiskEngine _engine = new();

    [Fact]
    public void Assess_LowDataCoverage_ReducesConfidence()
    {
        var result = _engine.Assess(new InstitutionalRiskInput
        {
            KnownEngineCount = 2,
            TotalEngineCount = 8,
            HasHighImpactCalendarEvent = true
        });

        Assert.True(result.Confidence < 70);
        Assert.Contains(result.Signals, s => s.Contains("cobertura", StringComparison.OrdinalIgnoreCase));
    }
}
