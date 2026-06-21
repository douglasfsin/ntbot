using NtBot.MarketDrivers.Configuration;
using NtBot.MarketDrivers.Models;
using NtBot.MarketDrivers.Services;
using NtBot.MarketDrivers.Rules;

namespace NtBot.MarketDrivers.Engine;

public interface IMarketDriverEngine
{
    MarketDriversSnapshot BuildSnapshot(MarketDriverContext context, IReadOnlyList<MarketDriver> drivers);
}

public sealed class DriverScoreEngine
{
    public DriverScore Calculate(MarketDriverContext context, IReadOnlyList<MarketDriver> drivers)
    {
        var macro = ScoreDrivers(drivers, MarketDriverCategory.Macro, MarketDriverCategory.Sentimento);
        var quant = context.QuantScore.Score;
        var correlation = ScoreCorrelation(context);
        var commodities = ScoreDrivers(drivers, MarketDriverCategory.Commodities);
        var momentum = ScoreDrivers(drivers, MarketDriverCategory.Momentum, MarketDriverCategory.MarketBreadth);
        var volatility = ScoreVolatility(context);
        var calendar = ScoreCalendar(drivers);

        var weighted =
            macro * DriverScoreWeights.Macro +
            quant * DriverScoreWeights.Quant +
            correlation * DriverScoreWeights.Correlation +
            commodities * DriverScoreWeights.Commodities +
            momentum * DriverScoreWeights.Momentum +
            volatility * DriverScoreWeights.Volatility +
            calendar * DriverScoreWeights.Calendar;

        var score = (int)Math.Clamp(Math.Round(weighted), 0, 100);
        var confidence = CalculateConfidence(context, drivers);

        return new DriverScore
        {
            Score = score,
            Label = ClassifyLabel(score),
            Classification = ClassifyClassification(score),
            Recommendation = ClassifyRecommendation(score),
            Confidence = confidence,
            QuantProbability = EstimateQuantProbability(score, context),
            ComponentScores = new Dictionary<string, decimal>
            {
                ["Macro"] = macro,
                ["Quant"] = quant,
                ["Correlation"] = correlation,
                ["Commodities"] = commodities,
                ["Momentum"] = momentum,
                ["Volatility"] = volatility,
                ["Calendar"] = calendar
            }
        };
    }

    private static decimal ScoreDrivers(IReadOnlyList<MarketDriver> drivers, params MarketDriverCategory[] categories)
    {
        var filtered = drivers.Where(d => categories.Contains(d.Category)).ToList();
        if (filtered.Count == 0) return 50;

        decimal total = 0;
        decimal weightSum = 0;
        foreach (var driver in filtered)
        {
            var driverScore = ImpactToScore(driver.Impact);
            total += driverScore * driver.Weight;
            weightSum += driver.Weight;
        }

        return weightSum > 0 ? total / weightSum : 50;
    }

    private static decimal ScoreCorrelation(MarketDriverContext context)
    {
        var impact = context.AssetImpact?.ImpactScore ?? 0;
        return (decimal)Math.Clamp((impact + 1) / 2 * 100, 0, 100);
    }

    private static decimal ScoreVolatility(MarketDriverContext context)
    {
        var vix = context.Overview.Vix?.Price ?? 20;
        return vix switch
        {
            <= 15 => 85,
            <= 18 => 75,
            <= 22 => 60,
            <= 28 => 45,
            _ => 30
        };
    }

    private static decimal ScoreCalendar(IReadOnlyList<MarketDriver> drivers)
    {
        var events = drivers.Where(d => d.Category == MarketDriverCategory.EventosEconomicos).ToList();
        if (events.Count == 0) return 70;
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

    private static decimal CalculateConfidence(MarketDriverContext context, IReadOnlyList<MarketDriver> drivers)
    {
        if (drivers.Count == 0) return 0;
        var avg = drivers.Average(d => (double)d.Confidence);
        var macroBoost = (double)context.Macro.Confidence / 100 * 0.15;
        return (decimal)Math.Clamp((avg + macroBoost) * 100, 40, 95);
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
        >= 70 => "COMPRA",
        <= 15 => "VENDA FORTE",
        <= 30 => "VENDA",
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
            lines.Add($"O cenário macro continua {context.Macro.MacroScore.ToString().ToLowerInvariant()}.");

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
            var score = impact is null ? 50 : (int)Math.Clamp((impact.ImpactScore + 1) / 2 * 100, 0, 100);
            cells.Add(new MarketDriverHeatCell
            {
                Group = "Correlações",
                Symbol = asset,
                Label = asset,
                Score = score,
                Impact = MarketDriverRuleHelpers.ClassifyImpact((decimal)(impact?.ImpactScore ?? 0) * 50),
                Variation = (decimal)(impact?.ImpactScore ?? 0),
                Tooltip = impact?.Recommendation ?? "Sem dados"
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
