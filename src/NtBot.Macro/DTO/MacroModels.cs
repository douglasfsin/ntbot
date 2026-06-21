namespace NtBot.Macro.DTO;

public enum MacroLevel
{
    Unknown,
    VeryLow,
    Low,
    Neutral,
    High,
    VeryHigh
}

public enum MacroRegimeLabel
{
    Unknown,
    Bearish,
    Neutral,
    Bullish
}

public enum MacroRecommendationAction
{
    StrongSell,
    ModerateSell,
    Neutral,
    ModerateBuy,
    StrongBuy
}

public enum MacroRiskLevel
{
    Low,
    Medium,
    High,
    Extreme
}

public enum MacroProviderHealth
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy,
    Disabled
}

public sealed class MacroIndicatorValue
{
    public string SeriesId { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public decimal? Value { get; init; }
    public DateTime? ObservedAt { get; init; }
    public string Unit { get; init; } = string.Empty;
}

public sealed class MacroCalendarEventDto
{
    public Guid Id { get; init; }
    public string EventName { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string Impact { get; init; } = string.Empty;
    public DateTime EventTime { get; init; }
    public string? Actual { get; init; }
    public string? Forecast { get; init; }
    public string? Previous { get; init; }
}

public sealed class MacroSnapshot
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public MacroLevel Liquidity { get; init; }
    public MacroLevel DollarStrength { get; init; }
    public MacroLevel Volatility { get; init; }
    public MacroLevel InterestRate { get; init; }
    public MacroLevel Inflation { get; init; }
    public MacroRegimeLabel RiskSentiment { get; init; }
    public MacroRegimeLabel MacroScore { get; init; }
    public decimal Confidence { get; init; }
    public IReadOnlyList<MacroRecommendation> Recommendations { get; init; } = [];
    public IReadOnlyList<MacroCalendarEventDto> UpcomingEvents { get; init; } = [];
    public string Provider { get; init; } = "aggregate";
    public IReadOnlyList<MacroIndicatorValue> Indicators { get; init; } = [];
}

public sealed class MacroRecommendation
{
    public string Ticker { get; init; } = string.Empty;
    public MacroRecommendationAction Action { get; init; }
    public decimal Confidence { get; init; }
    public MacroRiskLevel RiskLevel { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class MacroProviderStatusDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public int Priority { get; init; }
    public MacroProviderHealth HealthStatus { get; init; }
    public DateTime? LastUpdate { get; init; }
    public int RefreshIntervalMinutes { get; init; }
    public IReadOnlyList<string> Capabilities { get; init; } = [];
    public string? ApiUrl { get; init; }
}

public sealed class MacroProviderConfigureRequest
{
    public string? ApiUrl { get; init; }
    public string? ApiKey { get; init; }
    public int? RefreshIntervalMinutes { get; init; }
    public int? Priority { get; init; }
}

public sealed class MacroProviderPayload
{
    public string ProviderName { get; init; } = string.Empty;
    public DateTime FetchedAt { get; init; } = DateTime.UtcNow;
    public IReadOnlyList<MacroIndicatorValue> Indicators { get; init; } = [];
    public IReadOnlyList<MacroCalendarEventDto> Events { get; init; } = [];
}
