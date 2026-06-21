using NtBot.MarketIntelligence.Configuration;
using NtBot.MarketIntelligence.Models;

namespace NtBot.MarketIntelligence.Engine;

public sealed class QuantScoreEngine
{
    public QuantScore Calculate(MarketOverview overview, CorrelationResult correlation)
    {
        var macro = ScoreMacro(overview);
        var commodities = ScoreCommodities(overview.Commodities);
        var corr = ScoreCorrelation(correlation);
        var volatility = ScoreVolatility(overview.Vix);
        var currencies = ScoreCurrencies(overview.Currencies);
        var momentum = ScoreMomentum(overview.Indexes);

        var weighted =
            macro * QuantScoreWeights.Macro +
            commodities * QuantScoreWeights.Commodities +
            corr * QuantScoreWeights.Correlation +
            volatility * QuantScoreWeights.Volatility +
            currencies * QuantScoreWeights.Currencies +
            momentum * QuantScoreWeights.Momentum;

        var score = (int)Math.Clamp(Math.Round(weighted), 0, 100);

        return new QuantScore
        {
            Score = score,
            Label = score switch
            {
                >= 75 => "Risk-On",
                >= 55 => "Neutral-Bullish",
                <= 25 => "Risk-Off",
                <= 45 => "Neutral-Bearish",
                _ => "Neutral"
            },
            ComponentScores = new Dictionary<string, decimal>
            {
                ["Macro"] = macro,
                ["Commodities"] = commodities,
                ["Correlation"] = corr,
                ["Volatility"] = volatility,
                ["Currencies"] = currencies,
                ["Momentum"] = momentum
            },
            Weights = new Dictionary<string, decimal>
            {
                ["Macro"] = QuantScoreWeights.Macro,
                ["Commodities"] = QuantScoreWeights.Commodities,
                ["Correlation"] = QuantScoreWeights.Correlation,
                ["Volatility"] = QuantScoreWeights.Volatility,
                ["Currencies"] = QuantScoreWeights.Currencies,
                ["Momentum"] = QuantScoreWeights.Momentum
            }
        };
    }

    private static decimal ScoreMacro(MarketOverview overview)
    {
        var spx = overview.Indexes.FirstOrDefault(i => i.Symbol == "^GSPC");
        if (spx is null) return 50;
        return NormalizeChange(spx.ChangePercent);
    }

    private static decimal ScoreCommodities(IReadOnlyList<MarketSnapshot> commodities)
    {
        if (commodities.Count == 0) return 50;
        var avg = commodities.Average(c => c.ChangePercent);
        return NormalizeChange(avg);
    }

    private static decimal ScoreCorrelation(CorrelationResult correlation)
    {
        if (correlation.AssetImpacts.Count == 0) return 50;
        var avg = correlation.AssetImpacts.Average(a => (decimal)a.ImpactScore);
        return (decimal)Math.Clamp((avg + 1) / 2 * 100, 0, 100);
    }

    private static decimal ScoreVolatility(MarketSnapshot? vix)
    {
        if (vix is null) return 50;
        // Lower VIX = higher score (inverse)
        var vixLevel = vix.Price;
        return vixLevel switch
        {
            <= 15 => 85,
            <= 20 => 70,
            <= 25 => 55,
            <= 30 => 40,
            _ => 25
        };
    }

    private static decimal ScoreCurrencies(IReadOnlyList<MarketSnapshot> currencies)
    {
        var brl = currencies.FirstOrDefault(c => c.Symbol == "BRL=X");
        if (brl is null) return 50;
        // BRL weakening often risk-off for BR assets — invert for score
        return NormalizeChange(-brl.ChangePercent);
    }

    private static decimal ScoreMomentum(IReadOnlyList<MarketSnapshot> indexes)
    {
        if (indexes.Count == 0) return 50;
        var avg = indexes.Where(i => i.Symbol != "^VIX").Average(i => i.ChangePercent);
        return NormalizeChange(avg);
    }

    private static decimal NormalizeChange(decimal changePercent) =>
        (decimal)Math.Clamp(50 + (double)changePercent * 8, 0, 100);
}
