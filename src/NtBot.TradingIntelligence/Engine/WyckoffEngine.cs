using NtBot.Domain.Entities;
using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine;

public enum WyckoffMarketPhase
{
    Unknown,
    Accumulation,
    Distribution,
    Markup,
    Markdown,
    Ranging
}

public enum WyckoffMarketEvent
{
    Spring,
    Upthrust,
    SellingClimax,
    BuyingClimax,
    SignOfStrength,
    SignOfWeakness
}

public sealed class WyckoffStructureEvent
{
    public WyckoffMarketEvent Event { get; init; }
    public DateTime Timestamp { get; init; }
    public decimal Confidence { get; init; }
    public decimal? PriceLevel { get; init; }
    public string Description { get; init; } = string.Empty;
}

public sealed class WyckoffEngineResult
{
    public WyckoffMarketPhase Phase { get; init; } = WyckoffMarketPhase.Unknown;
    public decimal PhaseConfidence { get; init; }
    public WyckoffMarketEvent? PrimaryEvent { get; init; }
    public decimal EventConfidence { get; init; }
    public EngineMarketBias Bias { get; init; } = EngineMarketBias.Unknown;
    public decimal? RangeHigh { get; init; }
    public decimal? RangeLow { get; init; }
    public int RangeCandles { get; init; }
    public bool VolumeDivergence { get; init; }
    public IReadOnlyList<WyckoffStructureEvent> Events { get; init; } = [];
    public IReadOnlyList<string> Signals { get; init; } = [];
    public int Score { get; init; }
    public decimal Confidence { get; init; }
}

public interface IWyckoffEngine
{
    WyckoffEngineResult Analyze(IReadOnlyList<Candle> candles);
    bool HasVolumeDivergence(IReadOnlyList<Candle> candles);
}

/// <summary>
/// Motor Wyckoff institucional — fases, Spring, Upthrust e climax de volume.
/// </summary>
public sealed class WyckoffEngine : IWyckoffEngine
{
    private const decimal SpringPenetration = 0.002m;
    private const decimal RejectionRatio = 0.65m;
    private const int MinRangeCandles = 10;
    private const int VolumeLookback = 20;
    private const int MinCandles = 50;

    public WyckoffEngineResult Analyze(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < MinCandles)
        {
            return new WyckoffEngineResult
            {
                Signals = [$"Dados insuficientes para Wyckoff ({candles.Count}/{MinCandles})."]
            };
        }

        var ordered = candles.OrderBy(c => c.OpenTime).ToList();
        var atr = CalculateAtr(ordered, 14);
        var (isRange, rangeHigh, rangeLow, rangeCandles) = IdentifyRange(ordered, atr);
        var volumeDivergence = HasVolumeDivergence(ordered);
        var phase = DetectPhase(ordered, isRange, rangeCandles, atr);
        var phaseConfidence = CalculatePhaseConfidence(phase, ordered, isRange);
        var events = DetectStructureEvents(ordered, isRange, rangeHigh, rangeLow);
        var primary = events.OrderByDescending(e => e.Confidence).FirstOrDefault();
        var bias = DetermineBias(phase, primary?.Event);
        var score = CalculateScore(bias, phaseConfidence, primary);
        var confidence = Math.Clamp(
            phaseConfidence * 0.55m + (primary?.Confidence ?? 0) * 0.45m,
            30, 95);

        var signals = BuildSignals(phase, primary, volumeDivergence, isRange, rangeCandles, rangeLow, rangeHigh);

        return new WyckoffEngineResult
        {
            Phase = phase,
            PhaseConfidence = phaseConfidence,
            PrimaryEvent = primary?.Event,
            EventConfidence = primary?.Confidence ?? 0,
            Bias = bias,
            RangeHigh = isRange ? rangeHigh : null,
            RangeLow = isRange ? rangeLow : null,
            RangeCandles = rangeCandles,
            VolumeDivergence = volumeDivergence,
            Events = events,
            Signals = signals,
            Score = score,
            Confidence = confidence
        };
    }

    public bool HasVolumeDivergence(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < VolumeLookback * 2)
            return false;

        var recent = candles.TakeLast(VolumeLookback).ToList();
        var prior = candles.TakeLast(VolumeLookback * 2).Take(VolumeLookback).ToList();

        var recentHigh = recent.Max(c => c.High);
        var priorHigh = prior.Max(c => c.High);
        var recentLow = recent.Min(c => c.Low);
        var priorLow = prior.Min(c => c.Low);
        var recentAvgVol = recent.Average(c => c.Volume);
        var priorAvgVol = prior.Average(c => c.Volume);

        var bullishDiv = recentLow < priorLow && recentAvgVol < priorAvgVol * 0.8;
        var bearishDiv = recentHigh > priorHigh && recentAvgVol < priorAvgVol * 0.8;
        return bullishDiv || bearishDiv;
    }

    private static List<string> BuildSignals(
        WyckoffMarketPhase phase,
        WyckoffStructureEvent? primary,
        bool volumeDivergence,
        bool isRange,
        int rangeCandles,
        decimal rangeLow,
        decimal rangeHigh)
    {
        var signals = new List<string> { $"Fase {phase}" };

        if (primary is not null)
            signals.Insert(0, $"{FormatEvent(primary.Event)} ({primary.Confidence:F0}%)");

        if (volumeDivergence)
            signals.Add("divergência de volume");

        if (isRange)
            signals.Add($"range {rangeLow:F0}–{rangeHigh:F0} ({rangeCandles} candles)");

        return signals;
    }

    private static int CalculateScore(EngineMarketBias bias, decimal phaseConfidence, WyckoffStructureEvent? primary)
    {
        var score = bias switch
        {
            EngineMarketBias.Bullish => (int)Math.Clamp(55m + phaseConfidence * 0.45m, 0, 100),
            EngineMarketBias.Bearish => (int)Math.Clamp(45m - phaseConfidence * 0.45m, 0, 100),
            _ => (int)Math.Clamp(phaseConfidence * 0.5m + 25, 0, 100)
        };

        if (primary?.Event is WyckoffMarketEvent.Spring or WyckoffMarketEvent.SignOfStrength)
            score = Math.Min(100, score + 8);
        if (primary?.Event is WyckoffMarketEvent.Upthrust or WyckoffMarketEvent.SignOfWeakness)
            score = Math.Max(0, score - 8);

        return score;
    }

    private static EngineMarketBias DetermineBias(WyckoffMarketPhase phase, WyckoffMarketEvent? evt) =>
        phase switch
        {
            WyckoffMarketPhase.Accumulation or WyckoffMarketPhase.Markup => EngineMarketBias.Bullish,
            WyckoffMarketPhase.Distribution or WyckoffMarketPhase.Markdown => EngineMarketBias.Bearish,
            _ => evt switch
            {
                WyckoffMarketEvent.Spring or WyckoffMarketEvent.SignOfStrength or WyckoffMarketEvent.BuyingClimax => EngineMarketBias.Bullish,
                WyckoffMarketEvent.Upthrust or WyckoffMarketEvent.SignOfWeakness or WyckoffMarketEvent.SellingClimax => EngineMarketBias.Bearish,
                _ => EngineMarketBias.Sideways
            }
        };

    private List<WyckoffStructureEvent> DetectStructureEvents(
        IReadOnlyList<Candle> candles,
        bool isRange,
        decimal rangeHigh,
        decimal rangeLow)
    {
        var events = new List<WyckoffStructureEvent>();
        var scanFrom = Math.Max(0, candles.Count - 25);

        for (var i = scanFrom; i < candles.Count; i++)
        {
            var window = candles.Take(i + 1).ToList();
            if (window.Count < 20)
                continue;

            var candle = candles[i];
            var priorLow = window.TakeLast(50).Min(c => c.Low);
            var priorHigh = window.TakeLast(50).Max(c => c.High);
            var avgVolume = window.TakeLast(VolumeLookback).Average(c => c.Volume);

            var springConfidence = DetectSpringConfidence(candle, priorLow, avgVolume);
            if (springConfidence >= 55)
            {
                events.Add(new WyckoffStructureEvent
                {
                    Event = WyckoffMarketEvent.Spring,
                    Timestamp = candle.OpenTime,
                    Confidence = springConfidence,
                    PriceLevel = priorLow,
                    Description = "Spring — falso rompimento abaixo do suporte com rejeição"
                });
            }

            var upthrustConfidence = DetectUpthrustConfidence(candle, priorHigh, avgVolume);
            if (upthrustConfidence >= 55)
            {
                events.Add(new WyckoffStructureEvent
                {
                    Event = WyckoffMarketEvent.Upthrust,
                    Timestamp = candle.OpenTime,
                    Confidence = upthrustConfidence,
                    PriceLevel = priorHigh,
                    Description = "Upthrust — falso rompimento acima da resistência com rejeição"
                });
            }

            if (candle.Volume > avgVolume * 2.2)
            {
                var isDown = candle.Close < candle.Open;
                var delta = candle.Delta ?? 0;
                if (isDown && delta <= 0)
                {
                    events.Add(new WyckoffStructureEvent
                    {
                        Event = WyckoffMarketEvent.SellingClimax,
                        Timestamp = candle.OpenTime,
                        Confidence = 72,
                        PriceLevel = candle.Low,
                        Description = "Selling Climax — volume extremo com pressão vendedora"
                    });
                }
                else if (!isDown && delta >= 0)
                {
                    events.Add(new WyckoffStructureEvent
                    {
                        Event = WyckoffMarketEvent.BuyingClimax,
                        Timestamp = candle.OpenTime,
                        Confidence = 72,
                        PriceLevel = candle.High,
                        Description = "Buying Climax — volume extremo com pressão compradora"
                    });
                }
            }
        }

        if (isRange && candles.Count >= 2)
        {
            var last = candles[^1];
            var prev = candles[^2];
            if (last.Close > rangeHigh * 0.998m && last.Volume > candles.TakeLast(VolumeLookback).Average(c => c.Volume) * 1.3)
            {
                events.Add(new WyckoffStructureEvent
                {
                    Event = WyckoffMarketEvent.SignOfStrength,
                    Timestamp = last.OpenTime,
                    Confidence = 68,
                    PriceLevel = last.Close,
                    Description = "Sign of Strength — rompimento do topo do range com volume"
                });
            }
            else if (last.Close < rangeLow * 1.002m && last.Volume > candles.TakeLast(VolumeLookback).Average(c => c.Volume) * 1.3)
            {
                events.Add(new WyckoffStructureEvent
                {
                    Event = WyckoffMarketEvent.SignOfWeakness,
                    Timestamp = last.OpenTime,
                    Confidence = 68,
                    PriceLevel = last.Close,
                    Description = "Sign of Weakness — rompimento do fundo do range com volume"
                });
            }
        }

        return events
            .GroupBy(e => $"{e.Event}:{e.Timestamp:O}")
            .Select(g => g.OrderByDescending(e => e.Confidence).First())
            .OrderByDescending(e => e.Timestamp)
            .Take(8)
            .ToList();
    }

    private static decimal DetectSpringConfidence(Candle candle, decimal priorLow, double avgVolume)
    {
        if (priorLow <= 0)
            return 0;

        var range = candle.High - candle.Low;
        if (range <= 0)
            return 0;

        var penetrated = candle.Low < priorLow * (1 - SpringPenetration);
        var rejected = candle.Close > priorLow;
        var wickRatio = (candle.Close - candle.Low) / range;
        var strongRejection = wickRatio >= RejectionRatio;
        var highVolume = candle.Volume > avgVolume * 1.15;
        var positiveDelta = (candle.Delta ?? 0) > 0;

        var confidence = 0m;
        if (penetrated) confidence += 25;
        if (rejected) confidence += 25;
        if (strongRejection) confidence += 20;
        if (highVolume) confidence += 15;
        if (positiveDelta) confidence += 15;
        return confidence;
    }

    private static decimal DetectUpthrustConfidence(Candle candle, decimal priorHigh, double avgVolume)
    {
        if (priorHigh <= 0)
            return 0;

        var range = candle.High - candle.Low;
        if (range <= 0)
            return 0;

        var penetrated = candle.High > priorHigh * (1 + SpringPenetration);
        var rejected = candle.Close < priorHigh;
        var wickRatio = (candle.High - candle.Close) / range;
        var strongRejection = wickRatio >= RejectionRatio;
        var highVolume = candle.Volume > avgVolume * 1.15;
        var negativeDelta = (candle.Delta ?? 0) < 0;

        var confidence = 0m;
        if (penetrated) confidence += 25;
        if (rejected) confidence += 25;
        if (strongRejection) confidence += 20;
        if (highVolume) confidence += 15;
        if (negativeDelta) confidence += 15;
        return confidence;
    }

    private static WyckoffMarketPhase DetectPhase(IReadOnlyList<Candle> candles, bool isRange, int rangeCandles, decimal atr)
    {
        if (!isRange)
        {
            var trend = candles.TakeLast(20).ToList();
            var change = trend[^1].Close - trend[0].Close;
            if (change > atr * 3) return WyckoffMarketPhase.Markup;
            if (change < -atr * 3) return WyckoffMarketPhase.Markdown;
            return WyckoffMarketPhase.Ranging;
        }

        var rangeSlice = candles.TakeLast(rangeCandles).ToList();
        var buyVol = rangeSlice.Where(c => c.BuyVolume.HasValue).Sum(c => c.BuyVolume!.Value);
        var sellVol = rangeSlice.Where(c => c.SellVolume.HasValue).Sum(c => c.SellVolume!.Value);

        if (buyVol > 0 || sellVol > 0)
        {
            if (buyVol > sellVol * 1.2m) return WyckoffMarketPhase.Accumulation;
            if (sellVol > buyVol * 1.2m) return WyckoffMarketPhase.Distribution;
        }

        // Fallback sem order-flow: posição no range + fechamentos (útil para MT5 tick volume)
        var mid = (rangeSlice.Max(c => c.High) + rangeSlice.Min(c => c.Low)) / 2m;
        var closesAbove = rangeSlice.Count(c => c.Close >= mid);
        var closesBelow = rangeSlice.Count - closesAbove;
        var lastThird = rangeSlice.TakeLast(Math.Max(3, rangeCandles / 3)).ToList();
        var rising = lastThird[^1].Close > lastThird[0].Close;

        if (closesAbove > closesBelow * 1.15 && rising)
            return WyckoffMarketPhase.Accumulation;
        if (closesBelow > closesAbove * 1.15 && !rising)
            return WyckoffMarketPhase.Distribution;

        return WyckoffMarketPhase.Ranging;
    }

    private static decimal CalculatePhaseConfidence(WyckoffMarketPhase phase, IReadOnlyList<Candle> candles, bool isRange)
    {
        if (phase == WyckoffMarketPhase.Unknown)
            return 0;

        var confidence = 50m;
        if (isRange) confidence += 20;

        var recentVolumes = candles.TakeLast(10).Select(c => c.Volume).ToList();
        var avg = recentVolumes.Average();
        var stdDev = (decimal)Math.Sqrt(recentVolumes.Average(v => Math.Pow(v - avg, 2)));
        if (avg > 0 && stdDev < (decimal)avg * 0.35m)
            confidence += 12;

        return Math.Min(100, confidence);
    }

    private static (bool isRange, decimal high, decimal low, int candles) IdentifyRange(IReadOnlyList<Candle> candles, decimal atr)
    {
        if (candles.Count < MinRangeCandles * 2 || atr <= 0)
            return (false, 0, 0, 0);

        var lookback = Math.Min(100, candles.Count);
        var recent = candles.TakeLast(lookback).ToList();
        var high = recent.Take(MinRangeCandles).Max(c => c.High);
        var low = recent.Take(MinRangeCandles).Min(c => c.Low);
        var rangeSize = high - low;

        if (rangeSize > atr * 2.5m)
            return (false, 0, 0, 0);

        var candlesInRange = 0;
        foreach (var candle in recent)
        {
            if (candle.High <= high * 1.01m && candle.Low >= low * 0.99m)
            {
                candlesInRange++;
                if (candle.High > high) high = candle.High;
                if (candle.Low < low) low = candle.Low;
            }
            else if (candlesInRange >= MinRangeCandles)
            {
                break;
            }
            else
            {
                candlesInRange = 0;
                high = candle.High;
                low = candle.Low;
            }
        }

        return (candlesInRange >= MinRangeCandles, high, low, candlesInRange);
    }

    private static decimal CalculateAtr(IReadOnlyList<Candle> candles, int period)
    {
        if (candles.Count < period + 1)
            return 0;

        var trs = new List<decimal>();
        for (var i = 1; i < candles.Count; i++)
        {
            var current = candles[i];
            var previous = candles[i - 1];
            trs.Add(Math.Max(
                current.High - current.Low,
                Math.Max(Math.Abs(current.High - previous.Close), Math.Abs(current.Low - previous.Close))));
        }

        return trs.TakeLast(period).Average();
    }

    private static string FormatEvent(WyckoffMarketEvent evt) => evt switch
    {
        WyckoffMarketEvent.Spring => "Spring",
        WyckoffMarketEvent.Upthrust => "Upthrust",
        WyckoffMarketEvent.SellingClimax => "Selling Climax (SC)",
        WyckoffMarketEvent.BuyingClimax => "Buying Climax (BC)",
        WyckoffMarketEvent.SignOfStrength => "Sign of Strength (SOS)",
        WyckoffMarketEvent.SignOfWeakness => "Sign of Weakness (SOW)",
        _ => evt.ToString()
    };
}
