using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine;

public static class SpecialistAgentEngine
{
    private static readonly Dictionary<string, string> Specializations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WIN"] = "Mini Índice Bovespa",
        ["WDO"] = "Mini Dólar",
        ["PETR4"] = "Petrobras / Petróleo",
        ["VALE3"] = "Vale / Minério",
        ["XAUUSD"] = "Ouro",
        ["SP500"] = "S&P 500",
        ["NASDAQ"] = "Nasdaq",
        ["BTCUSD"] = "Bitcoin"
    };

    public static IReadOnlyList<AiAgentInsight> BuildInsights(string asset, TradingIntelligenceSnapshot snapshot)
    {
        var now = DateTime.UtcNow;
        var insights = new List<AiAgentInsight>();

        foreach (var cell in snapshot.HeatMap)
        {
            var agentId = cell.Engine switch
            {
                "Macro" => "macro-agent",
                "Drivers" => "drivers-agent",
                "Wyckoff" => "wyckoff-agent",
                "SMC" => "smc-agent",
                "Volume" => "volume-agent",
                _ => null
            };
            if (agentId is null) continue;

            insights.Add(new AiAgentInsight
            {
                AgentId = agentId,
                Asset = asset,
                Specialization = cell.Engine,
                Summary = SummarizeEngine(cell.Engine, cell.Score, snapshot),
                Highlights = BuildHighlights(cell.Engine, cell.Score, snapshot),
                GeneratedAt = now
            });
        }

        if (Specializations.TryGetValue(asset, out var spec))
        {
            insights.Add(new AiAgentInsight
            {
                AgentId = $"asset-{asset.ToLowerInvariant()}",
                Asset = asset,
                Specialization = spec,
                Summary = $"Visão especialista {asset}: confluence {snapshot.Confluence.Score}/100 ({snapshot.Confluence.Classification}).",
                Highlights =
                [
                    snapshot.Confluence.Recommendation,
                    ..snapshot.OperationalZones.Take(2).Select(z => z.Label)
                ],
                GeneratedAt = now
            });
        }

        return insights;
    }

    private static string SummarizeEngine(string engine, int score, TradingIntelligenceSnapshot snapshot) =>
        score switch
        {
            >= 70 => $"{engine} favorável ({score}/100) — reforça {snapshot.Confluence.Recommendation}.",
            <= 35 => $"{engine} pressionando ({score}/100) — cautela no setup.",
            _ => $"{engine} neutro ({score}/100)."
        };

    private static IReadOnlyList<string> BuildHighlights(string engine, int score, TradingIntelligenceSnapshot snapshot)
    {
        var items = new List<string> { $"Score {score}/100" };
        if (engine == "SMC" && snapshot.Intersections.Any(i => i.HighConfluence))
            items.Add($"Interseções: {string.Join(", ", snapshot.Intersections.Where(i => i.HighConfluence).Select(i => i.Pair))}");
        if (engine == "Drivers" && score >= 65)
            items.Add("Drivers alinhados com o ativo");
        return items;
    }
}
