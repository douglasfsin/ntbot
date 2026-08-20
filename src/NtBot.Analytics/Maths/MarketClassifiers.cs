namespace NtBot.Analytics.Maths;

public static class AbsorptionDetector
{
    /// <summary>
    /// High aggression + high volume + low price displacement → possible absorption.
    /// Does not claim institutional activity.
    /// </summary>
    public static (string? Label, decimal Strength) Detect(
        decimal? aggressionZ,
        decimal? volumeZ,
        decimal? rangeAtrRatio)
    {
        var aggression = Math.Abs(aggressionZ ?? 0);
        var volume = volumeZ ?? 0;
        var displacement = rangeAtrRatio ?? 1m;
        if (volume < 1.2m || aggression < 1.0m || displacement > 0.55m)
            return (null, 0m);

        var strength = Math.Clamp((volume + aggression) / 2m * (1m - Math.Min(displacement, 1m)), 0m, 5m);
        var label = (aggressionZ ?? 0) >= 0
            ? "POSSIBLE_BUY_ABSORPTION"
            : "POSSIBLE_SELL_ABSORPTION";
        return (label, Math.Round(strength, 4, MidpointRounding.AwayFromZero));
    }
}

public static class RegimeClassifier
{
    public static string Classify(
        string? trendDirection,
        decimal? trendStrength,
        decimal? volatilityPercentile,
        bool openingDrive,
        bool breakout)
    {
        if (openingDrive)
            return "OPENING_DRIVE";
        if (breakout)
            return "BREAKOUT";
        if (volatilityPercentile >= 80m)
            return "HIGH_VOLATILITY";
        if (volatilityPercentile <= 20m)
            return "LOW_VOLATILITY";
        if (string.Equals(trendDirection, "UP", StringComparison.OrdinalIgnoreCase) && (trendStrength ?? 0) >= 25m)
            return "TREND_UP";
        if (string.Equals(trendDirection, "DOWN", StringComparison.OrdinalIgnoreCase) && (trendStrength ?? 0) >= 25m)
            return "TREND_DOWN";
        if ((trendStrength ?? 0) < 15m)
            return "MEAN_REVERSION";
        return "RANGE";
    }
}

public static class MultiTimeframeAlignment
{
    public static string Classify(IReadOnlyDictionary<string, string> trends)
    {
        var dirs = new[] { "1m", "5m", "15m", "30m", "60m" }
            .Select(tf => trends.GetValueOrDefault(tf, "FLAT"))
            .ToArray();
        var up = dirs.Count(d => d.Equals("UP", StringComparison.OrdinalIgnoreCase));
        var down = dirs.Count(d => d.Equals("DOWN", StringComparison.OrdinalIgnoreCase));
        if (up == dirs.Length)
            return "FULL_BULLISH_ALIGNMENT";
        if (down == dirs.Length)
            return "FULL_BEARISH_ALIGNMENT";
        if (up >= 4)
            return "BULLISH_ALIGNMENT";
        if (down >= 4)
            return "BEARISH_ALIGNMENT";
        return "MIXED";
    }

    public static string TrendFromBars(IReadOnlyList<decimal> closes)
    {
        if (closes.Count < 3)
            return "FLAT";
        var first = closes[0];
        var last = closes[^1];
        if (first == 0)
            return "FLAT";
        var change = (last - first) / Math.Abs(first);
        if (change > 0.0008m)
            return "UP";
        if (change < -0.0008m)
            return "DOWN";
        return "FLAT";
    }
}

public static class PearsonCorrelation
{
    public static decimal? Compute(IReadOnlyList<decimal> x, IReadOnlyList<decimal> y)
    {
        var n = Math.Min(x.Count, y.Count);
        if (n < 3)
            return null;
        var xs = x.TakeLast(n).ToArray();
        var ys = y.TakeLast(n).ToArray();
        var mx = xs.Average();
        var my = ys.Average();
        decimal num = 0, dx = 0, dy = 0;
        for (var i = 0; i < n; i++)
        {
            var a = xs[i] - mx;
            var b = ys[i] - my;
            num += a * b;
            dx += a * a;
            dy += b * b;
        }
        if (dx == 0 || dy == 0)
            return 0m;
        return num / (decimal)Math.Sqrt((double)(dx * dy));
    }
}

public static class LookAheadGuard
{
    public static void EnsureNoFuture(DateTime asOf, IEnumerable<DateTime> timestamps)
    {
        foreach (var ts in timestamps)
        {
            if (ts > asOf)
                throw new InvalidOperationException($"Look-ahead bias: feature timestamp {ts:o} is after as-of {asOf:o}.");
        }
    }
}

public static class DataQuality
{
    public static IReadOnlyList<string> ValidateBar(
        string symbol,
        DateTime timestamp,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        long volume,
        long? buyVolume,
        long? sellVolume)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(symbol))
            errors.Add("symbol_empty");
        if (timestamp == default)
            errors.Add("timestamp_invalid");
        if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
            errors.Add("price_invalid");
        if (high < low)
            errors.Add("high_lt_low");
        if (volume < 0)
            errors.Add("volume_negative");
        if (buyVolume < 0 || sellVolume < 0)
            errors.Add("side_volume_negative");
        if (buyVolume is not null && sellVolume is not null && buyVolume + sellVolume > volume && volume > 0)
            errors.Add("side_volume_exceeds_total");
        return errors;
    }
}
