using System.Diagnostics;
using NtBot.Domain.Entities;
using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine.Liquidity;

public interface ILiquidityEngine
{
    EngineAnalysisResult Analyze(string asset, IReadOnlyList<Candle> candles);
}

/// <summary>
/// Liquidez: sweeps, equal highs/lows e pools próximos do preço.
/// </summary>
public sealed class LiquidityEngine : ILiquidityEngine
{
    private const int MinCandles = 30;
    private const decimal EqualTolerancePct = 0.0008m;

    public EngineAnalysisResult Analyze(string asset, IReadOnlyList<Candle> candles)
    {
        var sw = Stopwatch.StartNew();
        if (candles.Count < MinCandles)
        {
            return EngineAnalysisResult.Unknown(
                "Liquidity",
                InstitutionalWeights.Liquidity,
                $"Candles insuficientes para Liquidity ({candles.Count}/{MinCandles}).",
                "candles");
        }

        var ordered = candles.OrderBy(c => c.OpenTime).ToList();
        var last = ordered[^1];
        var signals = new List<string>();
        var score = 50;

        var equalHighs = CountEqualLevels(ordered.Select(c => c.High).ToList(), last.Close);
        var equalLows = CountEqualLevels(ordered.Select(c => c.Low).ToList(), last.Close);
        if (equalHighs >= 2)
        {
            score -= 6;
            signals.Add($"{equalHighs} equal highs (pool de venda)");
        }

        if (equalLows >= 2)
        {
            score += 6;
            signals.Add($"{equalLows} equal lows (pool de compra)");
        }

        var (bullSweep, bearSweep) = DetectRecentSweeps(ordered);
        if (bullSweep)
        {
            score += 14;
            signals.Add("liquidity sweep bullish recente");
        }

        if (bearSweep)
        {
            score -= 14;
            signals.Add("liquidity sweep bearish recente");
        }

        var poolBias = ScoreNearbyPools(ordered, last.Close);
        score += poolBias.Delta;
        signals.AddRange(poolBias.Signals);

        score = Math.Clamp(score, 0, 100);
        var bias = score switch
        {
            >= 58 => EngineMarketBias.Bullish,
            <= 42 => EngineMarketBias.Bearish,
            _ => EngineMarketBias.Sideways
        };

        var confidence = Math.Clamp(
            40m + Math.Abs(score - 50) * 0.9m + (bullSweep || bearSweep ? 12m : 0m),
            30, 92);

        sw.Stop();
        return EngineAnalysisResult.Known(
            "Liquidity",
            score,
            confidence,
            InstitutionalWeights.Liquidity,
            bias,
            signals,
            "candles",
            sw.ElapsedMilliseconds);
    }

    private static int CountEqualLevels(IReadOnlyList<decimal> levels, decimal refPrice)
    {
        if (levels.Count < 4 || refPrice <= 0)
            return 0;

        var recent = levels.TakeLast(40).ToList();
        var clusters = 0;
        for (var i = 0; i < recent.Count; i++)
        {
            var matches = 0;
            for (var j = i + 1; j < recent.Count; j++)
            {
                if (Math.Abs(recent[i] - recent[j]) / refPrice <= EqualTolerancePct)
                    matches++;
            }

            if (matches >= 1)
                clusters++;
        }

        return Math.Min(clusters, 5);
    }

    private static (bool Bull, bool Bear) DetectRecentSweeps(IReadOnlyList<Candle> candles)
    {
        var bull = false;
        var bear = false;
        var from = Math.Max(5, candles.Count - 16);

        for (var i = from; i < candles.Count; i++)
        {
            var c = candles[i];
            var prior = candles.Take(i).TakeLast(20).ToList();
            if (prior.Count < 5)
                continue;

            var priorLow = prior.Min(x => x.Low);
            var priorHigh = prior.Max(x => x.High);
            var range = c.High - c.Low;
            if (range <= 0)
                continue;

            if (c.Low < priorLow && c.Close > priorLow && (c.Close - c.Low) / range >= 0.55m)
                bull = true;
            if (c.High > priorHigh && c.Close < priorHigh && (c.High - c.Close) / range >= 0.55m)
                bear = true;
        }

        return (bull, bear);
    }

    private static (int Delta, List<string> Signals) ScoreNearbyPools(IReadOnlyList<Candle> candles, decimal price)
    {
        var signals = new List<string>();
        if (price <= 0)
            return (0, signals);

        var lookback = candles.TakeLast(50).ToList();
        var highs = lookback.Select(c => c.High).OrderByDescending(h => h).Take(5).ToList();
        var lows = lookback.Select(c => c.Low).OrderBy(l => l).Take(5).ToList();

        var nearestHigh = highs.FirstOrDefault(h => h > price);
        var nearestLow = lows.FirstOrDefault(l => l < price);
        var delta = 0;

        if (nearestHigh > 0)
        {
            var dist = (nearestHigh - price) / price;
            if (dist < 0.002m)
            {
                delta -= 4;
                signals.Add("preço sob pool de liquidez superior");
            }
        }

        if (nearestLow > 0)
        {
            var dist = (price - nearestLow) / price;
            if (dist < 0.002m)
            {
                delta += 4;
                signals.Add("preço sobre pool de liquidez inferior");
            }
        }

        return (delta, signals);
    }
}
