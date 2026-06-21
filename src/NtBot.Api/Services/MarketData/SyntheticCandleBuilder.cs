using NtBot.Domain.Entities;
using NtBot.Shared.MarketData;

namespace NtBot.Api.Services.MarketData;

public static class SyntheticCandleBuilder
{
    public static List<Candle> Build(
        string symbol,
        string timeframe,
        decimal priceLow,
        decimal priceHigh,
        int count = 60)
    {
        if (priceLow <= 0 && priceHigh <= 0)
            return [];

        var low = Math.Min(priceLow, priceHigh);
        var high = Math.Max(priceLow, priceHigh);
        if (high == low)
            high = low + Math.Max(low * 0.002m, 1m);

        var mid = (low + high) / 2m;
        var range = high - low;
        var now = DateTime.UtcNow;
        var stepMinutes = ChartTimeframe.Normalize(timeframe) switch
        {
            "M1" => 1,
            "M5" => 5,
            "M15" => 15,
            "M30" => 30,
            "H1" => 60,
            "H4" => 240,
            "D1" => 1440,
            _ => 5
        };

        var candles = new List<Candle>(count);
        var price = mid;

        for (var i = count - 1; i >= 0; i--)
        {
            var openTime = now.AddMinutes(-i * stepMinutes);
            var wave = (decimal)Math.Sin(i * 0.35) * range * 0.35m;
            var drift = (decimal)Math.Cos(i * 0.12) * range * 0.15m;
            var open = Math.Clamp(price, low, high);
            var close = Math.Clamp(open + wave + drift * 0.2m, low, high);
            var candleHigh = Math.Clamp(Math.Max(open, close) + range * 0.08m, low, high);
            var candleLow = Math.Clamp(Math.Min(open, close) - range * 0.08m, low, high);

            candles.Add(new Candle
            {
                Id = Guid.NewGuid(),
                Symbol = symbol,
                Timeframe = ChartTimeframe.Normalize(timeframe),
                OpenTime = openTime,
                CloseTime = openTime.AddMinutes(stepMinutes),
                Open = open,
                High = candleHigh,
                Low = candleLow,
                Close = close,
                Volume = 1000 + i * 10,
                CreatedAt = DateTime.UtcNow
            });

            price = close;
        }

        return candles;
    }
}
