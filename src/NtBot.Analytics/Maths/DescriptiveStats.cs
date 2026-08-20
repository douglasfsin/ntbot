namespace NtBot.Analytics.Maths;

public static class DescriptiveStats
{
    public static decimal? Mean(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
            return null;
        return values.Average();
    }

    public static decimal? Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
            return null;
        var sorted = values.OrderBy(v => v).ToArray();
        var mid = sorted.Length / 2;
        if (sorted.Length % 2 == 1)
            return sorted[mid];
        return (sorted[mid - 1] + sorted[mid]) / 2m;
    }

    public static decimal? StdDev(IReadOnlyList<decimal> values, bool sample = true)
    {
        if (values.Count == 0)
            return null;
        if (sample && values.Count < 2)
            return 0m;
        var mean = values.Average();
        var sumSq = values.Sum(v =>
        {
            var d = v - mean;
            return d * d;
        });
        var denom = sample ? values.Count - 1 : values.Count;
        if (denom <= 0)
            return 0m;
        return (decimal)Math.Sqrt((double)(sumSq / denom));
    }

    public static decimal? Percentile(IReadOnlyList<decimal> values, decimal percentile)
    {
        if (values.Count == 0)
            return null;
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 1)
            return sorted[0];
        var rank = (double)percentile / 100d * (sorted.Length - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        if (lo == hi)
            return sorted[lo];
        var weight = (decimal)(rank - lo);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * weight;
    }

    public static decimal? ZScore(decimal value, IReadOnlyList<decimal> window)
    {
        var mean = Mean(window);
        var std = StdDev(window);
        if (mean is null || std is null || std == 0)
            return 0m;
        return (value - mean.Value) / std.Value;
    }

    public static decimal? PercentileRank(decimal value, IReadOnlyList<decimal> window)
    {
        if (window.Count == 0)
            return null;
        var below = window.Count(v => v < value);
        var equal = window.Count(v => v == value);
        return (below + 0.5m * equal) / window.Count * 100m;
    }
}
