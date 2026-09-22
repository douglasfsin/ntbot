namespace NtBot.Shared.Trading;

/// <summary>
/// Regras do scalp curto:
/// TP +$10 (offset absoluto), SL técnico (2 ticks além da mín/máx do candle anterior),
/// BE em +$7 → trava SL em +$2.
/// Multi-lote: 3 níveis na tendência — menor volume no nível mais adverso.
/// </summary>
public static class ScalpShortRiskRules
{
    public const decimal TakeProfitDistance = 10m;
    public const int TechnicalStopTicks = 2;
    public const decimal BreakevenTriggerDistance = 7m;
    public const decimal BreakevenLockDistance = 2m;

    /// <summary>Espaçamento entre níveis de entrada (dólares de preço).</summary>
    public const decimal EntryStaggerDistance = 3m;

    /// <summary>
    /// Shares do lote: L1 mais adverso (menor) → L3 referência/com tendência (maior).
    /// </summary>
    public static readonly (decimal AdverseOffset, decimal VolumeShare)[] Levels =
    [
        (2m * EntryStaggerDistance, 0.20m), // mais longe do preço, contra o fluxo
        (1m * EntryStaggerDistance, 0.30m),
        (0m, 0.50m)                        // no preço de referência (pullback / com tendência)
    ];

    public readonly record struct PlanLevel(
        int Level,
        decimal Entry,
        decimal StopLoss,
        decimal TakeProfit,
        decimal Volume,
        decimal VolumeShare,
        decimal AdverseOffset);

    public readonly record struct LockUpdate(
        decimal PeakFavorablePrice,
        decimal StopLoss,
        bool ShouldApply);

    /// <summary>
    /// SL técnico: Buy = prevLow − N ticks; Sell = prevHigh + N ticks.
    /// Referência = candle completo imediatamente anterior ao candle da entrada.
    /// </summary>
    public static decimal? ComputeTechnicalStopLoss(
        string direction,
        decimal previousHigh,
        decimal previousLow,
        decimal tickSize,
        int ticksBeyond = TechnicalStopTicks)
    {
        if (previousHigh <= 0 || previousLow <= 0 || tickSize <= 0 || ticksBeyond < 0)
            return null;
        if (previousLow > previousHigh)
            return null;

        var isBuy = direction.Equals("Buy", StringComparison.OrdinalIgnoreCase);
        var offset = ticksBeyond * tickSize;
        var sl = isBuy
            ? previousLow - offset
            : previousHigh + offset;

        if (sl <= 0)
            return null;

        return SymbolTickSize.RoundToTick(sl, tickSize);
    }

    public static IReadOnlyList<PlanLevel> BuildLevels(
        string direction,
        decimal referencePrice,
        decimal lotSize,
        decimal previousHigh,
        decimal previousLow,
        decimal tickSize)
    {
        if (referencePrice <= 0 || lotSize <= 0)
            return [];

        var technicalSl = ComputeTechnicalStopLoss(direction, previousHigh, previousLow, tickSize);
        if (technicalSl is null or <= 0)
            return [];

        var isBuy = direction.Equals("Buy", StringComparison.OrdinalIgnoreCase);
        var plans = new List<PlanLevel>(Levels.Length);
        var level = 1;

        foreach (var (adverseOffset, share) in Levels)
        {
            // Buy: níveis adversos abaixo do preço; Sell: acima.
            var entry = isBuy
                ? referencePrice - adverseOffset
                : referencePrice + adverseOffset;

            // Nível inválido se o SL técnico ficar do lado errado da entrada
            // (ex.: pullback adverso além da mín do candle de referência).
            if (isBuy && technicalSl.Value >= entry)
                continue;
            if (!isBuy && technicalSl.Value <= entry)
                continue;

            var tp = isBuy
                ? entry + TakeProfitDistance
                : entry - TakeProfitDistance;

            var vol = Math.Max(0.01m, Math.Round(lotSize * share, 2));

            plans.Add(new PlanLevel(
                level++,
                RoundPrice(entry, tickSize),
                technicalSl.Value,
                RoundPrice(tp, tickSize),
                vol,
                share,
                adverseOffset));
        }

        return plans;
    }

    /// <summary>
    /// Quando o preço avançou ≥ triggerDistance a favor, move SL para entry ± lockDistance (lucro mínimo travado).
    /// Não afrouxa um stop técnico já mais favorável; só aperta após +$7.
    /// </summary>
    public static LockUpdate? TryBreakevenLock(
        string direction,
        decimal currentPrice,
        decimal entryPrice,
        decimal? previousPeak,
        decimal? currentStopLoss,
        decimal triggerDistance = BreakevenTriggerDistance,
        decimal lockDistance = BreakevenLockDistance)
    {
        if (currentPrice <= 0 || entryPrice <= 0 || triggerDistance <= 0 || lockDistance < 0)
            return null;

        var isBuy = direction.Equals("Buy", StringComparison.OrdinalIgnoreCase);

        if (isBuy)
        {
            var peak = previousPeak is > 0
                ? Math.Max(previousPeak.Value, currentPrice)
                : Math.Max(entryPrice, currentPrice);
            var favorable = peak - entryPrice;
            if (favorable < triggerDistance)
                return new LockUpdate(peak, currentStopLoss ?? 0, false);

            var candidateSl = RoundPrice(entryPrice + lockDistance);
            var shouldApply = currentStopLoss is null or <= 0 || candidateSl > currentStopLoss.Value;
            return new LockUpdate(peak, shouldApply ? candidateSl : (currentStopLoss ?? candidateSl), shouldApply);
        }

        var sellPeak = previousPeak is > 0
            ? Math.Min(previousPeak.Value, currentPrice)
            : Math.Min(entryPrice, currentPrice);
        var sellFavorable = entryPrice - sellPeak;
        if (sellFavorable < triggerDistance)
            return new LockUpdate(sellPeak, currentStopLoss ?? 0, false);

        var sellSl = RoundPrice(entryPrice - lockDistance);
        var applySell = currentStopLoss is null or <= 0 || sellSl < currentStopLoss.Value;
        return new LockUpdate(sellPeak, applySell ? sellSl : (currentStopLoss ?? sellSl), applySell);
    }

    private static decimal RoundPrice(decimal price, decimal? tickSize = null) =>
        tickSize is > 0
            ? SymbolTickSize.RoundToTick(price, tickSize.Value)
            : Math.Round(price, 2, MidpointRounding.AwayFromZero);
}
