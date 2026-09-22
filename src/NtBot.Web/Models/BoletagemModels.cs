namespace NtBot.Web.Models;

public class BoletaSessionModel
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = "";
    public DateTime SessionDate { get; set; }
    public string Strategy { get; set; } = "Wyckoff";
    public decimal DailyProfitTarget { get; set; }
    public decimal MaxDrawdownPercent { get; set; } = 2m;
    public decimal TrailingStopPercent { get; set; }
    public decimal LotSize { get; set; } = 0.01m;
    public decimal ReferenceBalance { get; set; }
    public decimal RealizedPnl { get; set; }
    public decimal FloatingPnl { get; set; }
    public decimal SessionPnl { get; set; }
    public decimal SessionPnlPercent { get; set; }
    public decimal CurrentDrawdownPercent { get; set; }
    public string RiskIndication { get; set; } = "Moderado";
    public string ScenarioBias { get; set; } = "";
    public string ScenarioRecommendation { get; set; } = "";
    public int ScenarioConfluenceScore { get; set; }
    public bool AutomationEnabled { get; set; }
    public bool TradingEnabled { get; set; }
    public bool AllowNeutralEntries { get; set; } = true;
    public bool MetaReached { get; set; }
    public bool DrawdownBreached { get; set; }
    public bool PositionsClosedByRule { get; set; }
    public string Status { get; set; } = "";
    public string? LastMessage { get; set; }
    public decimal? ResultPercentOfBalance { get; set; }
    public bool Mt5Ready { get; set; }
    public string? Mt5Status { get; set; }
    public List<BoletaOrderModel> Orders { get; set; } = [];
    public List<BoletaStrategyPlanModel> PlannedTargets { get; set; } = [];
}

public class BoletaOrderModel
{
    public Guid Id { get; set; }
    public string Direction { get; set; } = "";
    public decimal Volume { get; set; }
    public decimal? EntryPrice { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }
    public decimal? PeakFavorablePrice { get; set; }
    public decimal? TrailingStopPrice { get; set; }
    public int TargetLevel { get; set; }
    public string Strategy { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Mt5Ticket { get; set; }
    public decimal? RealizedPnl { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BoletaStrategyPlanModel
{
    public int Level { get; set; }
    public string Direction { get; set; } = "";
    public decimal Volume { get; set; }
    public decimal? Entry { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }
    public string Rationale { get; set; } = "";
}

public class UpsertBoletaModel
{
    public string Symbol { get; set; } = "";
    public string Strategy { get; set; } = "Wyckoff";
    public decimal DailyProfitTarget { get; set; }
    public decimal MaxDrawdownPercent { get; set; } = 2m;
    public decimal TrailingStopPercent { get; set; }
    public decimal LotSize { get; set; } = 0.01m;
    public bool AutomationEnabled { get; set; }
    public bool TradingEnabled { get; set; }
    public bool AllowNeutralEntries { get; set; } = true;
    public string? Direction { get; set; }
    public decimal? ReferenceBalance { get; set; }
    public int? ConfluenceScore { get; set; }
    public string? Recommendation { get; set; }
    public string? Bias { get; set; }
    public string? RiskLevel { get; set; }
    public string? MarketDriversRecommendation { get; set; }
    public decimal? SuggestedEntry { get; set; }
    public decimal? SuggestedStopLoss { get; set; }
    public decimal? SuggestedTakeProfit { get; set; }
    public decimal? LastPrice { get; set; }
    public int? WyckoffScore { get; set; }
    public List<BoletaZoneModel>? OperationalZones { get; set; }
}

public class BoletaZoneModel
{
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal PriceLow { get; set; }
    public decimal PriceHigh { get; set; }
    public int ConfluenceScore { get; set; }
}

public class ExecuteBoletaModel
{
    public Guid? SessionId { get; set; }
    public string Symbol { get; set; } = "";
    public string? Direction { get; set; }
    public decimal? Volume { get; set; }
    public decimal? EntryPrice { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }
    public bool DryRun { get; set; }
    public bool? AllowNeutralEntries { get; set; }
    public int? TargetLevel { get; set; }
    public int? ConfluenceScore { get; set; }
    public string? Recommendation { get; set; }
    public string? Bias { get; set; }
    public string? RiskLevel { get; set; }
    public string? MarketDriversRecommendation { get; set; }
    public decimal? SuggestedEntry { get; set; }
    public decimal? SuggestedStopLoss { get; set; }
    public decimal? SuggestedTakeProfit { get; set; }
    public decimal? LastPrice { get; set; }
    public int? WyckoffScore { get; set; }
    public List<BoletaZoneModel>? OperationalZones { get; set; }
}
