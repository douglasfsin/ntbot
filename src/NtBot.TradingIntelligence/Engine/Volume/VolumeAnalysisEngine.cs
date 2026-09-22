using System.Diagnostics;
using NtBot.Domain.Entities;
using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine.Volume;

public interface IVolumeAnalysisEngine
{
    EngineAnalysisResult Analyze(string asset, IReadOnlyList<Candle> candles);
}

/// <summary>
/// Volume a partir de tick/real volume das candles (MT5 mapeia tick_volume → Volume).
/// Detecta spikes e divergência preço×volume.
/// </summary>
public sealed class VolumeAnalysisEngine : IVolumeAnalysisEngine
{
    private const int MinCandles = 20;

    public EngineAnalysisResult Analyze(string asset, IReadOnlyList<Candle> candles)
    {
        var sw = Stopwatch.StartNew();
        if (candles.Count < MinCandles)
        {
            return EngineAnalysisResult.Unknown(
                "Volume",
                InstitutionalWeights.Volume,
                $"Candles insuficientes para Volume ({candles.Count}/{MinCandles}).",
                "candles");
        }

        var ordered = candles.OrderBy(c => c.OpenTime).ToList();
        var last = ordered[^1];
        var avgVol = ordered.TakeLast(30).Average(c => (double)Math.Max(1, c.Volume));
        var lastVol = (double)Math.Max(0, last.Volume);
        var ratio = avgVol > 0 ? lastVol / avgVol : 1;

        var score = 50;
        var signals = new List<string>();

        if (ratio >= 2.0)
        {
            score += last.Close >= last.Open ? 18 : -18;
            signals.Add($"spike de volume ({ratio:F1}× média)");
        }
        else if (ratio >= 1.35)
        {
            score += last.Close >= last.Open ? 12 : -12;
            signals.Add("volume acima da média");
        }
        else if (ratio < 0.65)
        {
            score -= 8;
            signals.Add("volume fraco / sem participação");
        }

        var divergence = DetectDivergence(ordered);
        if (divergence == EngineMarketBias.Bullish)
        {
            score += 10;
            signals.Add("divergência bullish preço×volume");
        }
        else if (divergence == EngineMarketBias.Bearish)
        {
            score -= 10;
            signals.Add("divergência bearish preço×volume");
        }

        if ((last.Delta ?? 0) > 0)
        {
            score += 6;
            signals.Add("delta comprador");
        }
        else if ((last.Delta ?? 0) < 0)
        {
            score -= 6;
            signals.Add("delta vendedor");
        }

        // Tendência de volume nas últimas barras com direção de preço
        var recent = ordered.TakeLast(8).ToList();
        var priceUp = recent[^1].Close > recent[0].Close;
        var volUp = recent.TakeLast(4).Average(c => (double)c.Volume) >
                    recent.Take(4).Average(c => (double)c.Volume);
        if (priceUp && volUp)
        {
            score += 6;
            signals.Add("volume confirma avanço");
        }
        else if (!priceUp && volUp)
        {
            score -= 6;
            signals.Add("volume confirma pressão vendedora");
        }

        score = Math.Clamp(score, 0, 100);
        var bias = score switch
        {
            >= 58 => EngineMarketBias.Bullish,
            <= 42 => EngineMarketBias.Bearish,
            _ => EngineMarketBias.Sideways
        };

        var confidence = Math.Clamp(42m + (decimal)Math.Abs(ratio - 1) * 28m, 30, 90);
        if (avgVol < 5)
        {
            confidence = Math.Min(confidence, 45);
            signals.Add("volume absoluto baixo (tick volume limitado)");
        }

        sw.Stop();
        return EngineAnalysisResult.Known(
            "Volume",
            score,
            confidence,
            InstitutionalWeights.Volume,
            bias,
            signals,
            "candles/mt5-tick-volume",
            sw.ElapsedMilliseconds);
    }

    private static EngineMarketBias DetectDivergence(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 24)
            return EngineMarketBias.Unknown;

        var recent = candles.TakeLast(12).ToList();
        var prior = candles.TakeLast(24).Take(12).ToList();
        var recentHigh = recent.Max(c => c.High);
        var priorHigh = prior.Max(c => c.High);
        var recentLow = recent.Min(c => c.Low);
        var priorLow = prior.Min(c => c.Low);
        var recentAvgVol = recent.Average(c => (double)c.Volume);
        var priorAvgVol = prior.Average(c => (double)c.Volume);

        if (recentLow < priorLow && recentAvgVol < priorAvgVol * 0.85)
            return EngineMarketBias.Bullish;
        if (recentHigh > priorHigh && recentAvgVol < priorAvgVol * 0.85)
            return EngineMarketBias.Bearish;
        return EngineMarketBias.Unknown;
    }
}
