using MarketData.API.Controllers;
using Marketdata.Database.Models;

namespace MarketData.API.Services;

public static class TradeToCandleAggregator
{
    public static List<CandleDto> Aggregate(
        IReadOnlyList<HistoricalTradeDto> trades,
        int timeframeMinutes,
        int count)
    {
        if (trades.Count == 0 || timeframeMinutes <= 0 || count <= 0)
            return [];

        var interval = TimeSpan.FromMinutes(timeframeMinutes);
        var ordered = trades
            .Where(t => !t.IsAuction && t.Price > 0)
            .OrderBy(t => t.Timestamp)
            .ToList();

        if (ordered.Count == 0)
            return [];

        var buckets = new SortedDictionary<DateTime, CandleDto>();

        foreach (var trade in ordered)
        {
            var bucketStart = FloorToInterval(trade.Timestamp, interval);
            if (!buckets.TryGetValue(bucketStart, out var candle))
            {
                candle = new CandleDto
                {
                    Time = bucketStart,
                    Open = (decimal)trade.Price,
                    High = (decimal)trade.Price,
                    Low = (decimal)trade.Price,
                    Close = (decimal)trade.Price,
                    Volume = trade.Quantity
                };
                buckets[bucketStart] = candle;
                continue;
            }

            var price = (decimal)trade.Price;
            candle.High = Math.Max(candle.High, price);
            candle.Low = Math.Min(candle.Low, price);
            candle.Close = price;
            candle.Volume += trade.Quantity;
        }

        return buckets.Values
            .OrderByDescending(c => c.Time)
            .Take(count)
            .OrderBy(c => c.Time)
            .ToList();
    }

    private static DateTime FloorToInterval(DateTime timestamp, TimeSpan interval)
    {
        var local = timestamp.Kind == DateTimeKind.Utc
            ? timestamp.ToLocalTime()
            : timestamp;

        var ticks = local.Ticks / interval.Ticks * interval.Ticks;
        return new DateTime(ticks, local.Kind);
    }
}
