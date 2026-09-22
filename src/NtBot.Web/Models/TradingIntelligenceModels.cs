namespace NtBot.Web.Models;

public class ConfluenceScoreModel
{
    public int Score { get; set; }
    public string Classification { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public decimal Confidence { get; set; }
    /// <summary>Muito Baixa → Institucional (campo aditivo; UI existente ignora se não usar).</summary>
    public string ConfidenceLevel { get; set; } = "";
    public string Bias { get; set; } = "";
    public string RiskLevel { get; set; } = "";
    public string DataQuality { get; set; } = "";
    public int KnownEngineCount { get; set; }
    public int TotalEngineCount { get; set; }
    public List<EngineScoreComponentModel> Components { get; set; } = [];
    public List<string> PositiveFactors { get; set; } = [];
    public List<string> NegativeFactors { get; set; } = [];
    public List<string> BlockingFactors { get; set; } = [];
    public TradeRiskSuggestionModel? RiskSuggestion { get; set; }
    public string Explanation { get; set; } = "";
}

public class TradeRiskSuggestionModel
{
    public decimal? Entry { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }
    public decimal? RiskReward { get; set; }
    public string StopBasis { get; set; } = "";
    public string TakeProfitBasis { get; set; } = "";
    public string Summary { get; set; } = "";
}

public class EngineScoreComponentModel
{
    public string Engine { get; set; } = "";
    public string Status { get; set; } = "Known";
    public int Score { get; set; }
    public decimal Confidence { get; set; }
    public decimal Weight { get; set; }
    public decimal EffectiveWeight { get; set; }
    public decimal WeightedContribution { get; set; }
    public string Impact { get; set; } = "";
    public string Tooltip { get; set; } = "";
}

public class OperationalZoneModel
{
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal PriceLow { get; set; }
    public decimal PriceHigh { get; set; }
    public int ConfluenceScore { get; set; }
    public List<string> Sources { get; set; } = [];
    public string Timeframe { get; set; } = "";
    public string Description { get; set; } = "";
}

public class TimeframeAnalysisModel
{
    public string Timeframe { get; set; } = "";
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Mid { get; set; }
    public int WyckoffScore { get; set; }
    public int SmcScore { get; set; }
    public int VolumeScore { get; set; }
}

public class TimeframeIntersectionModel
{
    public string Pair { get; set; } = "";
    public decimal PriceLow { get; set; }
    public decimal PriceHigh { get; set; }
    public int ConfluenceScore { get; set; }
    public bool HighConfluence { get; set; }
}

public class TradingIntelligenceHeatCellModel
{
    public string Engine { get; set; } = "";
    public int Score { get; set; }
    public decimal Weight { get; set; }
    public string Impact { get; set; } = "";
    public string Tooltip { get; set; } = "";
}

public class MasterAgentSummaryModel
{
    public string Summary { get; set; } = "";
    public List<string> Confluences { get; set; } = [];
    public List<string> Strengths { get; set; } = [];
    public List<string> Weaknesses { get; set; } = [];
    public List<string> Events { get; set; } = [];
    public List<string> Drivers { get; set; } = [];
    public string Probability { get; set; } = "";
    public string Risk { get; set; } = "";
}

public class AiAgentInsightModel
{
    public string AgentId { get; set; } = "";
    public string Asset { get; set; } = "";
    public string Specialization { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> Highlights { get; set; } = [];
    public DateTime GeneratedAt { get; set; }
}

public class SmcChartZoneModel
{
    public string Type { get; set; } = "";
    public decimal PriceLow { get; set; }
    public decimal PriceHigh { get; set; }
    public string Label { get; set; } = "";
}

public class SmcOverlayBundleModel
{
    public string Timeframe { get; set; } = "";
    public int Score { get; set; }
    public string Bias { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<SmcChartZoneModel> Overlays { get; set; } = [];
}

public class TradingIntelligenceSnapshotModel
{
    public string Asset { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public ConfluenceScoreModel Confluence { get; set; } = new();
    public List<OperationalZoneModel> OperationalZones { get; set; } = [];
    public List<TimeframeAnalysisModel> TimeframeAnalyses { get; set; } = [];
    public List<TimeframeIntersectionModel> Intersections { get; set; } = [];
    public List<TradingIntelligenceHeatCellModel> HeatMap { get; set; } = [];
    public List<TradingTimelineEventModel> Timeline { get; set; } = [];
    public List<SmcOverlayBundleModel> SmcOverlays { get; set; } = [];
    public MasterAgentSummaryModel? AiSummary { get; set; }
    public List<AiAgentInsightModel> AgentInsights { get; set; } = [];
}

public class TradingTimelineEventModel
{
    public DateTime Timestamp { get; set; }
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Severity { get; set; } = "Info";
}

public class DriverCompositionModel
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string TargetAsset { get; set; } = "";
    public string DriverAsset { get; set; } = "";
    public decimal Weight { get; set; }
    public bool Enabled { get; set; }
    public int DisplayOrder { get; set; }
    public string? Description { get; set; }
    public string Category { get; set; } = "";
    public bool Inverse { get; set; }
}

public class DriverCompositionUpsertModel
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

public class ChartCandleModel
{
    public long Time { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
}

public sealed class ChartCandlesResponse
{
    public List<ChartCandleModel> Candles { get; set; } = [];
    public string Source { get; set; } = "";
}

public sealed class ChartCandlesPushModel
{
    public string Symbol { get; set; } = "";
    public string Timeframe { get; set; } = "";
    public string Source { get; set; } = "";
    public bool IsPartial { get; set; }
    public List<ChartCandleModel> Candles { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}

public sealed class ChartPriceModel
{
    public string Symbol { get; set; } = "";
    public decimal Price { get; set; }
    public decimal? Bid { get; set; }
    public decimal? Ask { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Source { get; set; } = "";
}

public sealed class SmcOverlaysResponse
{
    public List<SmcChartZoneModel> Overlays { get; set; } = [];
    public int Score { get; set; }
    public string Summary { get; set; } = "";
}

public class TradingIntelligenceDashboardItemModel
{
    public string Asset { get; set; } = "";
    public int ConfluenceScore { get; set; }
    public string Classification { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public decimal Confidence { get; set; }
    public int HighConfluenceZones { get; set; }
    public int AgentInsightCount { get; set; }
    public string TopIntersection { get; set; } = "";
    public string ExplanationPreview { get; set; } = "";
}

public class TradingIntelligenceStatusModel
{
    public bool RedisEnabled { get; set; }
    public bool N8nConfigured { get; set; }
    public int N8nAssetWebhooks { get; set; }
    public List<string> DashboardAssets { get; set; } = [];
    public string AiMode { get; set; } = "stub";
}
