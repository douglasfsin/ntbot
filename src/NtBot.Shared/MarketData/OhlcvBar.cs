namespace NtBot.Shared.MarketData;

/// <summary>Lightweight OHLCV bar used for Redis M1 storage and timeframe aggregation.</summary>
public sealed class OhlcvBar
{
    public DateTime OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}
