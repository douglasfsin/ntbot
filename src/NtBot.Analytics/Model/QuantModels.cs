namespace NtBot.Analytics.Model;

public sealed record MarketBar(
    DateTime Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    int TradeCount = 0,
    long? BuyVolume = null,
    long? SellVolume = null,
    long? AggressiveBuyVolume = null,
    long? AggressiveSellVolume = null,
    decimal? Bid = null,
    decimal? Ask = null,
    long? BidVolume = null,
    long? AskVolume = null,
    string Symbol = "");

public sealed class FeatureSnapshot
{
    public required MarketBar Bar { get; init; }
    public decimal? Delta { get; init; }
    public decimal? CumulativeDelta { get; init; }
    public decimal? DeltaRatio { get; init; }
    public decimal? Vwap { get; init; }
    public decimal? DistanceVwap { get; init; }
    public decimal? DistanceVwapAtr { get; init; }
    public decimal? Atr { get; init; }
    public decimal Range { get; init; }
    public decimal? RangeAtrRatio { get; init; }
    public decimal? Volatility { get; init; }
    public decimal? VolumeZscore { get; init; }
    public decimal? DeltaZscore { get; init; }
    public decimal? VolumePercentile { get; init; }
    public decimal? DeltaPercentile { get; init; }
    public decimal? VolatilityPercentile { get; init; }
    public decimal? AverageTradeSize { get; init; }
    public decimal? TradeSizePercentile { get; init; }
    public string? LargeTradeClass { get; init; }
    public int LargeTradeCount { get; init; }
    public string? Absorption { get; init; }
    public decimal AbsorptionStrength { get; init; }
    public decimal? BookImbalance { get; init; }
    public decimal? Spread { get; init; }
    public string TrendDirection { get; init; } = "FLAT";
    public decimal TrendStrength { get; init; }
    public string MarketRegime { get; init; } = "RANGE";
    public Dictionary<string, string> TimeframeTrends { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string MultiTimeframeAlignment { get; init; } = "MIXED";
    public QuantScoreBreakdown Score { get; init; } = new();
}

public sealed class QuantScoreBreakdown
{
    public int Total { get; init; }
    public int Trend { get; init; }
    public int Volume { get; init; }
    public int Delta { get; init; }
    public int Aggression { get; init; }
    public int Book { get; init; }
    public int Vwap { get; init; }
    public int Volatility { get; init; }
    public int Auction { get; init; }
    public int MultiTimeframe { get; init; }
    public int CrossMarket { get; init; }
}

public sealed class HorizonPath
{
    public decimal? Price { get; init; }
    public decimal? High { get; init; }
    public decimal? Low { get; init; }
    public bool Available { get; init; }
}

public sealed class OutcomeSnapshot
{
    public required string Direction { get; init; }
    public required decimal Entry { get; init; }
    public decimal? StopDistance { get; init; }
    public Dictionary<string, decimal?> Returns { get; init; } = new();
    public Dictionary<string, decimal?> Mfe { get; init; } = new();
    public Dictionary<string, decimal?> Mae { get; init; } = new();
    public decimal? MaxPrice { get; init; }
    public decimal? MinPrice { get; init; }
    public bool? TargetHit { get; init; }
    public bool? StopHit { get; init; }
    public bool? Success5m { get; init; }
    public decimal? ReturnPoints { get; init; }
    public decimal? ReturnPercent { get; init; }
    public decimal? ReturnR { get; init; }
    public bool Complete { get; init; }
}

public sealed class StatisticalSummary
{
    public int SampleCount { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public string SampleClass { get; init; } = "INSUFFICIENT_SAMPLE";
    public decimal? SuccessProbability { get; init; }
    public decimal? ConfidenceLow { get; init; }
    public decimal? ConfidenceHigh { get; init; }
    public decimal ConfidenceLevel { get; init; } = 0.95m;
    public decimal? AverageReturn { get; init; }
    public decimal? MedianReturn { get; init; }
    public decimal? StdReturn { get; init; }
    public decimal? MinReturn { get; init; }
    public decimal? MaxReturn { get; init; }
    public decimal? P25 { get; init; }
    public decimal? P50 { get; init; }
    public decimal? P75 { get; init; }
    public decimal? P90 { get; init; }
    public decimal? P95 { get; init; }
    public decimal? AverageMfe { get; init; }
    public decimal? AverageMae { get; init; }
    public decimal? AverageWin { get; init; }
    public decimal? AverageLoss { get; init; }
    public decimal? ProfitFactor { get; init; }
    public decimal? Expectancy { get; init; }
    public decimal? ExpectancyR { get; init; }
    public decimal? SharpeLike { get; init; }
    public decimal? SortinoLike { get; init; }
    public decimal? MaxDrawdown { get; init; }
}
