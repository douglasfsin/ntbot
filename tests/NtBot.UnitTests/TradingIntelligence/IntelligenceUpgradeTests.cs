using NtBot.Domain.Entities;
using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Engine.Consensus;
using NtBot.TradingIntelligence.Engine.Context;
using NtBot.TradingIntelligence.Engine.Filters;
using NtBot.TradingIntelligence.Engine.Liquidity;
using NtBot.TradingIntelligence.Engine.Structure;
using NtBot.TradingIntelligence.Engine.Volume;
using NtBot.TradingIntelligence.Models;

namespace NtBot.UnitTests.TradingIntelligence;

public class ConfluenceWeightAndGatingTests
{
    private readonly ConfluenceEngine _engine = new();

    [Fact]
    public void Weights_UseInstitutionalStructureLiquidityVolumeScheme()
    {
        Assert.Equal(0.30m, InstitutionalWeights.Structure);
        Assert.Equal(0.20m, InstitutionalWeights.Liquidity);
        Assert.Equal(0.15m, InstitutionalWeights.Volume);
        Assert.Equal(0.10m, InstitutionalWeights.Wyckoff);
        Assert.Equal(0.10m, InstitutionalWeights.Correlation);
        Assert.Equal(0.10m, InstitutionalWeights.Macro);
        Assert.Equal(0.05m, InstitutionalWeights.Volatility);
    }

    [Fact]
    public void Calculate_LowConfidence_ForcesAguardarEvenWithBullishScore()
    {
        var result = _engine.Calculate(new InstitutionalConfluenceInput
        {
            Asset = "XAUUSD",
            Engines =
            [
                EngineAnalysisResult.Known("Structure", 85, 35, InstitutionalWeights.Structure, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Liquidity", 80, 30, InstitutionalWeights.Liquidity, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Volume", 78, 28, InstitutionalWeights.Volume, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Wyckoff", 75, 32, InstitutionalWeights.Wyckoff, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Correlação", 70, 30, InstitutionalWeights.Correlation, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Macro", 72, 30, InstitutionalWeights.Macro, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Volatility", 55, 40, InstitutionalWeights.Volatility, EngineMarketBias.Sideways)
            ],
            Risk = EngineAnalysisResult.RiskOnly(40, ["risco elevado"])
        });

        Assert.True(result.Confidence < 45);
        Assert.True(ConfidenceLevels.BlocksDirectionalTrade(result.ConfidenceLevel));
        Assert.Equal("AGUARDAR", result.Recommendation);
        Assert.Equal("Sideways", result.Bias);
        Assert.Contains(result.BlockingFactors, f => f.Contains("confiança", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Calculate_AntiLossBlocking_ForcesAguardar()
    {
        var result = _engine.Calculate(new InstitutionalConfluenceInput
        {
            Asset = "XAUUSD",
            Engines =
            [
                EngineAnalysisResult.Known("Structure", 88, 90, InstitutionalWeights.Structure, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Liquidity", 82, 88, InstitutionalWeights.Liquidity, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Volume", 80, 85, InstitutionalWeights.Volume, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Wyckoff", 78, 84, InstitutionalWeights.Wyckoff, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Correlação", 76, 80, InstitutionalWeights.Correlation, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Macro", 74, 82, InstitutionalWeights.Macro, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Volatility", 60, 70, InstitutionalWeights.Volatility, EngineMarketBias.Sideways)
            ],
            Risk = EngineAnalysisResult.RiskOnly(90),
            BlockingFactors = ["conflito entre timeframes", "ATR extremo (1.80%)"]
        });

        Assert.Equal("AGUARDAR", result.Recommendation);
        Assert.Null(result.RiskSuggestion);
    }

    [Fact]
    public void Calculate_HighConfidenceBullish_AllowsCompra()
    {
        var result = _engine.Calculate(new InstitutionalConfluenceInput
        {
            Asset = "XAUUSD",
            Engines =
            [
                EngineAnalysisResult.Known("Structure", 88, 92, InstitutionalWeights.Structure, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Liquidity", 84, 90, InstitutionalWeights.Liquidity, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Volume", 80, 88, InstitutionalWeights.Volume, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Wyckoff", 78, 86, InstitutionalWeights.Wyckoff, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Correlação", 76, 84, InstitutionalWeights.Correlation, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Macro", 74, 82, InstitutionalWeights.Macro, EngineMarketBias.Bullish),
                EngineAnalysisResult.Known("Volatility", 62, 75, InstitutionalWeights.Volatility, EngineMarketBias.Sideways)
            ],
            Risk = EngineAnalysisResult.RiskOnly(92),
            RiskSuggestion = new TradeRiskSuggestion
            {
                Entry = 2400,
                StopLoss = 2390,
                TakeProfit = 2420,
                RiskReward = 2,
                Summary = "Stop 2390 · TP 2420 · R:R 2.00"
            }
        });

        Assert.Contains("COMPRA", result.Recommendation);
        Assert.False(ConfidenceLevels.BlocksDirectionalTrade(result.ConfidenceLevel));
        Assert.NotNull(result.RiskSuggestion);
    }
}

public class AntiLossFilterTests
{
    private readonly AntiLossFilter _filter = new();

    [Fact]
    public void Evaluate_MultipleRiskFactors_Blocks()
    {
        var result = _filter.Evaluate(new AntiLossFilterInput
        {
            Asset = "XAUUSD",
            AtrPercent = 1.8m,
            HasTimeframeConflict = true,
            WeakVolume = true,
            StructureScore = 50,
            VolumeScore = 35
        });

        Assert.True(result.ShouldBlock);
        Assert.True(result.ConfidencePenalty >= 28);
        Assert.NotEmpty(result.Reasons);
    }

    [Fact]
    public void Evaluate_CleanConditions_DoesNotBlock()
    {
        var result = _filter.Evaluate(new AntiLossFilterInput
        {
            Asset = "XAUUSD",
            AtrPercent = 0.4m,
            StructureScore = 75,
            VolumeScore = 70,
            IsRanging = false,
            HasTimeframeConflict = false,
            WeakVolume = false,
            StructureUndefined = false
        });

        Assert.False(result.ShouldBlock);
        Assert.True(result.ConfidencePenalty < 28);
    }
}

public class StructureComposerTests
{
    [Fact]
    public void Compose_BlendsTrendAndSmcIntoStructureWeight()
    {
        var trend = EngineAnalysisResult.Known("Trend", 80, 85, InstitutionalWeights.Trend, EngineMarketBias.Bullish);
        var smc = EngineAnalysisResult.Known("SMC", 70, 80, InstitutionalWeights.Smc, EngineMarketBias.Bullish);
        var result = StructureComposer.Compose(trend, smc);

        Assert.Equal("Structure", result.Engine);
        Assert.Equal(InstitutionalWeights.Structure, result.BaseWeight);
        Assert.Equal(EngineDataStatus.Known, result.Status);
        Assert.InRange(result.Score!.Value, 70, 80);
    }
}

public class MarketContextEngineTests
{
    [Fact]
    public void Analyze_DetectsSessionAndMultiBarRegime()
    {
        var engine = new MarketContextEngine();
        var candles = IntelligenceCandleFixtures.BuildTrend(30, bullish: true);
        var london = new DateTime(2026, 8, 2, 13, 0, 0, DateTimeKind.Utc);
        var ctx = engine.Analyze("XAUUSD", candles, london);

        Assert.Equal("LondonNY_Overlap", ctx.Session);
        Assert.True(ctx.IsOverlap);
        Assert.True(ctx.ContextBarsUsed >= 8);
        Assert.NotEqual("Undefined", ctx.Regime);
    }
}

public class LiquidityAndVolumeEngineTests
{
    [Fact]
    public void Liquidity_BullishSweep_RaisesScore()
    {
        var engine = new LiquidityEngine();
        var candles = IntelligenceCandleFixtures.BuildWithBullishSweep(40);
        var result = engine.Analyze("XAUUSD", candles);
        Assert.Equal(EngineDataStatus.Known, result.Status);
        Assert.True(result.Score > 50);
    }

    [Fact]
    public void Volume_SpikeOnUpBar_RaisesScore()
    {
        var engine = new VolumeAnalysisEngine();
        var candles = IntelligenceCandleFixtures.BuildTrend(30, bullish: true);
        candles[^1].Volume = (long)(candles.Take(29).Average(c => c.Volume) * 2.5);
        var result = engine.Analyze("XAUUSD", candles);
        Assert.True(result.Score > 55);
        Assert.Contains(result.Signals, s => s.Contains("volume", StringComparison.OrdinalIgnoreCase));
    }
}

public class MultiTimeframeConsensusTests
{
    [Fact]
    public void Evaluate_Conflict_SetsHasConflict()
    {
        var engine = new MultiTimeframeConsensusEngine();
        var result = engine.Evaluate(
        [
            new TimeframeAnalysis { Timeframe = "5", SmcScore = 80, WyckoffScore = 78, VolumeScore = 75 },
            new TimeframeAnalysis { Timeframe = "15", SmcScore = 75, WyckoffScore = 72, VolumeScore = 70 },
            new TimeframeAnalysis { Timeframe = "60", SmcScore = 25, WyckoffScore = 28, VolumeScore = 30 },
            new TimeframeAnalysis { Timeframe = "240", SmcScore = 22, WyckoffScore = 20, VolumeScore = 25 }
        ]);

        Assert.True(result.HasConflict);
    }
}

public class SmcDetectionExpansionTests
{
    [Fact]
    public void Analyze_IncludesPremiumDiscountAndDetections()
    {
        var engine = new SmcEngine();
        var result = engine.Analyze(IntelligenceCandleFixtures.BuildTrend(40, bullish: true));

        Assert.True(
            result.PremiumDiscountZone is "Premium" or "Discount" or "Equilibrium",
            $"Unexpected zone: {result.PremiumDiscountZone}");
        Assert.NotEmpty(result.Detections);
        Assert.Contains(result.Detections, d =>
            d.Type is "PremiumDiscount" or "BOS" or "FVG" or "OrderBlock" or "OTE" or "CHOCH" or "LiquiditySweep");
        Assert.All(result.Detections, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Explanation));
            Assert.InRange(d.Confidence, 0, 100);
        });
    }
}

internal static class IntelligenceCandleFixtures
{
    public static List<Candle> BuildTrend(int count, bool bullish)
    {
        var candles = new List<Candle>();
        var price = 2300m;
        for (var i = 0; i < count; i++)
        {
            var delta = bullish ? 1.2m + i * 0.05m : -1.2m - i * 0.05m;
            var open = price;
            var close = price + delta;
            candles.Add(new Candle
            {
                OpenTime = DateTime.UtcNow.AddMinutes(i * 5),
                Open = open,
                Close = close,
                High = Math.Max(open, close) + 0.8m,
                Low = Math.Min(open, close) - 0.8m,
                Volume = 800 + i * 12
            });
            price = close;
        }

        return candles;
    }

    public static List<Candle> BuildWithBullishSweep(int count)
    {
        var candles = BuildTrend(count, bullish: true);
        var priorLow = candles.Take(count - 2).Min(c => c.Low);
        var last = candles[^1];
        last.Low = priorLow - 2m;
        last.Close = priorLow + 1m;
        last.Open = priorLow - 0.5m;
        last.High = last.Close + 1m;
        return candles;
    }
}
