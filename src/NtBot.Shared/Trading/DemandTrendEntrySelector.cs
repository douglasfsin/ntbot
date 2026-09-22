namespace NtBot.Shared.Trading;

/// <summary>
/// Preferência de entrada: a favor da tendência e em zonas de demanda (compra) / oferta (venda).
/// </summary>
public sealed record OperationalZoneHint(
    string Type,
    string Label,
    decimal PriceLow,
    decimal PriceHigh,
    int ConfluenceScore = 0);

public static class DemandTrendEntrySelector
{
    /// <summary>Proximidade máxima do preço à zona (% do preço).</summary>
    public const decimal MaxProximityPercent = 1.5m;

    public static bool IsDemand(OperationalZoneHint zone)
    {
        var type = zone.Type ?? "";
        var label = zone.Label ?? "";
        if (type.Contains("Buy", StringComparison.OrdinalIgnoreCase)
            || type.Contains("StrongBuy", StringComparison.OrdinalIgnoreCase)
            || type.Contains("ModerateBuy", StringComparison.OrdinalIgnoreCase))
            return true;

        return label.Contains("Demanda", StringComparison.OrdinalIgnoreCase)
               || label.Contains("Demand", StringComparison.OrdinalIgnoreCase)
               || label.Contains("OB Compra", StringComparison.OrdinalIgnoreCase)
               || label.Contains("Order Block", StringComparison.OrdinalIgnoreCase)
                  && !label.Contains("Venda", StringComparison.OrdinalIgnoreCase)
                  && !label.Contains("Sell", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSupply(OperationalZoneHint zone)
    {
        var type = zone.Type ?? "";
        var label = zone.Label ?? "";
        if (type.Contains("Sell", StringComparison.OrdinalIgnoreCase)
            || type.Contains("StrongSell", StringComparison.OrdinalIgnoreCase)
            || type.Contains("ModerateSell", StringComparison.OrdinalIgnoreCase))
            return true;

        return label.Contains("Oferta", StringComparison.OrdinalIgnoreCase)
               || label.Contains("Supply", StringComparison.OrdinalIgnoreCase)
               || label.Contains("OB Venda", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Distância relativa do preço à zona (0 = dentro). null se inputs inválidos.
    /// </summary>
    public static decimal? DistancePercent(decimal price, OperationalZoneHint zone)
    {
        if (price <= 0 || zone.PriceHigh <= 0 || zone.PriceLow <= 0)
            return null;
        if (zone.PriceLow > zone.PriceHigh)
            return null;

        if (price >= zone.PriceLow && price <= zone.PriceHigh)
            return 0m;

        var edge = price < zone.PriceLow ? zone.PriceLow : zone.PriceHigh;
        return Math.Abs(price - edge) / price * 100m;
    }

    /// <summary>
    /// Melhor zona alinhada à direção (demanda p/ Buy, oferta p/ Sell) próxima do preço.
    /// </summary>
    public static OperationalZoneHint? FindAlignedZone(
        string direction,
        decimal? referencePrice,
        IReadOnlyList<OperationalZoneHint> zones,
        decimal maxProximityPercent = MaxProximityPercent)
    {
        if (zones is null || zones.Count == 0 || referencePrice is not > 0)
            return null;

        var isBuy = direction.Equals("Buy", StringComparison.OrdinalIgnoreCase);
        var isSell = direction.Equals("Sell", StringComparison.OrdinalIgnoreCase);
        if (!isBuy && !isSell)
            return null;

        OperationalZoneHint? best = null;
        var bestScore = decimal.MinValue;

        foreach (var zone in zones)
        {
            if (isBuy && !IsDemand(zone)) continue;
            if (isSell && !IsSupply(zone)) continue;

            var dist = DistancePercent(referencePrice.Value, zone);
            if (dist is null || dist > maxProximityPercent)
                continue;

            // Preferência: dentro/perto + maior confluence + faixa mais estreita (mais precisa).
            var width = Math.Max(0.01m, zone.PriceHigh - zone.PriceLow);
            var score = zone.ConfluenceScore * 10m - dist.Value * 100m - width / referencePrice.Value * 1000m;
            if (score > bestScore)
            {
                bestScore = score;
                best = zone;
            }
        }

        return best;
    }

    /// <summary>
    /// Preço de entrada preferido na zona: compra no desconto (demanda), venda no prêmio (oferta).
    /// </summary>
    public static decimal PreferredEntry(string direction, OperationalZoneHint zone)
    {
        var span = zone.PriceHigh - zone.PriceLow;
        if (span <= 0)
            return zone.PriceLow > 0 ? zone.PriceLow : zone.PriceHigh;

        var isBuy = direction.Equals("Buy", StringComparison.OrdinalIgnoreCase);
        // Buy: 25% da faixa a partir do low (desconto / demanda).
        // Sell: 75% da faixa (perto do high / oferta).
        return isBuy
            ? zone.PriceLow + span * 0.25m
            : zone.PriceLow + span * 0.75m;
    }

    /// <summary>
    /// Quando há zonas analisadas, exige confluência demanda/oferta com a tendência.
    /// null = ok; mensagem = Flat.
    /// </summary>
    public static string? FlatReasonIfNoAlignedZone(
        string direction,
        decimal? referencePrice,
        IReadOnlyList<OperationalZoneHint>? zones,
        bool requireWhenZonesPresent = true)
    {
        if (!requireWhenZonesPresent)
            return null;
        if (zones is null || zones.Count == 0)
            return null;

        var zone = FindAlignedZone(direction, referencePrice, zones);
        if (zone is not null)
            return null;

        var side = direction.Equals("Buy", StringComparison.OrdinalIgnoreCase)
            ? "demanda"
            : "oferta";
        return $"Sem zona de {side} alinhada à tendência perto do preço — Flat (não opera contra tendência/sem confluência).";
    }
}
