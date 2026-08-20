namespace NtBot.Analytics.Maths;

public static class WilsonScore
{
    /// <summary>
    /// Wilson score interval for a binomial proportion.
    /// </summary>
    public static (decimal Low, decimal High) Interval(int successes, int samples, decimal confidenceLevel = 0.95m)
    {
        if (samples <= 0)
            return (0m, 0m);

        var z = ZFor(confidenceLevel);
        var n = (double)samples;
        var p = (double)successes / n;
        var z2 = z * z;
        var denom = 1d + z2 / n;
        var centre = p + z2 / (2d * n);
        var margin = z * Math.Sqrt((p * (1d - p) + z2 / (4d * n)) / n);
        var low = (centre - margin) / denom;
        var high = (centre + margin) / denom;
        return (
            Round(Math.Clamp(low, 0d, 1d)),
            Round(Math.Clamp(high, 0d, 1d)));
    }

    public static double ZFor(decimal confidenceLevel) => confidenceLevel switch
    {
        0.90m => 1.6448536269514722,
        0.99m => 2.5758293035489004,
        _ => 1.959963984540054
    };

    private static decimal Round(double value) => Math.Round((decimal)value, 6, MidpointRounding.AwayFromZero);
}
