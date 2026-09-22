using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.MarketData;

/// <summary>
/// Tick padronizado do Market Data Gateway — única forma interna de transporte entre providers e ingest.
/// </summary>
public sealed record MarketTick
{
    public BrokerSource Provider { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
    public decimal? LastPrice { get; init; }
    public decimal? Bid { get; init; }
    public decimal? Ask { get; init; }
    public long? Volume { get; init; }
    public decimal? Open { get; init; }
    public decimal? High { get; init; }
    public decimal? Low { get; init; }
    public decimal? Close { get; init; }
    public int? Trades { get; init; }
    public string Source { get; init; } = string.Empty;

    public string CacheKey => $"{Provider}:{Symbol}";

    public static MarketTick FromNormalized(NormalizedMarketTick tick) => new()
    {
        Provider = tick.Source,
        Symbol = tick.Symbol,
        TimestampUtc = tick.TimestampUtc,
        LastPrice = tick.Last,
        Bid = tick.Bid,
        Ask = tick.Ask,
        Volume = tick.Volume,
        Source = tick.Source.ToString()
    };

    public NormalizedMarketTick ToNormalized() => new()
    {
        Source = Provider,
        Symbol = Symbol,
        Last = LastPrice,
        Bid = Bid,
        Ask = Ask,
        Volume = Volume,
        TimestampUtc = TimestampUtc,
        TenantId = null
    };
}
