using NtBot.MarketDrivers.Configuration;
using NtBot.MarketDrivers.Models;
using NtBot.MarketDrivers.Services;
using NtBot.MarketDrivers.Rules;
using NtBot.Macro.DTO;

namespace NtBot.MarketDrivers.Engine;

public interface IMarketDriverEngine
{
    MarketDriversSnapshot BuildSnapshot(MarketDriverContext context, IReadOnlyList<MarketDriver> drivers);
}

public sealed class DriverScoreEngine
{
    public DriverScore Calculate(MarketDriverContext context, IReadOnlyList<MarketDriver> drivers)
    {
        var components = new (string Name, decimal? Score, decimal Weight)[]
        {
            ("Macro", ScoreDrivers(drivers, MarketDriverCategory.Macro, MarketDriverCategory.Sentimento), DriverScoreWeights.Macro),
            ("Quant", ScoreQuant(context), DriverScoreWeights.Quant),
            ("Correlation", ScoreCorrelation(context), DriverScoreWeights.Correlation),
            ("Commodities", ScoreDrivers(drivers, MarketDriverCategory.Commodities), DriverScoreWeights.Commodities),
            ("Momentum", ScoreDrivers(drivers, MarketDriverCategory.Momentum, MarketDriverCategory.MarketBreadth), DriverScoreWeights.Momentum),
            ("Volatility", ScoreVolatility(context), DriverScoreWeights.Volatility),
            ("Calendar", ScoreCalendar(drivers), DriverScoreWeights.Calendar)
        };

        var known = components.Where(c => c.Score.HasValue).ToList();
        var knownCount = known.Count;
        var totalCount = components.Length;

        int score;
        string dataQuality;
        if (knownCount == 0)
        {
            score = 50;
            dataQuality = "Insuficiente";
        }
        else
        {
            var weightSum = known.Sum(c => c.Weight);
            score = weightSum > 0
                ? (int)Math.Clamp(Math.Round(known.Sum(c => c.Score!.Value * c.Weight) / weightSum), 0, 100)
                : 50;
            dataQuality = knownCount >= totalCount * 0.75 ? "Alta" : knownCount >= totalCount * 0.5 ? "Parcial" : "Baixa";
        }

        var confidence = CalculateConfidence(context, drivers, knownCount, totalCount);

        var componentScores = components.ToDictionary(
            c => c.Name,
            c => c.Score ?? -1m);

        return new DriverScore
        {
            Score = score,
            Label = ClassifyLabel(score),
            Classification = ClassifyClassification(score),
            Recommendation = dataQuality == "Insuficiente" ? "AGUARDAR DADOS" : ClassifyRecommendation(score),
            Confidence = confidence,
            DataQuality = dataQuality,
            KnownComponentCount = knownCount,
            QuantProbability = EstimateQuantProbability(score, context),
            ComponentScores = componentScores
        };
    }

    private static decimal? ScoreQuant(MarketDriverContext context) =>
        context.QuantScore.Score is > 0 and not 50 ? context.QuantScore.Score : null;

    private static decimal? ScoreDrivers(IReadOnlyList<MarketDriver> drivers, params MarketDriverCategory[] categories)
    {
        var filtered = drivers
            .Where(d => categories.Contains(d.Category))
            .Where(d => !d.Description.Contains("indisponível", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (filtered.Count == 0) return null;

        decimal total = 0;
        decimal weightSum = 0;
        foreach (var driver in filtered)
        {
            var driverScore = ImpactToScore(driver.Impact);
            total += driverScore * driver.Weight;
            weightSum += driver.Weight;
        }

        return weightSum > 0 ? total / weightSum : null;
    }

    private static decimal? ScoreCorrelation(MarketDriverContext context)
    {
        if (context.AssetImpact is null || context.AssetImpact.Factors.Count == 0)
            return null;

        var impact = context.AssetImpact.ImpactScore;
        return (decimal)Math.Clamp((impact + 1) / 2 * 100, 0, 100);
    }

    private static decimal? ScoreVolatility(MarketDriverContext context)
    {
        if (context.Overview.Vix is null)
            return null;

        var vix = context.Overview.Vix.Price;
        return vix switch
        {
            <= 15 => 85,
            <= 18 => 75,
            <= 22 => 60,
            <= 28 => 45,
            _ => 30
        };
    }

    private static decimal? ScoreCalendar(IReadOnlyList<MarketDriver> drivers)
    {
        var events = drivers
            .Where(d => d.Category == MarketDriverCategory.EventosEconomicos)
            .Where(d => !d.Description.Contains("indisponível", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (events.Count == 0) return null;
        return events.Any(e => e.Recommendation == "Evento iminente") ? 45 : 60;
    }

    private static decimal ImpactToScore(DriverImpactLevel impact) => impact switch
    {
        DriverImpactLevel.VeryPositive => 95,
        DriverImpactLevel.Positive => 80,
        DriverImpactLevel.SlightlyPositive => 65,
        DriverImpactLevel.SlightlyNegative => 35,
        DriverImpactLevel.Negative => 20,
        DriverImpactLevel.VeryNegative => 5,
        _ => 50
    };

    private static decimal CalculateConfidence(
        MarketDriverContext context,
        IReadOnlyList<MarketDriver> drivers,
        int knownComponents,
        int totalComponents)
    {
        if (drivers.Count == 0) return 0;

        var available = drivers
            .Where(d => !d.Description.Contains("indisponível", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (available.Count == 0) return 0;

        var avg = available.Average(d => (double)d.Confidence);
        var macroBoost = (double)context.Macro.Confidence / 100 * 0.15;
        var coverage = totalComponents > 0 ? (double)knownComponents / totalComponents : 0;
        return (decimal)Math.Clamp((avg + macroBoost) * coverage * 100, 0, 95);
    }

    private static decimal? EstimateQuantProbability(int score, MarketDriverContext context)
    {
        var baseProb = score * 0.75m + context.QuantScore.Score * 0.25m;
        return Math.Clamp(baseProb, 0, 100);
    }

    public static string ClassifyLabel(int score) => score switch
    {
        >= 80 => "Forte",
        >= 60 => "Moderado",
        >= 40 => "Neutro",
        >= 20 => "Fraco",
        _ => "Muito Fraco"
    };

    public static string ClassifyClassification(int score) => score switch
    {
        >= 95 => "Muito Forte",
        >= 80 => "Forte",
        >= 60 => "Moderado",
        >= 40 => "Neutro",
        >= 20 => "Fraco",
        _ => "Muito Fraco"
    };

    public static string ClassifyRecommendation(int score) => score switch
    {
        >= 85 => "COMPRA FORTE",
        >= 70 => "COMPRA MODERADA",
        >= 58 => "COMPRA FRACA",
        <= 15 => "VENDA FORTE",
        <= 30 => "VENDA MODERADA",
        <= 42 => "VENDA FRACA",
        _ => "NEUTRO"
    };
}

public sealed class DriverExplanationEngine
{
    public string BuildExplanation(MarketDriverContext context, IReadOnlyList<MarketDriver> drivers, DriverScore score)
    {
        var positive = drivers.Where(d => d.Impact is DriverImpactLevel.VeryPositive or DriverImpactLevel.Positive or DriverImpactLevel.SlightlyPositive).ToList();
        var negative = drivers.Where(d => d.Impact is DriverImpactLevel.VeryNegative or DriverImpactLevel.Negative or DriverImpactLevel.SlightlyNegative).ToList();

        var lines = new List<string>
        {
            $"{context.Asset} apresenta cenário {(score.Score >= 70 ? "favorável" : score.Score <= 30 ? "desfavorável" : "neutro")}."
        };

        foreach (var driver in positive.Take(4))
        {
            if (driver.Category == MarketDriverCategory.Commodities || driver.Variation != 0)
                lines.Add($"{driver.Name} {(driver.Variation >= 0 ? "acumula alta" : "mostra estabilidade")} de {Math.Abs(driver.Variation):F1}%{(driver.Category == MarketDriverCategory.Volatilidade ? "" : ".")}");
        }

        var vix = drivers.FirstOrDefault(d => d.Name == "VIX");
        if (vix?.CurrentValue is not null && vix.CurrentValue <= 18)
            lines.Add($"O VIX permanece abaixo de 18 indicando menor aversão ao risco.");

        var flow = drivers.FirstOrDefault(d => d.Category == MarketDriverCategory.Fluxo);
        if (flow is not null)
            lines.Add($"O fluxo institucional permanece {flow.Recommendation.ToLowerInvariant()}.");

        var corr = drivers.FirstOrDefault(d => d.Category == MarketDriverCategory.Correlacao && d.Name == "Correlação")
                   ?? drivers.FirstOrDefault(d => d.Category == MarketDriverCategory.Correlacao);
        if (corr?.CurrentValue is not null && Math.Abs(corr.CurrentValue.Value) >= 0.5m)
            lines.Add($"A correlação entre {context.Asset} e drivers-chave está acima de {Math.Abs(corr.CurrentValue.Value):0.00}.");

        if (context.Macro.MacroScore != Macro.DTO.MacroRegimeLabel.Unknown)
        {
            var regime = MacroRegimeDisplay.ToLabel(context.Macro.MacroScore);
            lines.Add(context.Macro.MacroScore == Macro.DTO.MacroRegimeLabel.Bearish
                ? "O cenário macro continua em tendência de baixa."
                : $"O cenário macro continua {regime.ToLowerInvariant()}.");
        }

        if (negative.Count > 0 && score.Score < 70)
        {
            var negNames = string.Join(", ", negative.Take(2).Select(n => n.Name));
            lines.Add($"Fatores de pressão: {negNames}.");
        }

        lines.Add($"Recommendation Score {score.Score}/100.");
        lines.Add($"Confiança {score.Confidence:F0}%.");

        return string.Join("\n\n", lines);
    }
}

public sealed class MarketDriversHeatMapEngine
{
    public IReadOnlyList<MarketDriverHeatCell> BuildHeatMap(
        MarketDriverContext context,
        IReadOnlyList<MarketDriver> drivers)
    {
        var cells = new List<MarketDriverHeatCell>();

        foreach (var commodity in context.Overview.Commodities)
        {
            cells.Add(new MarketDriverHeatCell
            {
                Group = "Commodities",
                Symbol = commodity.Symbol,
                Label = commodity.Name,
                Score = ChangeToScore(commodity.ChangePercent),
                Impact = MarketDriverRuleHelpers.ClassifyImpact(commodity.ChangePercent),
                Variation = commodity.ChangePercent,
                Tooltip = $"{commodity.Name}: {commodity.ChangePercent:+0.00;-0.00;0.00}%"
            });
        }

        cells.Add(new MarketDriverHeatCell
        {
            Group = "Macro",
            Symbol = "LIQ",
            Label = "Liquidity",
            Score = MacroLevelToScore(context.Macro.Liquidity),
            Impact = MarketDriverRuleHelpers.ClassifyImpact(MacroLevelToVariation(context.Macro.Liquidity)),
            Variation = MacroLevelToVariation(context.Macro.Liquidity),
            Tooltip = $"Liquidez {context.Macro.Liquidity}"
        });
        cells.Add(new MarketDriverHeatCell
        {
            Group = "Macro",
            Symbol = "DXY",
            Label = "Dollar",
            Score = MacroLevelToScore(context.Macro.DollarStrength, invert: true),
            Impact = MarketDriverRuleHelpers.ClassifyImpact(-MacroLevelToVariation(context.Macro.DollarStrength)),
            Variation = MacroLevelToVariation(context.Macro.DollarStrength),
            Tooltip = $"Dólar {context.Macro.DollarStrength}"
        });
        cells.Add(new MarketDriverHeatCell
        {
            Group = "Macro",
            Symbol = "INF",
            Label = "Inflation",
            Score = MacroLevelToScore(context.Macro.Inflation, invert: true),
            Impact = MarketDriverRuleHelpers.ClassifyImpact(-MacroLevelToVariation(context.Macro.Inflation)),
            Variation = MacroLevelToVariation(context.Macro.Inflation),
            Tooltip = $"Inflação {context.Macro.Inflation}"
        });
        cells.Add(new MarketDriverHeatCell
        {
            Group = "Macro",
            Symbol = "INT",
            Label = "Interest",
            Score = MacroLevelToScore(context.Macro.InterestRate, invert: true),
            Impact = MarketDriverRuleHelpers.ClassifyImpact(-MacroLevelToVariation(context.Macro.InterestRate)),
            Variation = MacroLevelToVariation(context.Macro.InterestRate),
            Tooltip = $"Juros {context.Macro.InterestRate}"
        });
        cells.Add(new MarketDriverHeatCell
        {
            Group = "Macro",
            Symbol = "VOL",
            Label = "Volatility",
            Score = MacroLevelToScore(context.Macro.Volatility, invert: true),
            Impact = MarketDriverRuleHelpers.ClassifyImpact(-MacroLevelToVariation(context.Macro.Volatility)),
            Variation = MacroLevelToVariation(context.Macro.Volatility),
            Tooltip = $"Volatilidade {context.Macro.Volatility}"
        });

        foreach (var asset in MarketDriversCatalog.All.Select(a => a.Asset))
        {
            var impact = context.Correlation.AssetImpacts.FirstOrDefault(i =>
                string.Equals(i.Asset, asset, StringComparison.OrdinalIgnoreCase));

            if (impact is null || impact.Factors.Count == 0)
            {
                cells.Add(new MarketDriverHeatCell
                {
                    Group = "Correlações",
                    Symbol = asset,
                    Label = asset,
                    Score = 0,
                    Impact = DriverImpactLevel.Neutral,
                    Variation = 0,
                    Tooltip = "Sem dados de correlação"
                });
                continue;
            }

            var score = (int)Math.Clamp((impact.ImpactScore + 1) / 2 * 100, 0, 100);
            cells.Add(new MarketDriverHeatCell
            {
                Group = "Correlações",
                Symbol = asset,
                Label = asset,
                Score = score,
                Impact = MarketDriverRuleHelpers.ClassifyImpact((decimal)impact.ImpactScore * 50),
                Variation = (decimal)impact.ImpactScore,
                Tooltip = impact.Recommendation
            });
        }

        return cells;
    }

    private static int ChangeToScore(decimal change) =>
        (int)Math.Clamp(50 + (double)change * 8, 0, 100);

    private static int MacroLevelToScore(Macro.DTO.MacroLevel level, bool invert = false)
    {
        var score = level switch
        {
            Macro.DTO.MacroLevel.VeryHigh => 85,
            Macro.DTO.MacroLevel.High => 70,
            Macro.DTO.MacroLevel.Low => 30,
            Macro.DTO.MacroLevel.VeryLow => 15,
            _ => 50
        };
        return invert ? 100 - score : score;
    }

    private static decimal MacroLevelToVariation(Macro.DTO.MacroLevel level) => level switch
    {
        Macro.DTO.MacroLevel.VeryHigh => 1.5m,
        Macro.DTO.MacroLevel.High => 0.8m,
        Macro.DTO.MacroLevel.Low => -0.8m,
        Macro.DTO.MacroLevel.VeryLow => -1.5m,
        _ => 0m
    };
}

public sealed class MarketDriverEngine : IMarketDriverEngine
{
    private readonly DriverScoreEngine _scoreEngine;
    private readonly DriverExplanationEngine _explanationEngine;
    private readonly MarketDriversHeatMapEngine _heatMapEngine;
    private readonly MarketDriversAIService _aiService;

    public MarketDriverEngine(
        DriverScoreEngine scoreEngine,
        DriverExplanationEngine explanationEngine,
        MarketDriversHeatMapEngine heatMapEngine,
        MarketDriversAIService aiService)
    {
        _scoreEngine = scoreEngine;
        _explanationEngine = explanationEngine;
        _heatMapEngine = heatMapEngine;
        _aiService = aiService;
    }

    public MarketDriversSnapshot BuildSnapshot(MarketDriverContext context, IReadOnlyList<MarketDriver> drivers)
    {
        var score = _scoreEngine.Calculate(context, drivers);
        var explanation = _explanationEngine.BuildExplanation(context, drivers, score);
        var heatMap = _heatMapEngine.BuildHeatMap(context, drivers);
        var aiSummary = _aiService.Summarize(context, drivers, score);

        return new MarketDriversSnapshot
        {
            Asset = context.Asset,
            Timestamp = DateTime.UtcNow,
            Drivers = drivers,
            Score = score,
            Explanation = explanation,
            HeatMap = heatMap,
            AiSummary = aiSummary
        };
    }
}
