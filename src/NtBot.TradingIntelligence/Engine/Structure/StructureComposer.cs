using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine.Structure;

/// <summary>
/// Combina Trend + SMC em um único engine "Structure" (peso 30%).
/// </summary>
public static class StructureComposer
{
    public static EngineAnalysisResult Compose(EngineAnalysisResult? trend, EngineAnalysisResult? smc)
    {
        var known = new List<EngineAnalysisResult>();
        if (trend is { Status: EngineDataStatus.Known, Score: not null })
            known.Add(trend);
        if (smc is { Status: EngineDataStatus.Known, Score: not null })
            known.Add(smc);

        if (known.Count == 0)
        {
            return EngineAnalysisResult.Unknown(
                "Structure",
                InstitutionalWeights.Structure,
                "Estrutura indisponível — Trend/SMC sem dados.",
                "structure");
        }

        // SMC levemente mais peso dentro da estrutura (55/45)
        decimal score;
        if (trend?.Score is int t && smc?.Score is int s)
            score = s * 0.55m + t * 0.45m;
        else
            score = (decimal)known.Average(e => e.Score!.Value);

        var confidence = (decimal)known.Average(e => (double)e.Confidence);
        var signals = known.SelectMany(e => e.Signals).Distinct().Take(6).ToList();

        var bullish = known.Count(e => e.Bias == EngineMarketBias.Bullish);
        var bearish = known.Count(e => e.Bias == EngineMarketBias.Bearish);
        var bias = bullish > bearish ? EngineMarketBias.Bullish
            : bearish > bullish ? EngineMarketBias.Bearish
            : score >= 58 ? EngineMarketBias.Bullish
            : score <= 42 ? EngineMarketBias.Bearish
            : EngineMarketBias.Sideways;

        return EngineAnalysisResult.Known(
            "Structure",
            (int)Math.Clamp(Math.Round(score), 0, 100),
            Math.Clamp(confidence, 30, 95),
            InstitutionalWeights.Structure,
            bias,
            signals,
            "trend+smc");
    }
}
