using System.Diagnostics;
using NtBot.Domain.Entities;
using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine.Volatility;

public interface IVolatilityEngine
{
    EngineAnalysisResult Analyze(string asset, IReadOnlyList<Candle> candles);
    decimal? GetAtrPercent(IReadOnlyList<Candle> candles);
}

/// <summary>
/// Regime de volatilidade (ATR%). Score próximo de 50 = normal; extremos penalizam direção via Anti-Loss.
/// </summary>
public sealed class VolatilityEngine : IVolatilityEngine
{
    private const int MinCandles = 20;

    public EngineAnalysisResult Analyze(string asset, IReadOnlyList<Candle> candles)
    {
        var sw = Stopwatch.StartNew();
        if (candles.Count < MinCandles)
        {
            return EngineAnalysisResult.Unknown(
                "Volatility",
                InstitutionalWeights.Volatility,
                $"Candles insuficientes para Volatility ({candles.Count}/{MinCandles}).",
                "candles");
        }

        var ordered = candles.OrderBy(c => c.OpenTime).ToList();
        var atrPct = GetAtrPercent(ordered) ?? 0;
        var signals = new List<string>();
        var score = 50;

        // Volatilidade não é direcional: favorece ambientes negociáveis (ATR moderado).
        if (atrPct is >= 0.15m and <= 0.90m)
        {
            score = 62;
            signals.Add($"ATR {atrPct:F2}% — ambiente negociável");
        }
        else if (atrPct > 1.5m)
        {
            score = 35;
            signals.Add($"ATR {atrPct:F2}% — volatilidade extrema");
        }
        else if (atrPct > 0.90m)
        {
            score = 42;
            signals.Add($"ATR {atrPct:F2}% — volatilidade elevada");
        }
        else
        {
            score = 40;
            signals.Add($"ATR {atrPct:F2}% — volatilidade comprimida");
        }

        // Leve viés pela expansão recente vs média
        var shortAtr = AverageTrueRange(ordered.TakeLast(7).ToList(), 5);
        var longAtr = AverageTrueRange(ordered, 14);
        if (longAtr > 0 && shortAtr > longAtr * 1.35m)
            signals.Add("expansão recente de ATR");
        else if (longAtr > 0 && shortAtr < longAtr * 0.7m)
            signals.Add("compressão recente de ATR");

        sw.Stop();
        return EngineAnalysisResult.Known(
            "Volatility",
            score,
            Math.Clamp(50m + Math.Abs(atrPct - 0.5m) * 20m, 35, 85),
            InstitutionalWeights.Volatility,
            EngineMarketBias.Sideways,
            signals,
            "candles",
            sw.ElapsedMilliseconds);
    }

    public decimal? GetAtrPercent(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < MinCandles)
            return null;

        var ordered = candles.OrderBy(c => c.OpenTime).ToList();
        var last = ordered[^1].Close;
        if (last <= 0)
            return null;

        var atr = AverageTrueRange(ordered, 14);
        return atr / last * 100m;
    }

    private static decimal AverageTrueRange(IReadOnlyList<Candle> candles, int period)
    {
        if (candles.Count < 2)
            return 0;

        var trs = new List<decimal>();
        for (var i = 1; i < candles.Count; i++)
        {
            var c = candles[i];
            var p = candles[i - 1];
            trs.Add(Math.Max(c.High - c.Low, Math.Max(Math.Abs(c.High - p.Close), Math.Abs(c.Low - p.Close))));
        }

        return trs.TakeLast(Math.Max(1, period)).DefaultIfEmpty(0).Average();
    }
}
