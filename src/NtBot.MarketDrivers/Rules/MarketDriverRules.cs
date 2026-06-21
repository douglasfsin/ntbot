using NtBot.MarketDrivers.Configuration;
using NtBot.MarketDrivers.Models;
using NtBot.MarketIntelligence.Models;

namespace NtBot.MarketDrivers.Rules;

public interface IMarketDriverRule
{
    string Name { get; }
    IReadOnlyList<MarketDriver> Apply(MarketDriverContext context);
}

public static class MarketDriverRuleHelpers
{
    public static MarketDriver FromSnapshot(
        DriverSourceDefinition source,
        MarketSnapshot? snapshot,
        double? correlation = null)
    {
        if (snapshot is null)
        {
            return new MarketDriver
            {
                Symbol = source.Symbol,
                Category = source.Category,
                Name = source.Label,
                Impact = DriverImpactLevel.Neutral,
                Weight = source.Weight,
                Direction = DriverDirection.Neutral,
                Description = $"{source.Label} indisponível.",
                Recommendation = "Aguardar dados",
                Confidence = 0.3m
            };
        }

        var variation = snapshot.ChangePercent;
        if (source.Inverse)
            variation = -variation;

        var direction = variation switch
        {
            > 0.15m => DriverDirection.Bullish,
            < -0.15m => DriverDirection.Bearish,
            _ => DriverDirection.Neutral
        };

        var impact = ClassifyImpact(variation);
        var confidence = correlation.HasValue
            ? (decimal)Math.Clamp(Math.Abs(correlation.Value), 0.4, 0.95)
            : 0.75m;

        return new MarketDriver
        {
            Symbol = source.Symbol,
            Category = source.Category,
            Name = source.Label,
            CurrentValue = snapshot.Price,
            PreviousValue = snapshot.PreviousClose,
            Variation = snapshot.ChangePercent,
            Impact = impact,
            Weight = source.Weight,
            Direction = direction,
            Description = BuildDescription(source.Label, snapshot.ChangePercent, impact),
            Recommendation = ClassifyRecommendation(impact),
            LastUpdate = snapshot.Timestamp,
            Confidence = confidence
        };
    }

    public static MarketDriver FromMacro(MarketDriverContext context, DriverSourceDefinition source)
    {
        var macro = context.Macro;
        var score = macro.MacroScore switch
        {
            Macro.DTO.MacroRegimeLabel.Bullish => 1.2m,
            Macro.DTO.MacroRegimeLabel.Bearish => -1.2m,
            _ => 0m
        };

        var variation = score;
        if (source.Inverse)
            variation = -variation;

        var impact = ClassifyImpact(variation);
        return new MarketDriver
        {
            Symbol = source.Symbol,
            Category = source.Category,
            Name = source.Label,
            CurrentValue = (decimal)macro.Confidence,
            Variation = variation,
            Impact = impact,
            Weight = source.Weight,
            Direction = variation switch
            {
                > 0 => DriverDirection.Bullish,
                < 0 => DriverDirection.Bearish,
                _ => DriverDirection.Neutral
            },
            Description = $"Regime macro {macro.MacroScore}, liquidez {macro.Liquidity}, volatilidade {macro.Volatility}.",
            Recommendation = context.MacroRecommendation?.Action.ToString() ?? ClassifyRecommendation(impact),
            LastUpdate = macro.Timestamp,
            Confidence = macro.Confidence / 100m
        };
    }

    public static MarketDriver FromFed(MarketDriverContext context, DriverSourceDefinition source)
    {
        var rate = context.Macro.InterestRate;
        var variation = rate switch
        {
            Macro.DTO.MacroLevel.High => -0.8m,
            Macro.DTO.MacroLevel.Low => 0.8m,
            _ => 0m
        };

        var impact = ClassifyImpact(variation);
        return new MarketDriver
        {
            Symbol = source.Symbol,
            Category = source.Category,
            Name = source.Label,
            Variation = variation,
            Impact = impact,
            Weight = source.Weight,
            Direction = variation >= 0 ? DriverDirection.Bullish : DriverDirection.Bearish,
            Description = $"Política monetária (juros {rate}) influencia fluxo cambial.",
            Recommendation = ClassifyRecommendation(impact),
            LastUpdate = context.Macro.Timestamp,
            Confidence = 0.7m
        };
    }

    public static MarketDriver FromFlow(MarketDriverContext context, DriverSourceDefinition source)
    {
        var impactResult = context.AssetImpact;
        var flowLabel = impactResult?.ImpactScore switch
        {
            >= 0.35 => "Comprador",
            <= -0.35 => "Vendedor",
            _ => "Neutro"
        };

        var variation = (decimal)(impactResult?.ImpactScore ?? 0) * 100m;
        var impact = ClassifyImpact(variation);

        return new MarketDriver
        {
            Symbol = source.Symbol,
            Category = source.Category,
            Name = source.Label,
            Variation = variation,
            Impact = impact,
            Weight = source.Weight,
            Direction = variation >= 0 ? DriverDirection.Bullish : DriverDirection.Bearish,
            Description = $"Fluxo institucional {flowLabel.ToLowerInvariant()}.",
            Recommendation = flowLabel,
            LastUpdate = context.Overview.Timestamp,
            Confidence = 0.68m
        };
    }

    public static MarketDriver FromCorrelation(MarketDriverContext context, DriverSourceDefinition source)
    {
        var corr = context.AssetImpact?.Factors.FirstOrDefault()?.Correlation ?? 0;
        var variation = (decimal)corr;
        var impact = ClassifyImpact(variation * 50m);

        return new MarketDriver
        {
            Symbol = source.Symbol,
            Category = source.Category,
            Name = source.Label,
            CurrentValue = (decimal)corr,
            Variation = variation,
            Impact = impact,
            Weight = source.Weight,
            Direction = corr >= 0 ? DriverDirection.Bullish : DriverDirection.Bearish,
            Description = $"Correlação média {corr:0.00} com drivers do ativo.",
            Recommendation = context.AssetImpact?.Recommendation ?? ClassifyRecommendation(impact),
            LastUpdate = context.Correlation.Timestamp,
            Confidence = (decimal)Math.Clamp(Math.Abs(corr), 0.5, 0.92)
        };
    }

    public static MarketDriver FromMomentum(MarketDriverContext context, DriverSourceDefinition source)
    {
        var avg = context.Overview.Indexes
            .Where(i => i.Symbol != "^VIX")
            .DefaultIfEmpty()
            .Average(i => i?.ChangePercent ?? 0);

        var variation = source.Inverse ? -avg : avg;
        return FromSynthetic(source, variation, "Momentum de índices globais.");
    }

    public static MarketDriver FromSynthetic(DriverSourceDefinition source, decimal variation, string description)
    {
        var impact = ClassifyImpact(variation);
        return new MarketDriver
        {
            Symbol = source.Symbol,
            Category = source.Category,
            Name = source.Label,
            Variation = variation,
            Impact = impact,
            Weight = source.Weight,
            Direction = variation >= 0 ? DriverDirection.Bullish : DriverDirection.Bearish,
            Description = description,
            Recommendation = ClassifyRecommendation(impact),
            Confidence = 0.65m
        };
    }

    public static MarketDriver FromIndicator(
        DriverSourceDefinition source,
        Macro.DTO.MacroIndicatorValue? indicator,
        bool inverse = false)
    {
        if (indicator?.Value is null)
            return FromSynthetic(source, 0, $"{source.Label} indisponível.");

        var variation = indicator.Value.Value;
        if (inverse)
            variation = -variation;

        var impact = ClassifyImpact(variation);
        return new MarketDriver
        {
            Symbol = source.Symbol,
            Category = source.Category,
            Name = source.Label,
            CurrentValue = indicator.Value,
            Variation = variation,
            Impact = impact,
            Weight = source.Weight,
            Direction = variation >= 0 ? DriverDirection.Bullish : DriverDirection.Bearish,
            Description = $"{source.Label} em {indicator.Value:F2}.",
            Recommendation = ClassifyRecommendation(impact),
            LastUpdate = indicator.ObservedAt ?? DateTime.UtcNow,
            Confidence = 0.72m
        };
    }

    public static DriverImpactLevel ClassifyImpact(decimal variation) => variation switch
    {
        >= 2.0m => DriverImpactLevel.VeryPositive,
        >= 0.8m => DriverImpactLevel.Positive,
        >= 0.2m => DriverImpactLevel.SlightlyPositive,
        <= -2.0m => DriverImpactLevel.VeryNegative,
        <= -0.8m => DriverImpactLevel.Negative,
        <= -0.2m => DriverImpactLevel.SlightlyNegative,
        _ => DriverImpactLevel.Neutral
    };

    public static string ClassifyRecommendation(DriverImpactLevel impact) => impact switch
    {
        DriverImpactLevel.VeryPositive => "Muito Positivo",
        DriverImpactLevel.Positive => "Positivo",
        DriverImpactLevel.SlightlyPositive => "Levemente Positivo",
        DriverImpactLevel.VeryNegative => "Muito Negativo",
        DriverImpactLevel.Negative => "Negativo",
        DriverImpactLevel.SlightlyNegative => "Levemente Negativo",
        _ => "Neutro"
    };

    private static string BuildDescription(string label, decimal changePercent, DriverImpactLevel impact) =>
        $"{label} {(changePercent >= 0 ? "▲" : "▼")} {Math.Abs(changePercent):F2}% — impacto {ClassifyRecommendation(impact).ToLowerInvariant()}.";
}

public sealed class AssetDriverRule : IMarketDriverRule
{
    public string Name => "asset-drivers";

    public IReadOnlyList<MarketDriver> Apply(MarketDriverContext context)
    {
        var sources = context.DriverSources;
        if (sources.Count == 0)
        {
            var definition = MarketDriversCatalog.Find(context.Asset);
            if (definition is null)
                return [];
            sources = definition.Sources;
        }

        var drivers = new List<MarketDriver>();
        var snapshots = BuildSnapshotLookup(context.Overview);

        foreach (var source in sources)
        {
            drivers.Add(source.Symbol switch
            {
                "MACRO" => MarketDriverRuleHelpers.FromMacro(context, source),
                "MACRO_FED" => MarketDriverRuleHelpers.FromFed(context, source),
                "FLOW" => MarketDriverRuleHelpers.FromFlow(context, source),
                "CORR" => MarketDriverRuleHelpers.FromCorrelation(context, source),
                "MOM" => MarketDriverRuleHelpers.FromMomentum(context, source),
                "DX-Y.NYB" => MarketDriverRuleHelpers.FromIndicator(
                    source,
                    context.Macro.Indicators.FirstOrDefault(i => i.SeriesId == "YAHOO_DXY"),
                    source.Inverse),
                _ when source.Category == MarketDriverCategory.Correlacao &&
                         context.Asset == "WIN" =>
                    BuildWinComponentDriver(context, source),
                _ => MarketDriverRuleHelpers.FromSnapshot(
                    source,
                    FindSnapshot(snapshots, source.Symbol),
                    GetCorrelation(context, source.Symbol))
            });
        }

        return drivers;
    }

    private static MarketDriver BuildWinComponentDriver(MarketDriverContext context, DriverSourceDefinition source)
    {
        var factor = context.AssetImpact?.Factors.FirstOrDefault(f =>
            string.Equals(f.Label, source.Label, StringComparison.OrdinalIgnoreCase));

        var corr = factor?.Correlation ?? 0;
        var variation = (decimal)(factor?.Weight * corr * 100);
        var impact = MarketDriverRuleHelpers.ClassifyImpact(variation);

        return new MarketDriver
        {
            Symbol = source.Symbol,
            Category = source.Category,
            Name = source.Label,
            Variation = variation,
            Impact = impact,
            Weight = source.Weight,
            Direction = corr >= 0 ? DriverDirection.Bullish : DriverDirection.Bearish,
            Description = $"Peso basket {factor?.Weight:P0} · correlação {corr:0.00}.",
            Recommendation = MarketDriverRuleHelpers.ClassifyRecommendation(impact),
            Confidence = (decimal)Math.Clamp(Math.Abs(corr), 0.45, 0.9)
        };
    }

    private static double? GetCorrelation(MarketDriverContext context, string symbol)
    {
        var factor = context.AssetImpact?.Factors.FirstOrDefault(f =>
            string.Equals(f.Symbol, symbol, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(f.Label, symbol, StringComparison.OrdinalIgnoreCase));
        return factor?.Correlation;
    }

    private static Dictionary<string, MarketSnapshot> BuildSnapshotLookup(MarketOverview overview)
    {
        var all = overview.Commodities
            .Concat(overview.Indexes)
            .Concat(overview.Currencies)
            .Concat(overview.Treasury)
            .Concat(overview.Sectors);

        if (overview.Vix is not null)
            all = all.Append(overview.Vix);

        return all.GroupBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static MarketSnapshot? FindSnapshot(IReadOnlyDictionary<string, MarketSnapshot> lookup, string symbol) =>
        lookup.TryGetValue(symbol, out var snap) ? snap : null;
}

public sealed class CalendarDriverRule : IMarketDriverRule
{
    public string Name => "calendar-drivers";

    public IReadOnlyList<MarketDriver> Apply(MarketDriverContext context)
    {
        var upcoming = context.Macro.UpcomingEvents
            .Where(e => e.Impact is "High" or "Medium")
            .OrderBy(e => e.EventTime)
            .Take(3)
            .ToList();

        if (upcoming.Count == 0)
            return [];

        return upcoming.Select(e =>
        {
            var hours = (e.EventTime - DateTime.UtcNow).TotalHours;
            var urgency = hours <= 24 ? -0.5m : 0m;
            var impact = MarketDriverRuleHelpers.ClassifyImpact(urgency);
            return new MarketDriver
            {
                Symbol = e.Id.ToString(),
                Category = MarketDriverCategory.EventosEconomicos,
                Name = e.EventName,
                Description = $"{e.Country} · {e.Impact} impact · {e.EventTime:g} UTC",
                Variation = urgency,
                Impact = impact,
                Weight = DriverScoreWeights.Calendar,
                Direction = DriverDirection.Neutral,
                Recommendation = hours <= 24 ? "Evento iminente" : "Monitorar",
                LastUpdate = e.EventTime,
                Confidence = 0.6m
            };
        }).ToList();
    }
}
