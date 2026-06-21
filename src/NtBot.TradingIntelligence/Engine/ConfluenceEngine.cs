using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine;

public sealed class ConfluenceEngine : IConfluenceEngine
{
    public ConfluenceScoreResult Calculate(EngineScoreInput input)
    {
        var components = new List<EngineScoreComponent>
        {
            Build("Macro", input.MacroScore, ConfluenceWeights.Macro),
            Build("Drivers", input.DriverScore, ConfluenceWeights.Drivers),
            Build("Wyckoff", input.WyckoffScore, ConfluenceWeights.Wyckoff),
            Build("SMC", input.SmcScore, ConfluenceWeights.Smc),
            Build("Volume", input.VolumeScore, ConfluenceWeights.Volume),
            Build("Momentum", input.MomentumScore, ConfluenceWeights.Momentum),
            Build("Correlação", input.CorrelationScore, ConfluenceWeights.Correlation),
            Build("Liquidez", input.LiquidityScore, ConfluenceWeights.Liquidity),
            Build("Calendário", input.CalendarScore, ConfluenceWeights.Calendar)
        };

        var weighted = components.Sum(c => c.WeightedContribution);
        var score = (int)Math.Clamp(Math.Round(weighted), 0, 100);
        var classification = ConfluenceClassification.Classify(score);

        var positive = components
            .Where(c => c.Score >= 65)
            .Select(c => $"{c.Engine} ({c.Score}/100, peso {(c.Weight * 100):F0}%)")
            .ToList();

        var negative = components
            .Where(c => c.Score <= 35)
            .Select(c => $"{c.Engine} ({c.Score}/100, peso {(c.Weight * 100):F0}%)")
            .ToList();

        var explanation = BuildExplanation(input.Asset, score, classification, components, positive, negative);

        return new ConfluenceScoreResult
        {
            Score = score,
            Classification = classification,
            Recommendation = ClassifyRecommendation(score),
            Confidence = CalculateConfidence(components),
            Components = components,
            PositiveFactors = positive,
            NegativeFactors = negative,
            Explanation = explanation
        };
    }

    private static EngineScoreComponent Build(string engine, int score, decimal weight) =>
        new()
        {
            Engine = engine,
            Score = score,
            Weight = weight,
            WeightedContribution = score * weight,
            Impact = ClassifyImpact(score),
            Tooltip = $"{engine}: {score}/100 · peso {(weight * 100):F0}%"
        };

    private static string ClassifyImpact(int score) => score switch
    {
        >= 80 => "Muito Positivo",
        >= 60 => "Positivo",
        >= 45 => "Neutro",
        >= 25 => "Negativo",
        _ => "Muito Negativo"
    };

    private static string ClassifyRecommendation(int score) => score switch
    {
        >= 85 => "COMPRA FORTE",
        >= 70 => "COMPRA",
        <= 15 => "VENDA FORTE",
        <= 30 => "VENDA",
        _ => "NEUTRO"
    };

    private static decimal CalculateConfidence(IReadOnlyList<EngineScoreComponent> components)
    {
        if (components.Count == 0) return 0;
        var spread = components.Max(c => c.Score) - components.Min(c => c.Score);
        var alignment = 100 - Math.Min(spread, 100);
        return Math.Clamp(alignment * 0.6m + (decimal)components.Average(c => c.Score) * 0.4m, 0, 100);
    }

    private static string BuildExplanation(
        string asset,
        int score,
        string classification,
        IReadOnlyList<EngineScoreComponent> components,
        IReadOnlyList<string> positive,
        IReadOnlyList<string> negative)
    {
        var lines = new List<string>
        {
            $"O ativo {asset} apresenta Confluence Score {score}/100 ({classification})."
        };

        if (positive.Count > 0)
            lines.Add($"Fatores positivos: {string.Join("; ", positive)}.");

        if (negative.Count > 0)
            lines.Add($"Fatores negativos: {string.Join("; ", negative)}.");

        var top = components.OrderByDescending(c => c.WeightedContribution).Take(3);
        lines.Add($"Maior contribuição: {string.Join(", ", top.Select(c => $"{c.Engine} ({c.Score})"))}.");

        var wyckoff = components.FirstOrDefault(c => c.Engine == "Wyckoff");
        var smc = components.FirstOrDefault(c => c.Engine == "SMC");
        if (wyckoff is not null && smc is not null && wyckoff.Score >= 60 && smc.Score >= 60)
            lines.Add("Wyckoff e SMC convergem em região favorável.");

        return string.Join("\n\n", lines);
    }
}
