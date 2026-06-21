using NtBot.MarketDrivers.Configuration;

namespace NtBot.MarketDrivers.Models;

public enum DriverDirection
{
    Bullish,
    Bearish,
    Neutral
}

public enum DriverImpactLevel
{
    VeryPositive,
    Positive,
    SlightlyPositive,
    Neutral,
    SlightlyNegative,
    Negative,
    VeryNegative
}

public sealed class MarketDriver
{
    public string Symbol { get; init; } = string.Empty;
    public MarketDriverCategory Category { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal? CurrentValue { get; init; }
    public decimal? PreviousValue { get; init; }
    public decimal Variation { get; init; }
    public DriverImpactLevel Impact { get; init; }
    public decimal Weight { get; init; }
    public DriverDirection Direction { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Recommendation { get; init; } = string.Empty;
    public DateTime LastUpdate { get; init; } = DateTime.UtcNow;
    public decimal Confidence { get; init; }
}

public sealed class DriverScore
{
    public int Score { get; init; }
    public string Label { get; init; } = "Neutro";
    public string Classification { get; init; } = "Neutro";
    public string Recommendation { get; init; } = "Neutro";
    public decimal Confidence { get; init; }
    public decimal? QuantProbability { get; init; }
    public IReadOnlyDictionary<string, decimal> ComponentScores { get; init; } =
        new Dictionary<string, decimal>();
}

public sealed class MarketDriverHeatCell
{
    public string Group { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Score { get; init; }
    public DriverImpactLevel Impact { get; init; }
    public string Tooltip { get; init; } = string.Empty;
    public decimal Variation { get; init; }
}

public sealed class MarketDriversAISummary
{
    public IReadOnlyList<string> PositiveFactors { get; init; } = [];
    public IReadOnlyList<string> NegativeFactors { get; init; } = [];
    public IReadOnlyList<string> RecentChanges { get; init; } = [];
    public IReadOnlyList<string> RelevantEvents { get; init; } = [];
    public string ExpectedImpact { get; init; } = string.Empty;
}

public sealed class MarketDriversSnapshot
{
    public string Asset { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public IReadOnlyList<MarketDriver> Drivers { get; init; } = [];
    public DriverScore Score { get; init; } = new();
    public string Explanation { get; init; } = string.Empty;
    public IReadOnlyList<MarketDriverHeatCell> HeatMap { get; init; } = [];
    public MarketDriversAISummary? AiSummary { get; init; }
}

public sealed class MarketDriversDashboardItem
{
    public string Asset { get; init; } = string.Empty;
    public DriverScore Score { get; init; } = new();
    public IReadOnlyList<MarketDriver> TopDrivers { get; init; } = [];
    public string ExplanationPreview { get; init; } = string.Empty;
}

public sealed class MarketDriverContext
{
    public required string Asset { get; init; }
    public NtBot.MarketIntelligence.Models.MarketOverview Overview { get; init; } = new();
    public NtBot.MarketIntelligence.Models.CorrelationResult Correlation { get; init; } = new();
    public NtBot.MarketIntelligence.Models.QuantScore QuantScore { get; init; } = new();
    public NtBot.Macro.DTO.MacroSnapshot Macro { get; init; } = new();
    public NtBot.Macro.DTO.MacroRecommendation? MacroRecommendation { get; init; }
    public NtBot.MarketIntelligence.Models.AssetImpactResult? AssetImpact { get; init; }
}
