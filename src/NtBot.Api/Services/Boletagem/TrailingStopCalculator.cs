using NtBot.Shared.Trading;

namespace NtBot.Api.Services.Boletagem;

/// <summary>
/// Trailing stop técnico: ratchet só no sentido favorável.
/// Buy — peak só sobe; SL = peak*(1-pct/100); aplica só se SL novo &gt; SL atual.
/// Sell — peak só desce; SL = peak*(1+pct/100); aplica só se SL novo &lt; SL atual.
/// Também expõe lock absoluto em dólares de preço (ScalpShort: +$7 → SL +$2).
/// O SL técnico inicial (2 ticks além do candle anterior) permanece até o trigger de BE.
/// </summary>
public static class TrailingStopCalculator
{
    public readonly record struct Update(
        decimal PeakFavorablePrice,
        decimal TrailingStopPrice,
        bool ShouldApply);

    public static Update? TryAdvance(
        string direction,
        decimal currentPrice,
        decimal trailingPercent,
        decimal? entryPrice,
        decimal? previousPeak,
        decimal? currentStopLoss)
    {
        if (trailingPercent <= 0 || currentPrice <= 0)
            return null;

        var isBuy = direction.Equals("Buy", StringComparison.OrdinalIgnoreCase);
        var baseline = entryPrice is > 0 ? entryPrice.Value : currentPrice;

        if (isBuy)
        {
            var peak = previousPeak is > 0
                ? Math.Max(previousPeak.Value, currentPrice)
                : Math.Max(baseline, currentPrice);
            var candidateSl = RoundPrice(peak * (1m - trailingPercent / 100m));
            var shouldApply = currentStopLoss is null or <= 0 || candidateSl > currentStopLoss.Value;
            return new Update(peak, shouldApply ? candidateSl : (currentStopLoss ?? candidateSl), shouldApply);
        }

        // Sell: peak favorável = menor preço desde a entrada
        var sellPeak = previousPeak is > 0
            ? Math.Min(previousPeak.Value, currentPrice)
            : Math.Min(baseline, currentPrice);
        var sellSl = RoundPrice(sellPeak * (1m + trailingPercent / 100m));
        var applySell = currentStopLoss is null or <= 0 || sellSl < currentStopLoss.Value;
        return new Update(sellPeak, applySell ? sellSl : (currentStopLoss ?? sellSl), applySell);
    }

    /// <summary>
    /// Lock absoluto ScalpShort: lucro flutuante de preço ≥ $7 → SL em entry ± $2 favorável.
    /// </summary>
    public static Update? TryAbsoluteProfitLock(
        string direction,
        decimal currentPrice,
        decimal? entryPrice,
        decimal? previousPeak,
        decimal? currentStopLoss,
        decimal triggerDistance = ScalpShortRiskRules.BreakevenTriggerDistance,
        decimal lockDistance = ScalpShortRiskRules.BreakevenLockDistance)
    {
        if (entryPrice is null or <= 0)
            return null;

        var lockUpdate = ScalpShortRiskRules.TryBreakevenLock(
            direction,
            currentPrice,
            entryPrice.Value,
            previousPeak,
            currentStopLoss,
            triggerDistance,
            lockDistance);

        if (lockUpdate is null)
            return null;

        return new Update(
            lockUpdate.Value.PeakFavorablePrice,
            lockUpdate.Value.StopLoss,
            lockUpdate.Value.ShouldApply);
    }

    private static decimal RoundPrice(decimal price) =>
        Math.Round(price, 5, MidpointRounding.AwayFromZero);
}
