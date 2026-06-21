namespace NtBot.Web.Models;

public sealed class MacroSnapshotModel
{
    public DateTime Timestamp { get; set; }
    public string Liquidity { get; set; } = "Neutral";
    public string DollarStrength { get; set; } = "Neutral";
    public string Volatility { get; set; } = "Neutral";
    public string InterestRate { get; set; } = "Neutral";
    public string Inflation { get; set; } = "Neutral";
    public string RiskSentiment { get; set; } = "Neutral";
    public string MacroScore { get; set; } = "Neutral";
    public decimal Confidence { get; set; }
    public List<MacroRecommendationModel> Recommendations { get; set; } = [];
    public List<MacroCalendarEventModel> UpcomingEvents { get; set; } = [];
    public string Provider { get; set; } = "aggregate";
    public List<MacroIndicatorModel> Indicators { get; set; } = [];
}

public sealed class MacroRecommendationModel
{
    public string Ticker { get; set; } = string.Empty;
    public string Action { get; set; } = "Neutral";
    public decimal Confidence { get; set; }
    public string RiskLevel { get; set; } = "Medium";
    public string Reason { get; set; } = string.Empty;
}

public sealed class MacroCalendarEventModel
{
    public Guid Id { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }
    public string? Actual { get; set; }
    public string? Forecast { get; set; }
    public string? Previous { get; set; }
}

public sealed class MacroIndicatorModel
{
    public string SeriesId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal? Value { get; set; }
    public DateTime? ObservedAt { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public sealed class MacroProviderStatusModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int Priority { get; set; }
    public string HealthStatus { get; set; } = "Unknown";
    public DateTime? LastUpdate { get; set; }
    public int RefreshIntervalMinutes { get; set; }
    public List<string> Capabilities { get; set; } = [];
    public string? ApiUrl { get; set; }
}

public sealed class MacroProviderConfigureModel
{
    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public int? RefreshIntervalMinutes { get; set; }
    public int? Priority { get; set; }
}
