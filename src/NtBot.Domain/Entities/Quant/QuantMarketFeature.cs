namespace NtBot.Domain.Entities.Quant;

/// <summary>
/// Aggregated market features at a single timestamp/timeframe.
/// Order-flow fields are null when the source (OHLCV-only) cannot provide them.
/// </summary>
public class QuantMarketFeature
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;

    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }

    public long Volume { get; set; }
    public int TradeCount { get; set; }

    public long? BuyVolume { get; set; }
    public long? SellVolume { get; set; }
    public long? AggressiveBuyVolume { get; set; }
    public long? AggressiveSellVolume { get; set; }

    public decimal? Delta { get; set; }
    public decimal? CumulativeDelta { get; set; }
    public decimal? DeltaRatio { get; set; }

    public long? BidVolume { get; set; }
    public long? AskVolume { get; set; }
    public decimal? BookImbalance { get; set; }
    public decimal? Spread { get; set; }

    public decimal? Vwap { get; set; }
    public decimal? DistanceVwap { get; set; }
    public decimal? DistanceVwapAtr { get; set; }

    public decimal? Atr { get; set; }
    public decimal? Range { get; set; }
    public decimal? RangeAtrRatio { get; set; }
    public decimal? Volatility { get; set; }

    public decimal? AverageTradeSize { get; set; }
    public int? LargeTradeCount { get; set; }
    public long? LargeTradeVolume { get; set; }
    public decimal? TradeSizePercentile { get; set; }
    public string? LargeTradeClass { get; set; }

    public decimal? VolumeZscore { get; set; }
    public decimal? DeltaZscore { get; set; }
    public decimal? VolumePercentile { get; set; }
    public decimal? DeltaPercentile { get; set; }

    public string? MarketRegime { get; set; }
    public string? TrendDirection { get; set; }
    public decimal? TrendStrength { get; set; }
    public string? MultiTimeframeAlignment { get; set; }
    public string? Trend1m { get; set; }
    public string? Trend5m { get; set; }
    public string? Trend15m { get; set; }
    public string? Trend30m { get; set; }
    public string? Trend60m { get; set; }

    public string? Absorption { get; set; }
    public decimal? AbsorptionStrength { get; set; }

    public int? QuantScore { get; set; }
    public int? TrendScore { get; set; }
    public int? VolumeScore { get; set; }
    public int? DeltaScore { get; set; }
    public int? AggressionScore { get; set; }
    public int? BookScore { get; set; }
    public int? VwapScore { get; set; }
    public int? VolatilityScore { get; set; }
    public int? AuctionScore { get; set; }
    public int? MtfScore { get; set; }
    public int? CrossMarketScore { get; set; }

    public string Session { get; set; } = string.Empty;
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
