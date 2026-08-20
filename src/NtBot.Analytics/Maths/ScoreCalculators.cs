namespace NtBot.Analytics.Maths;

public sealed record AuctionScoreInput(
    decimal? DeltaZ,
    decimal? VolumeZ,
    decimal? BookImbalance,
    decimal? GapPercent,
    decimal? DistanceVwapAtr,
    decimal? TrendStrength,
    decimal? VolatilityPercentile,
    decimal? OpeningRangeAtr);

public static class AuctionScoreCalculator
{
    public static int Score(AuctionScoreInput input, IReadOnlyDictionary<string, decimal> weights)
    {
        var w = Normalize(weights);
        decimal total = 0;
        total += Component(w, "Delta", SignedUnit(input.DeltaZ, 3m));
        total += Component(w, "Volume", SignedUnit(input.VolumeZ, 3m));
        total += Component(w, "Book", SignedUnit(((input.BookImbalance ?? 0.5m) - 0.5m) * 2m, 1m));
        total += Component(w, "Gap", SignedUnit(input.GapPercent, 1.5m));
        total += Component(w, "VWAP", SignedUnit(-(input.DistanceVwapAtr ?? 0), 2m));
        total += Component(w, "Trend", SignedUnit(input.TrendStrength, 100m));
        total += Component(w, "Volatility", SignedUnit(((input.VolatilityPercentile ?? 50) - 50m) / 50m, 1m));
        total += Component(w, "OpeningRange", SignedUnit(input.OpeningRangeAtr, 2m));
        return (int)Math.Clamp(Math.Round(total * 100m, MidpointRounding.AwayFromZero), -100, 100);
    }

    private static decimal Component(IReadOnlyDictionary<string, decimal> weights, string key, decimal unit)
        => weights.GetValueOrDefault(key) * Math.Clamp(unit, -1m, 1m);

    private static decimal SignedUnit(decimal? value, decimal scale)
    {
        if (value is null || scale == 0)
            return 0;
        return value.Value / scale;
    }

    private static Dictionary<string, decimal> Normalize(IReadOnlyDictionary<string, decimal> weights)
    {
        var sum = weights.Values.Where(v => v > 0).Sum();
        if (sum <= 0)
            return new Dictionary<string, decimal>(weights);
        return weights.ToDictionary(kv => kv.Key, kv => kv.Value / sum, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record QuantScoreInput(
    int Trend,
    int Volume,
    int Delta,
    int Aggression,
    int Book,
    int Vwap,
    int Volatility,
    int Auction,
    int MultiTimeframe,
    int CrossMarket);

public static class QuantMarketScoreCalculator
{
    public static (int Total, QuantScoreInput Components) Score(
        QuantScoreInput raw,
        IReadOnlyDictionary<string, decimal> weights)
    {
        var w = weights;
        decimal total = 0;
        total += w.GetValueOrDefault("Trend") * raw.Trend;
        total += w.GetValueOrDefault("Volume") * raw.Volume;
        total += w.GetValueOrDefault("Delta") * raw.Delta;
        total += w.GetValueOrDefault("Aggression") * raw.Aggression;
        total += w.GetValueOrDefault("Book") * raw.Book;
        total += w.GetValueOrDefault("VWAP") * raw.Vwap;
        total += w.GetValueOrDefault("Volatility") * raw.Volatility;
        total += w.GetValueOrDefault("Auction") * raw.Auction;
        total += w.GetValueOrDefault("MultiTimeframe") * raw.MultiTimeframe;
        total += w.GetValueOrDefault("CrossMarket") * raw.CrossMarket;
        var weightSum = w.Values.Where(v => v > 0).Sum();
        if (weightSum > 0)
            total /= weightSum;
        var clamped = (int)Math.Clamp(Math.Round(total, MidpointRounding.AwayFromZero), -100, 100);
        return (clamped, raw);
    }

    public static int ComponentFromZ(decimal? z, decimal scale = 2m)
    {
        if (z is null || scale == 0)
            return 0;
        return (int)Math.Clamp(Math.Round(z.Value / scale * 100m, MidpointRounding.AwayFromZero), -100, 100);
    }
}
