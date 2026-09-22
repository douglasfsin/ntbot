using System.Diagnostics;
using NtBot.Domain.Entities;
using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine;

public interface IMomentumEngine
{
    EngineAnalysisResult Analyze(string asset, IReadOnlyList<Candle> candles);
}

public sealed class MomentumEngine : IMomentumEngine
{
    private const int MinCandles = 20;

    public EngineAnalysisResult Analyze(string asset, IReadOnlyList<Candle> candles)
    {
        var sw = Stopwatch.StartNew();
        if (candles.Count < MinCandles)
        {
            return EngineAnalysisResult.Unknown(
                "Momentum",
                InstitutionalWeights.Momentum,
                $"Dados insuficientes ({candles.Count}/{MinCandles} candles).",
                "candles");
        }

        var ordered = candles.OrderBy(c => c.OpenTime).ToList();
        var closes = ordered.Select(c => c.Close).ToList();
        var last = ordered[^1];

        var roc10 = Roc(closes, 10);
        var roc5 = Roc(closes, 5);
        var rsi = CalculateRsi(closes, 14);
        var (macdLine, signalLine) = CalculateMacd(closes);
        var avgVol = ordered.Average(c => (double)c.Volume);
        var lastVolRatio = avgVol > 0 ? (double)last.Volume / avgVol : 1;
        var dailyReturn = ordered.Count >= 2
            ? (last.Close - ordered[^2].Close) / (ordered[^2].Close == 0 ? 1 : ordered[^2].Close) * 100m
            : 0m;

        var score = 50;
        var signals = new List<string>();

        score += (int)Math.Clamp(roc10 * 4m, -20, 20);
        if (roc10 > 0.5m) signals.Add($"ROC10 +{roc10:F2}%");
        if (roc10 < -0.5m) signals.Add($"ROC10 {roc10:F2}%");

        if (roc5 > roc10) { score += 5; signals.Add("aceleração de momentum"); }
        if (roc5 < roc10 && roc10 > 0) { score -= 4; signals.Add("desaceleração de momentum"); }

        if (rsi >= 70) { score += 6; signals.Add($"RSI {rsi:F0} (força compradora)"); }
        else if (rsi <= 30) { score -= 6; signals.Add($"RSI {rsi:F0} (força vendedora)"); }
        else if (rsi >= 55) score += 3;
        else if (rsi <= 45) score -= 3;

        if (macdLine > signalLine) { score += 6; signals.Add("MACD acima do sinal"); }
        else { score -= 6; signals.Add("MACD abaixo do sinal"); }

        if (lastVolRatio > 1.3) { score += 5; signals.Add("volume acima da média"); }
        if (lastVolRatio < 0.7) { score -= 3; signals.Add("volume abaixo da média"); }

        if (dailyReturn > 0.25m) { score += 4; signals.Add($"retorno recente +{dailyReturn:F2}%"); }
        if (dailyReturn < -0.25m) { score -= 4; signals.Add($"retorno recente {dailyReturn:F2}%"); }

        var bias = score switch
        {
            >= 60 => EngineMarketBias.Bullish,
            <= 40 => EngineMarketBias.Bearish,
            _ => EngineMarketBias.Sideways
        };

        var confidence = 40m
            + Math.Min(25m, Math.Abs(roc10) * 10m)
            + Math.Min(15m, (decimal)Math.Abs(lastVolRatio - 1) * 20m)
            + (Math.Abs(rsi - 50) / 2m);

        sw.Stop();
        return EngineAnalysisResult.Known(
            "Momentum",
            score,
            Math.Clamp(confidence, 30, 92),
            InstitutionalWeights.Momentum,
            bias,
            signals,
            "profitdll-candles",
            sw.ElapsedMilliseconds);
    }

    private static decimal Roc(IReadOnlyList<decimal> closes, int period)
    {
        if (closes.Count <= period) return 0;
        var prev = closes[closes.Count - period];
        if (prev == 0) return 0;
        return (closes[^1] - prev) / prev * 100m;
    }

    private static decimal CalculateRsi(IReadOnlyList<decimal> closes, int period)
    {
        period = Math.Min(period, closes.Count - 1);
        if (period <= 0) return 50;

        decimal gains = 0;
        decimal losses = 0;
        for (var i = closes.Count - period; i < closes.Count; i++)
        {
            var diff = closes[i] - closes[i - 1];
            if (diff >= 0) gains += diff;
            else losses += Math.Abs(diff);
        }

        if (losses == 0) return 100;
        var rs = gains / losses;
        return 100m - 100m / (1 + rs);
    }

    private static (decimal Macd, decimal Signal) CalculateMacd(IReadOnlyList<decimal> closes)
    {
        var ema12 = Ema(closes, 12);
        var ema26 = Ema(closes, 26);
        var macd = ema12 - ema26;
        var signal = macd * 0.85m;
        return (macd, signal);
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
}
