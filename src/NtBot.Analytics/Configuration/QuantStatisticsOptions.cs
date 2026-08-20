namespace NtBot.Analytics.Configuration;

public sealed class QuantStatisticsOptions
{
    public const string SectionName = "QuantStatistics";

    public bool Enabled { get; set; } = true;
    public int MinimumSampleSize { get; set; } = 30;
    public int LowSampleSize { get; set; } = 100;
    public int MediumSampleSize { get; set; } = 500;
    public decimal ConfidenceLevel { get; set; } = 0.95m;
    public int ZScoreWindow { get; set; } = 20;
    public int LargeTradePercentile { get; set; } = 95;
    public int LiveBarSeconds { get; set; } = 15;
    public int FeatureRefreshSeconds { get; set; } = 60;
    public int OutcomeRefreshSeconds { get; set; } = 30;
    public int StatisticsRefreshSeconds { get; set; } = 300;
    public int FeatureLookback { get; set; } = 80;
    public int CacheTtlSeconds { get; set; } = 30;
    public int RetentionMonthsRaw { get; set; } = 12;
    public bool EnableRetentionJob { get; set; }

    public string SessionTimezone { get; set; } = "America/Sao_Paulo";
    public string SessionStart { get; set; } = "09:00";
    public string SessionEnd { get; set; } = "18:25";
    public string AuctionStart { get; set; } = "08:45";
    public string AuctionEnd { get; set; } = "09:00";

    public string PrimaryFeatureTimeframe { get; set; } = "5m";
    public IReadOnlyList<string> FeatureTimeframes { get; set; } = ["15s", "1m", "5m", "15m", "60m", "1d"];
    public IReadOnlyList<int> OpeningRangeWindowsMinutes { get; set; } = [1, 3, 5, 10, 15, 30];
    public IReadOnlyList<string> Symbols { get; set; } = ["WIN", "WDO"];
    public IReadOnlyList<string> CorrelationSymbols { get; set; } = ["WIN", "WDO", "NQ", "MNQ", "ES", "XAUUSD"];

    public Dictionary<string, decimal> AuctionScoreWeights { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Delta"] = 0.20m,
        ["Volume"] = 0.15m,
        ["Book"] = 0.15m,
        ["Gap"] = 0.10m,
        ["VWAP"] = 0.10m,
        ["Trend"] = 0.10m,
        ["Volatility"] = 0.10m,
        ["OpeningRange"] = 0.10m
    };

    public Dictionary<string, decimal> QuantScoreWeights { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Trend"] = 0.10m,
        ["Volume"] = 0.10m,
        ["Delta"] = 0.15m,
        ["Aggression"] = 0.10m,
        ["Book"] = 0.10m,
        ["VWAP"] = 0.10m,
        ["Volatility"] = 0.10m,
        ["Auction"] = 0.10m,
        ["MultiTimeframe"] = 0.10m,
        ["CrossMarket"] = 0.05m
    };
}
