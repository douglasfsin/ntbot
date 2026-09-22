using NtBot.Domain.Entities;
using NtBot.Shared.Trading;

namespace NtBot.Api.Services.Boletagem;

public interface IBoletaStrategy
{
    string Name { get; }
    IReadOnlyList<BoletaStrategyPlanDto> BuildPlan(BoletaStrategyContext context);
}

public sealed class BoletaStrategyContext
{
    public string Symbol { get; init; } = "";
    public decimal LotSize { get; init; }
    public int ConfluenceScore { get; init; }
    public string Recommendation { get; init; } = "NEUTRO";
    public string Bias { get; init; } = "Sideways";
    public string RiskLevel { get; init; } = "Moderado";
    public int WyckoffScore { get; init; }
    public decimal? SuggestedEntry { get; init; }
    public decimal? SuggestedStopLoss { get; init; }
    public decimal? SuggestedTakeProfit { get; init; }
    public decimal? LastPrice { get; init; }

    /// <summary>Buy | Sell definido pelo operador — ignora o cálculo automático.</summary>
    public string? ForcedDirection { get; init; }

    /// <summary>Recomendação do painel Market Drivers (COMPRA MODERADA, VENDA…).</summary>
    public string? MarketDriversRecommendation { get; init; }

    /// <summary>Permite abrir mesmo com recomendação NEUTRO/AGUARDAR, usando bias/score.</summary>
    public bool AllowNeutralEntries { get; init; } = true;

    /// <summary>
    /// Quando há zonas TI, exige demanda (Buy) / oferta (Sell) alinhada à tendência.
    /// </summary>
    public bool PreferDemandWithTrend { get; init; } = true;

    /// <summary>Zonas operacionais (demanda/oferta) do TI.</summary>
    public IReadOnlyList<OperationalZoneHint> OperationalZones { get; init; } = [];

    /// <summary>High do candle completo imediatamente anterior ao da entrada (SL técnico ScalpShort).</summary>
    public decimal? PreviousCandleHigh { get; set; }

    /// <summary>Low do candle completo imediatamente anterior ao da entrada (SL técnico ScalpShort).</summary>
    public decimal? PreviousCandleLow { get; set; }

    /// <summary>Tick size do símbolo (broker ou fallback conhecido).</summary>
    public decimal? TickSize { get; set; }

    /// <summary>TF usado para o candle de referência do SL técnico (ex.: M5).</summary>
    public string? StopLossTimeframe { get; set; }

    public decimal? ReferencePrice => SuggestedEntry ?? LastPrice;

    public static string? NormalizeDirection(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "buy" or "compra" or "long" => "Buy",
            "sell" or "venda" or "short" => "Sell",
            _ => null
        };

    public static IReadOnlyList<OperationalZoneHint> MapZones(IEnumerable<BoletaZoneDto>? zones)
    {
        if (zones is null)
            return [];

        return zones
            .Where(z => z.PriceLow > 0 && z.PriceHigh > 0)
            .Select(z => new OperationalZoneHint(
                z.Type ?? "",
                z.Label ?? "",
                z.PriceLow,
                z.PriceHigh,
                z.ConfluenceScore))
            .ToList();
    }
}

internal static class BoletaPlanHelpers
{
    public static IReadOnlyList<BoletaStrategyPlanDto> Flat(string rationale) =>
    [
        new BoletaStrategyPlanDto
        {
            Level = 0,
            Direction = "Flat",
            Volume = 0,
            Rationale = rationale
        }
    ];

    /// <summary>
    /// Resolve direção + entrada preferindo zona de demanda/oferta na tendência.
    /// </summary>
    public static (string? Direction, decimal? Entry, string? ZoneNote, string? FlatReason) ResolveWithDemand(
        BoletaStrategyContext context)
    {
        var direction = WyckoffBoletaStrategy.ResolveDirection(context);
        if (direction is null)
        {
            var mdBlock = context.ForcedDirection is not null
                ? MarketDriversDirectionGuard.BlockReason(context.MarketDriversRecommendation, context.ForcedDirection)
                : null;
            var rationale = mdBlock
                ?? (string.IsNullOrWhiteSpace(context.MarketDriversRecommendation)
                    ? $"Sem direção: {context.Recommendation} · Bias {context.Bias} · Wyckoff {context.WyckoffScore} · Confluence {context.ConfluenceScore}. "
                      + "Escolha COMPRA/VENDA manualmente ou habilite entradas em cenário neutro."
                    : $"Sem direção alinhada a Market Drivers ({context.MarketDriversRecommendation}).");
            return (null, null, null, rationale);
        }

        var refPrice = context.ReferencePrice;
        var flatDemand = DemandTrendEntrySelector.FlatReasonIfNoAlignedZone(
            direction,
            refPrice,
            context.OperationalZones,
            requireWhenZonesPresent: context.PreferDemandWithTrend);

        if (flatDemand is not null)
            return (null, null, null, flatDemand);

        var zone = DemandTrendEntrySelector.FindAlignedZone(
            direction, refPrice, context.OperationalZones);

        if (zone is not null)
        {
            var entry = DemandTrendEntrySelector.PreferredEntry(direction, zone);
            var side = direction == "Buy" ? "demanda" : "oferta";
            var note =
                $" · {side} {zone.Label} [{zone.PriceLow:F2}–{zone.PriceHigh:F2}] · entrada {entry:F2}";
            return (direction, entry, note, null);
        }

        return (direction, refPrice, null, null);
    }
}

/// <summary>
/// Wyckoff: direção alinhada ao dashboard (recommendation/bias/Wyckoff score),
/// SL/TP preferindo sugestão do TradeRiskPlanner do TI.
/// Entrada preferencial em demanda (compra) / oferta (venda) quando zonas TI existem.
/// </summary>
public sealed class WyckoffBoletaStrategy : IBoletaStrategy
{
    public string Name => BoletaStrategies.Wyckoff;

    public IReadOnlyList<BoletaStrategyPlanDto> BuildPlan(BoletaStrategyContext context)
    {
        var (direction, entry, zoneNote, flatReason) = BoletaPlanHelpers.ResolveWithDemand(context);
        if (direction is null)
            return BoletaPlanHelpers.Flat(flatReason ?? "Sem setup.");

        var sl = context.SuggestedStopLoss;
        var tp = context.SuggestedTakeProfit;

        if (entry is > 0 && (sl is null or <= 0 || tp is null or <= 0))
        {
            var atrProxy = entry.Value * 0.003m;
            if (direction == "Buy")
            {
                sl ??= entry - atrProxy * 1.5m;
                tp ??= entry + atrProxy * 3m;
            }
            else
            {
                sl ??= entry + atrProxy * 1.5m;
                tp ??= entry - atrProxy * 3m;
            }
        }

        var riskNote = context.RiskLevel is "Alto" or "Extremo"
            ? " · risco elevado — volume reduzido"
            : "";

        var volume = context.RiskLevel is "Alto" or "Extremo"
            ? Math.Max(0.01m, Math.Round(context.LotSize * 0.5m, 2))
            : context.LotSize;

        return
        [
            new BoletaStrategyPlanDto
            {
                Level = 1,
                Direction = direction,
                Volume = volume,
                Entry = entry,
                StopLoss = sl,
                TakeProfit = tp,
                Rationale =
                    $"Wyckoff {context.WyckoffScore}/100 · Confluence {context.ConfluenceScore} · {context.Recommendation} · Bias {context.Bias}{zoneNote}{riskNote}"
            }
        ];
    }

    /// <summary>
    /// Direção: MD → Confluence rec → Bias (tendência) → Wyckoff/score em cenário lateral.
    /// </summary>
    internal static string? ResolveDirection(BoletaStrategyContext ctx)
    {
        var mdAllowed = MarketDriversDirectionGuard.AllowedDirectionOrNull(ctx.MarketDriversRecommendation);
        var forced = BoletaStrategyContext.NormalizeDirection(ctx.ForcedDirection);

        if (forced is not null)
        {
            if (!MarketDriversDirectionGuard.IsAllowed(ctx.MarketDriversRecommendation, forced))
                return null;
            return forced;
        }

        // Com bias de Market Drivers, a automática segue o MD (nunca o lado contrário).
        if (mdAllowed is not null)
            return mdAllowed;

        var rec = ctx.Recommendation ?? "";
        if (rec.Contains("COMPRA", StringComparison.OrdinalIgnoreCase)) return "Buy";
        if (rec.Contains("VENDA", StringComparison.OrdinalIgnoreCase)) return "Sell";

        // Favor da tendência: Bias antes de Wyckoff extremo.
        if (ctx.Bias.Equals("Bullish", StringComparison.OrdinalIgnoreCase)) return "Buy";
        if (ctx.Bias.Equals("Bearish", StringComparison.OrdinalIgnoreCase)) return "Sell";

        var neutral = rec.Contains("AGUARDAR", StringComparison.OrdinalIgnoreCase)
                      || rec.Contains("NEUTRO", StringComparison.OrdinalIgnoreCase)
                      || string.IsNullOrWhiteSpace(rec);

        if (neutral && !ctx.AllowNeutralEntries)
            return null;

        if (ctx.WyckoffScore >= 70) return "Buy";
        if (ctx.WyckoffScore is > 0 and <= 30) return "Sell";
        if (ctx.WyckoffScore >= 58) return "Buy";
        if (ctx.WyckoffScore is > 0 and <= 42) return "Sell";
        if (ctx.ConfluenceScore >= 55) return "Buy";
        if (ctx.ConfluenceScore is > 0 and <= 45) return "Sell";
        return null;
    }
}

/// <summary>
/// Gradiente linear: escala parcial em 3 alvos curtos (R:R ~0.6 / 1.2 / 2.0)
/// para progressão de lucro, com stop único.
/// </summary>
public sealed class LinearGradientBoletaStrategy : IBoletaStrategy
{
    public string Name => BoletaStrategies.LinearGradient;

    private static readonly (decimal rr, decimal volumeShare)[] Levels =
    [
        (0.6m, 0.40m),
        (1.2m, 0.35m),
        (2.0m, 0.25m)
    ];

    public IReadOnlyList<BoletaStrategyPlanDto> BuildPlan(BoletaStrategyContext context)
    {
        var (direction, entryOpt, zoneNote, flatReason) = BoletaPlanHelpers.ResolveWithDemand(context);
        if (direction is null)
            return BoletaPlanHelpers.Flat(flatReason ?? "Sem setup.");

        var entry = entryOpt ?? 0m;
        if (entry <= 0)
        {
            return BoletaPlanHelpers.Flat(
                "Sem preço de referência para montar alvos — aguarde cotação do símbolo ou informe o preço de entrada.");
        }

        var riskDist = context.SuggestedStopLoss is > 0
            ? Math.Abs(entry - context.SuggestedStopLoss.Value)
            : entry * 0.0025m;

        if (riskDist <= 0) riskDist = entry * 0.0025m;

        var sl = direction == "Buy" ? entry - riskDist : entry + riskDist;
        var plans = new List<BoletaStrategyPlanDto>();
        var level = 1;

        foreach (var (rr, share) in Levels)
        {
            var vol = Math.Max(0.01m, Math.Round(context.LotSize * share, 2));
            var tp = direction == "Buy"
                ? entry + riskDist * rr
                : entry - riskDist * rr;

            plans.Add(new BoletaStrategyPlanDto
            {
                Level = level++,
                Direction = direction,
                Volume = vol,
                Entry = entry,
                StopLoss = Math.Round(sl, 2),
                TakeProfit = Math.Round(tp, 2),
                Rationale =
                    $"Gradiente L{level - 1}: {share:P0} do lote · alvo R:R {rr:0.0}{zoneNote}"
            });
        }

        return plans;
    }
}

/// <summary>
/// Scalp curto: TP $10, SL técnico (2 ticks além da mín/máx do candle anterior M5),
/// BE em +$7 → SL em +$2. Multi-lote em 3 níveis — menor volume no nível mais adverso.
/// Referência de entrada: zona de demanda/oferta quando disponível.
/// </summary>
public sealed class ScalpShortBoletaStrategy : IBoletaStrategy
{
    public string Name => BoletaStrategies.ScalpShort;

    public IReadOnlyList<BoletaStrategyPlanDto> BuildPlan(BoletaStrategyContext context)
    {
        var (direction, entryOpt, zoneNote, flatReason) = BoletaPlanHelpers.ResolveWithDemand(context);
        if (direction is null)
            return BoletaPlanHelpers.Flat(flatReason ?? "Sem setup.");

        var entry = entryOpt ?? 0m;
        if (entry <= 0)
        {
            return BoletaPlanHelpers.Flat(
                "Scalp curto sem preço de referência — aguarde cotação ou informe o preço de entrada.");
        }

        var tf = string.IsNullOrWhiteSpace(context.StopLossTimeframe)
            ? SymbolTickSize.ScalpShortStopTimeframe
            : context.StopLossTimeframe!;

        if (context.PreviousCandleHigh is not > 0
            || context.PreviousCandleLow is not > 0
            || context.TickSize is not > 0)
        {
            return BoletaPlanHelpers.Flat(
                $"Scalp curto sem candle {tf} anterior — SL técnico indisponível. "
                + "Aguarde OHLCV do símbolo (não usa SL fixo).");
        }

        var levels = ScalpShortRiskRules.BuildLevels(
            direction,
            entry,
            context.LotSize,
            context.PreviousCandleHigh.Value,
            context.PreviousCandleLow.Value,
            context.TickSize.Value);

        if (levels.Count == 0)
        {
            return BoletaPlanHelpers.Flat(
                $"Scalp curto: falha ao calcular SL técnico (candle {tf} / tick {context.TickSize}).");
        }

        var slLabel =
            $"SL técnico {ScalpShortRiskRules.TechnicalStopTicks} ticks além da "
            + (direction == "Buy" ? "mín" : "máx")
            + $" do candle anterior ({tf})";

        return levels.Select(l => new BoletaStrategyPlanDto
        {
            Level = l.Level,
            Direction = direction,
            Volume = l.Volume,
            Entry = l.Entry,
            StopLoss = l.StopLoss,
            TakeProfit = l.TakeProfit,
            Rationale =
                $"Scalp L{l.Level}: {l.VolumeShare:P0} do lote · offset adverso ${l.AdverseOffset:0} · "
                + $"TP ${ScalpShortRiskRules.TakeProfitDistance:0} · {slLabel} · "
                + $"BE +${ScalpShortRiskRules.BreakevenTriggerDistance:0}→+${ScalpShortRiskRules.BreakevenLockDistance:0}"
                + $"{zoneNote} (menor tamanho no nível mais adverso à tendência)"
        }).ToList();
    }
}
