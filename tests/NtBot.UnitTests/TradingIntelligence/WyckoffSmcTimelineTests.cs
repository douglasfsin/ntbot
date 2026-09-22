using NtBot.Domain.Entities;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Models;

namespace NtBot.UnitTests.TradingIntelligence;

public class WyckoffEngineTests
{
    private readonly WyckoffEngine _engine = new();

    [Fact]
    public void Analyze_DetectsSpring_WithPenetrationAndRejection()
    {
        var candles = BuildRangeCandles(100m, 105m, 60);
        var springTime = DateTime.UtcNow.AddHours(-1);
        candles.Add(new Candle
        {
            OpenTime = springTime,
            Open = 101m,
            High = 102m,
            Low = 98.5m,
            Close = 101.5m,
            Volume = 5000,
            Delta = 500
        });

        var result = _engine.Analyze(candles);

        Assert.Contains(result.Events, e => e.Event == WyckoffMarketEvent.Spring);
        Assert.Contains(result.Signals, s => s.Contains("Spring", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_DetectsUpthrust_WithPenetrationAndRejection()
    {
        var candles = BuildRangeCandles(100m, 105m, 60);
        var upthrustTime = DateTime.UtcNow.AddHours(-1);
        candles.Add(new Candle
        {
            OpenTime = upthrustTime,
            Open = 104m,
            High = 106.5m,
            Low = 103m,
            Close = 103.5m,
            Volume = 5000,
            Delta = -500
        });

        var result = _engine.Analyze(candles);

        Assert.Contains(result.Events, e => e.Event == WyckoffMarketEvent.Upthrust);
    }

    [Fact]
    public void Analyze_InsufficientCandles_ReturnsUnknownSignals()
    {
        var result = _engine.Analyze(BuildRangeCandles(100m, 105m, 10));

        Assert.Contains("insuficientes", result.Signals[0], StringComparison.OrdinalIgnoreCase);
    }

    private static List<Candle> BuildRangeCandles(decimal low, decimal high, int count)
    {
        var list = new List<Candle>();
        var mid = (low + high) / 2;
        for (var i = 0; i < count; i++)
        {
            var oscillate = i % 2 == 0 ? mid + 0.5m : mid - 0.5m;
            list.Add(new Candle
            {
                OpenTime = DateTime.UtcNow.AddHours(-count + i),
                Open = oscillate,
                High = high - 0.2m,
                Low = low + 0.2m,
                Close = oscillate,
                Volume = 1000
            });
        }

        return list;
    }
}

public class SmcEngineEventTests
{
    private readonly SmcEngine _engine = new();

    [Fact]
    public void Analyze_DetectsLiquiditySweep_OnFalseBreakdown()
    {
        var candles = BuildTrendCandles(100m, 0.1m, 35);
        var priorLow = candles.Min(c => c.Low);
        candles.Add(new Candle
        {
            OpenTime = DateTime.UtcNow,
            Open = priorLow + 0.5m,
            High = priorLow + 1m,
            Low = priorLow - 1.5m,
            Close = priorLow + 0.8m,
            Volume = 2000
        });

        var result = _engine.Analyze(candles);

        Assert.Contains(result.Events, e =>
            e.Type == "LiquiditySweep" &&
            e.Title.Contains("bullish", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_DetectsBos_WhenCloseBreaksSwingHigh()
    {
        var baseTime = DateTime.UtcNow.AddHours(-40);
        var candles = BuildRangeCandles(100m, 104m, 28, baseTime);
        var swingHighTime = baseTime.AddHours(28);
        candles.Add(new Candle
        {
            OpenTime = swingHighTime,
            Open = 103m,
            High = 105m,
            Low = 102m,
            Close = 103.5m,
            Volume = 1500
        });
        for (var i = 0; i < 5; i++)
        {
            candles.Add(new Candle
            {
                OpenTime = swingHighTime.AddHours(i + 1),
                Open = 103m,
                High = 103.8m,
                Low = 101.5m,
                Close = 102m,
                Volume = 1200
            });
        }
        candles.Add(new Candle
        {
            OpenTime = swingHighTime.AddHours(6),
            Open = 102m,
            High = 106.5m,
            Low = 101.8m,
            Close = 106m,
            Volume = 3000
        });

        var result = _engine.Analyze(candles);

        Assert.Contains("BOS", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    private static List<Candle> BuildRangeCandles(decimal low, decimal high, int count, DateTime? start = null)
    {
        var list = new List<Candle>();
        var mid = (low + high) / 2;
        var origin = start ?? DateTime.UtcNow.AddHours(-count);
        for (var i = 0; i < count; i++)
        {
            list.Add(new Candle
            {
                OpenTime = origin.AddHours(i),
                Open = mid,
                High = high - 0.1m,
                Low = low + 0.1m,
                Close = mid,
                Volume = 1200
            });
        }

        return list;
    }

    private static List<Candle> BuildTrendCandles(decimal start, decimal step, int count)
    {
        var list = new List<Candle>();
        var price = start;
        for (var i = 0; i < count; i++)
        {
            list.Add(new Candle
            {
                OpenTime = DateTime.UtcNow.AddHours(-count + i),
                Open = price,
                High = price + 1m,
                Low = price - 0.5m,
                Close = price + step,
                Volume = 1200
            });
            price += step;
        }

        return list;
    }
}

public class TradingTimelineAdvancedTests
{
    [Fact]
    public void Build_WithWyckoffSpring_IncludesTimestampedEvent()
    {
        var springTime = new DateTime(2026, 7, 9, 13, 30, 0, DateTimeKind.Utc);
        var events = TradingTimelineEngine.Build(new InstitutionalTimelineInput
        {
            Asset = "WIN",
            Engines = [],
            Confluence = new ConfluenceScoreResult(),
            WyckoffEvents =
            [
                new WyckoffStructureEvent
                {
                    Event = WyckoffMarketEvent.Spring,
                    Timestamp = springTime,
                    Confidence = 78,
                    PriceLevel = 132500m,
                    Description = "Spring — falso rompimento abaixo do suporte com rejeição"
                }
            ]
        });

        var spring = Assert.Single(events, e => e.Title.Contains("Spring", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(springTime, spring.Timestamp);
        Assert.Equal("Bullish", spring.Severity);
    }

    [Fact]
    public void Build_WithSmcLiquiditySweep_IncludesSmcCategory()
    {
        var events = TradingTimelineEngine.Build(new InstitutionalTimelineInput
        {
            Asset = "WIN",
            Engines = [],
            Confluence = new ConfluenceScoreResult(),
            SmcEvents =
            [
                new SmcStructureEvent
                {
                    Type = "LiquiditySweep",
                    Timestamp = DateTime.UtcNow,
                    Title = "Sweep de liquidez (bearish)",
                    Description = "Varredura acima do swing high",
                    Severity = "Bearish"
                }
            ]
        });

        Assert.Contains(events, e => e.Category == "SMC" && e.Severity == "Bearish");
    }
}
