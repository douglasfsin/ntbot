using System.Diagnostics;
using NtBot.Domain.Entities;
using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine;

public interface ITrendEngine
{
    EngineAnalysisResult Analyze(string asset, IReadOnlyList<Candle> candles);
}

/// <summary>
/// Direção de mercado via estrutura de preço, médias, VWAP e amplitude.
/// </summary>
public sealed class TrendEngine : ITrendEngine
{
    private const int MinCandles = 30;

    public EngineAnalysisResult Analyze(string asset, IReadOnlyList<Candle> candles)
    {
        var sw = Stopwatch.StartNew();
        if (candles.Count < MinCandles)
        {
            return EngineAnalysisResult.Unknown(
                "Trend",
                InstitutionalWeights.Trend,
                $"Dados insuficientes ({candles.Count}/{MinCandles} candles).",
                "candles");
        }

        var ordered = candles.OrderBy(c => c.OpenTime).ToList();
        var closes = ordered.Select(c => c.Close).ToList();
        var last = ordered[^1];

        var ema20 = Ema(closes, 20);
        var ema50 = Ema(closes, Math.Min(50, closes.Count));
        var vwap = CalculateVwap(ordered);
        var atr = CalculateAtr(ordered, 14);
        var atrPct = last.Close > 0 ? atr / last.Close * 100m : 0m;

        var structure = AnalyzeStructure(ordered);
        var dailyChange = ordered.Count >= 2
            ? (last.Close - ordered[0].Open) / (ordered[0].Open == 0 ? 1 : ordered[0].Open) * 100m
            : 0m;

        var score = 50;
        var signals = new List<string>();

        if (last.Close > ema20) { score += 8; signals.Add("preço acima da EMA20"); }
        else { score -= 8; signals.Add("preço abaixo da EMA20"); }

        if (last.Close > ema50) { score += 10; signals.Add("preço acima da EMA50"); }
        else { score -= 10; signals.Add("preço abaixo da EMA50"); }

        if (ema20 > ema50) { score += 6; signals.Add("EMA20 acima da EMA50"); }
        else { score -= 6; signals.Add("EMA20 abaixo da EMA50"); }

        if (vwap.HasValue)
        {
            if (last.Close > vwap.Value) { score += 8; signals.Add("preço acima da VWAP"); }
            else { score -= 8; signals.Add("preço abaixo da VWAP"); }
        }

        score += structure.Bias switch
        {
            EngineMarketBias.Bullish => 12,
            EngineMarketBias.Bearish => -12,
            _ => 0
        };
        signals.AddRange(structure.Signals);

        if (dailyChange > 0.35m) { score += 6; signals.Add($"variação diária +{dailyChange:F2}%"); }
        if (dailyChange < -0.35m) { score -= 6; signals.Add($"variação diária {dailyChange:F2}%"); }

        var rangePosition = last.High > last.Low
            ? (last.Close - last.Low) / (last.High - last.Low)
            : 0.5m;
        if (rangePosition >= 0.75m) { score += 4; signals.Add("fechamento no terço superior do candle"); }
        if (rangePosition <= 0.25m) { score -= 4; signals.Add("fechamento no terço inferior do candle"); }

        var bias = score switch
        {
            >= 62 => EngineMarketBias.Bullish,
            <= 38 => EngineMarketBias.Bearish,
            _ => EngineMarketBias.Sideways
        };

        var confidence = 45m
            + Math.Min(25m, Math.Abs(dailyChange) * 8m)
            + (structure.HigherHighs && structure.HigherLows ? 15m : 0m)
            + (structure.LowerHighs && structure.LowerLows ? 15m : 0m)
            + Math.Min(10m, atrPct * 2m);

        sw.Stop();
        return EngineAnalysisResult.Known(
            "Trend",
            score,
            Math.Clamp(confidence, 35, 95),
            InstitutionalWeights.Trend,
            bias,
            signals,
            "profitdll-candles",
            sw.ElapsedMilliseconds);
    }

    private static decimal Ema(IReadOnlyList<decimal> values, int period)
    {
        if (values.Count == 0) return 0;
        period = Math.Max(2, Math.Min(period, values.Count));
        var k = 2m / (period + 1);
        var ema = values[0];
        for (var i = 1; i < values.Count; i++)
            ema = values[i] * k + ema * (1 - k);
        return ema;
    }

    private static decimal? CalculateVwap(IReadOnlyList<Candle> candles)
    {
        decimal pv = 0;
        decimal vol = 0;
        foreach (var c in candles)
        {
            var typical = (c.High + c.Low + c.Close) / 3m;
            var v = Math.Max(c.Volume, 1);
            pv += typical * v;
            vol += v;
        }

        return vol > 0 ? pv / vol : null;
    }

    private static decimal CalculateAtr(IReadOnlyList<Candle> candles, int period)
    {
        period = Math.Min(period, candles.Count - 1);
        if (period <= 0) return 0;

        var trs = new List<decimal>();
        for (var i = 1; i < candles.Count; i++)
        {
            var c = candles[i];
            var prev = candles[i - 1];
            var tr = Math.Max(c.High - c.Low, Math.Max(Math.Abs(c.High - prev.Close), Math.Abs(c.Low - prev.Close)));
            trs.Add(tr);
        }

        return trs.TakeLast(period).DefaultIfEmpty(0).Average();
    }

    private static (EngineMarketBias Bias, bool HigherHighs, bool HigherLows, bool LowerHighs, bool LowerLows, IReadOnlyList<string> Signals)
        AnalyzeStructure(IReadOnlyList<Candle> candles)
    {
        var signals = new List<string>();
        var swings = new List<(bool IsHigh, decimal Price)>();
        const int lookback = 2;

        for (var i = lookback; i < candles.Count - lookback; i++)
        {
            var isHigh = candles.Skip(i - lookback).Take(lookback * 2 + 1).Max(c => c.High) == candles[i].High;
            var isLow = candles.Skip(i - lookback).Take(lookback * 2 + 1).Min(c => c.Low) == candles[i].Low;
            if (isHigh) swings.Add((true, candles[i].High));
            if (isLow) swings.Add((false, candles[i].Low));
        }

        var highs = swings.Where(s => s.IsHigh).Select(s => s.Price).TakeLast(3).ToList();
        var lows = swings.Where(s => !s.IsHigh).Select(s => s.Price).TakeLast(3).ToList();

        var hh = highs.Count >= 2 && highs[^1] > highs[^2];
        var hl = lows.Count >= 2 && lows[^1] > lows[^2];
        var lh = highs.Count >= 2 && highs[^1] < highs[^2];
        var ll = lows.Count >= 2 && lows[^1] < lows[^2];

        if (hh) signals.Add("Higher High");
        if (hl) signals.Add("Higher Low");
        if (lh) signals.Add("Lower High");
        if (ll) signals.Add("Lower Low");

        var bias = hh && hl ? EngineMarketBias.Bullish
            : lh && ll ? EngineMarketBias.Bearish
            : EngineMarketBias.Sideways;

        return (bias, hh, hl, lh, ll, signals);
    }
}
