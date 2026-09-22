namespace NtBot.Shared.MarketData;

/// <summary>
/// Aggregates 1-minute OHLCV bars into higher timeframes (3, 5, 15, 30, 60, 240, 1440).
/// </summary>
public static class CandleAggregator
{
    public static IReadOnlyList<OhlcvBar> FromM1(
        IReadOnlyList<OhlcvBar> m1Bars,
        int targetMinutes,
        int count)
    {
        if (m1Bars.Count == 0 || targetMinutes <= 0 || count <= 0)
            return [];

        if (targetMinutes == 1)
        {
            return m1Bars
                .OrderByDescending(b => b.OpenTime)
                .Take(count)
                .OrderBy(b => b.OpenTime)
                .Select(Clone)
                .ToList();
        }

        var interval = TimeSpan.FromMinutes(targetMinutes);
        var buckets = new SortedDictionary<DateTime, OhlcvBar>();

        foreach (var bar in m1Bars.OrderBy(b => b.OpenTime))
        {
            if (bar.OpenTime == default || bar.Close <= 0 && bar.Open <= 0)
                continue;

            var bucketStart = FloorToIntervalUtc(bar.OpenTime, interval);
            if (!buckets.TryGetValue(bucketStart, out var candle))
            {
                buckets[bucketStart] = new OhlcvBar
                {
                    OpenTime = bucketStart,
                    Open = bar.Open,
                    High = bar.High,
                    Low = bar.Low,
                    Close = bar.Close,
                    Volume = bar.Volume
                };
                continue;
            }

            candle.High = Math.Max(candle.High, bar.High);
            candle.Low = Math.Min(candle.Low, bar.Low);
            candle.Close = bar.Close;
            candle.Volume += bar.Volume;
        }

        return buckets.Values
            .OrderByDescending(c => c.OpenTime)
            .Take(count)
            .OrderBy(c => c.OpenTime)
            .ToList();
    }

    public static IReadOnlyList<OhlcvBar> FromM1(
        IReadOnlyList<OhlcvBar> m1Bars,
        string timeframe,
        int count) =>
        FromM1(m1Bars, ChartTimeframe.ToMinutes(timeframe), count);

    /// <summary>How many M1 bars are needed to produce <paramref name="count"/> bars of <paramref name="targetMinutes"/>.</summary>
    public static int RequiredM1Count(int targetMinutes, int count, int max = 1_500)
    {
        if (targetMinutes <= 0 || count <= 0)
            return 0;

        var needed = (long)targetMinutes * count + targetMinutes * 2L;
        return (int)Math.Min(max, Math.Max(needed, count));
    }

    public static DateTime FloorToIntervalUtc(DateTime timestamp, TimeSpan interval)
    {
        var utc = timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        };

        var ticks = utc.Ticks / interval.Ticks * interval.Ticks;
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    private static OhlcvBar Clone(OhlcvBar bar) => new()
    {
        OpenTime = bar.OpenTime,
        Open = bar.Open,
        High = bar.High,
        Low = bar.Low,
        Close = bar.Close,
        Volume = bar.Volume
    };
}
