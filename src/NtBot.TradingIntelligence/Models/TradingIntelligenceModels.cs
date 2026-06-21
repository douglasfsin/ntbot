namespace NtBot.TradingIntelligence.Models;

public sealed class EngineScoreInput
{
    public string Asset { get; init; } = string.Empty;
    public int MacroScore { get; init; }
    public int QuantScore { get; init; }
    public int DriverScore { get; init; }
    public int WyckoffScore { get; init; }
    public int SmcScore { get; init; }
    public int VolumeScore { get; init; }
    public int MomentumScore { get; init; }
    public int CorrelationScore { get; init; }
    public int LiquidityScore { get; init; }
    public int CalendarScore { get; init; }
}

public sealed class EngineScoreComponent
{
    public string Engine { get; init; } = string.Empty;
    public int Score { get; init; }
    public decimal Weight { get; init; }
    public decimal WeightedContribution { get; init; }
    public string Impact { get; init; } = "Neutro";
    public string Tooltip { get; init; } = string.Empty;
}

public sealed class ConfluenceScoreResult
{
    public int Score { get; init; }
    public string Classification { get; init; } = "Neutral";
    public string Recommendation { get; init; } = "Neutro";
    public decimal Confidence { get; init; }
    public IReadOnlyList<EngineScoreComponent> Components { get; init; } = [];
    public IReadOnlyList<string> PositiveFactors { get; init; } = [];
    public IReadOnlyList<string> NegativeFactors { get; init; } = [];
    public string Explanation { get; init; } = string.Empty;
}

public enum OperationalZoneType
{
    StrongBuy,
    StrongSell,
    ModerateBuy,
    ModerateSell,
    Neutral
}

public sealed class OperationalZone
{
    public OperationalZoneType Type { get; init; }
    public string Label { get; init; } = string.Empty;
    public decimal PriceLow { get; init; }
    public decimal PriceHigh { get; init; }
    public int ConfluenceScore { get; init; }
    public IReadOnlyList<string> Sources { get; init; } = [];
    public string Timeframe { get; init; } = "";
    public string Description { get; init; } = "";
}

public sealed class TimeframeAnalysis
{
    public string Timeframe { get; init; } = "";
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Mid { get; init; }
    public int WyckoffScore { get; init; }
    public int SmcScore { get; init; }
    public int VolumeScore { get; init; }
}

public sealed class TimeframeIntersection
{
    public string Pair { get; init; } = "";
    public decimal PriceLow { get; init; }
    public decimal PriceHigh { get; init; }
    public int ConfluenceScore { get; init; }
    public bool HighConfluence { get; init; }
}

public sealed class TradingIntelligenceHeatCell
{
    public string Engine { get; init; } = string.Empty;
    public int Score { get; init; }
    public decimal Weight { get; init; }
    public string Impact { get; init; } = "Neutro";
    public string Tooltip { get; init; } = string.Empty;
}

public sealed class AiAgentInsight
{
    public string AgentId { get; init; } = string.Empty;
    public string Asset { get; init; } = string.Empty;
    public string Specialization { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<string> Highlights { get; init; } = [];
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

public sealed class MasterAgentSummary
{
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<string> Confluences { get; init; } = [];
    public IReadOnlyList<string> Strengths { get; init; } = [];
    public IReadOnlyList<string> Weaknesses { get; init; } = [];
    public IReadOnlyList<string> Events { get; init; } = [];
    public IReadOnlyList<string> Drivers { get; init; } = [];
    public string Probability { get; init; } = "";
    public string Risk { get; init; } = "";
}

public sealed class TradingIntelligenceAiResult
{
    public MasterAgentSummary? Master { get; init; }
    public IReadOnlyList<AiAgentInsight> AgentInsights { get; init; } = [];
}

public sealed class SmcChartZoneDto
{
    public string Type { get; init; } = string.Empty;
    public decimal PriceLow { get; init; }
    public decimal PriceHigh { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class TradingIntelligenceSnapshot
{
    public string Asset { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public ConfluenceScoreResult Confluence { get; init; } = new();
    public IReadOnlyList<OperationalZone> OperationalZones { get; init; } = [];
    public IReadOnlyList<TimeframeAnalysis> TimeframeAnalyses { get; init; } = [];
    public IReadOnlyList<TimeframeIntersection> Intersections { get; init; } = [];
    public IReadOnlyList<TradingIntelligenceHeatCell> HeatMap { get; init; } = [];
    public MasterAgentSummary? AiSummary { get; init; }
    public IReadOnlyList<AiAgentInsight> AgentInsights { get; init; } = [];
}

public sealed class TradingIntelligenceDashboardItem
{
    public string Asset { get; init; } = string.Empty;
    public int ConfluenceScore { get; init; }
    public string Classification { get; init; } = string.Empty;
    public string Recommendation { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public int HighConfluenceZones { get; init; }
    public int AgentInsightCount { get; init; }
    public string TopIntersection { get; init; } = string.Empty;
    public string ExplanationPreview { get; init; } = string.Empty;
}

public sealed class TradingIntelligenceStatus
{
    public bool RedisEnabled { get; init; }
    public bool N8nConfigured { get; init; }
    public int N8nAssetWebhooks { get; init; }
    public IReadOnlyList<string> DashboardAssets { get; init; } = [];
}

public sealed class DriverCompositionDto
{
    public Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public string TargetAsset { get; init; } = "";
    public string DriverAsset { get; init; } = "";
    public decimal Weight { get; init; }
    public bool Enabled { get; init; }
    public int DisplayOrder { get; init; }
    public string? Description { get; init; }
    public string Category { get; init; } = "Correlacao";
    public bool Inverse { get; init; }
}

public sealed class DriverCompositionUpsertRequest
{
    public string TargetAsset { get; set; } = "";
    public string DriverAsset { get; set; } = "";
    public decimal Weight { get; set; }
    public bool Enabled { get; set; } = true;
    public int DisplayOrder { get; set; }
    public string? Description { get; set; }
    public string Category { get; set; } = "Correlacao";
    public bool Inverse { get; set; }
}

public sealed class DuplicateCompositionRequest
{
    public string SourceAsset { get; set; } = "";
    public string TargetAsset { get; set; } = "";
}

public sealed class ReorderCompositionRequest
{
    public string TargetAsset { get; set; } = "";
    public List<Guid> OrderedIds { get; set; } = [];
}
