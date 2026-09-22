using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.MarketData;

public static class MarketTickNormalizer
{
    public static MarketTick Normalize(MarketTick raw)
    {
        var symbol = raw.Symbol.Trim().ToUpperInvariant();
        var last = raw.LastPrice ?? InferMid(raw.Bid, raw.Ask);

        return raw with
        {
            Symbol = symbol,
            LastPrice = last,
            TimestampUtc = raw.TimestampUtc == default ? DateTime.UtcNow : raw.TimestampUtc,
            Source = string.IsNullOrWhiteSpace(raw.Source) ? raw.Provider.ToString() : raw.Source
        };
    }

    public static MarketTick FromProvider(
        BrokerSource provider,
        string symbol,
        decimal? last,
        decimal? bid,
        decimal? ask,
        long? volume = null,
        DateTime? timestampUtc = null,
        decimal? open = null,
        decimal? high = null,
        decimal? low = null,
        decimal? close = null) =>
        Normalize(new MarketTick
        {
            Provider = provider,
            Symbol = symbol,
            LastPrice = last ?? InferMid(bid, ask),
            Bid = bid,
            Ask = ask,
            Volume = volume,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            TimestampUtc = timestampUtc ?? DateTime.UtcNow,
            Source = provider.ToString()
        });

    private static decimal? InferMid(decimal? bid, decimal? ask)
    {
        if (bid is > 0 && ask is > 0)
            return (bid.Value + ask.Value) / 2m;
        return null;
    }
}
