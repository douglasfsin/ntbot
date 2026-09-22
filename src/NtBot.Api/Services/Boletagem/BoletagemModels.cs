namespace NtBot.Api.Services.Boletagem;

public sealed class BoletaSessionDto
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = "";
    public DateTime SessionDate { get; set; }
    public string Strategy { get; set; } = "";
    public decimal DailyProfitTarget { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    /// <summary>Trailing stop % sobre o pico favorável (0 = off).</summary>
    public decimal TrailingStopPercent { get; set; }
    public decimal LotSize { get; set; }
    public decimal ReferenceBalance { get; set; }
    public decimal RealizedPnl { get; set; }
    public decimal FloatingPnl { get; set; }
    public decimal SessionPnl { get; set; }
    public decimal SessionPnlPercent { get; set; }
    public decimal CurrentDrawdownPercent { get; set; }
    public string RiskIndication { get; set; } = "";
    public string ScenarioBias { get; set; } = "";
    public string ScenarioRecommendation { get; set; } = "";
    public int ScenarioConfluenceScore { get; set; }
    public bool AutomationEnabled { get; set; }
    /// <summary>Start = true (permite abrir); Stop = false (bloqueia novas aberturas).</summary>
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
    public List<BoletaOrderDto> Orders { get; set; } = [];
    public List<BoletaStrategyPlanDto> PlannedTargets { get; set; } = [];
}

public sealed class BoletaOrderDto
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
    public DateTime? ExecutedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public sealed class BoletaStrategyPlanDto
{
    public int Level { get; set; }
    public string Direction { get; set; } = "";
    public decimal Volume { get; set; }
    public decimal? Entry { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }
    public string Rationale { get; set; } = "";
}

public sealed class UpsertBoletaRequest
{
    public string Symbol { get; set; } = "";
    public string Strategy { get; set; } = "Wyckoff";
    public decimal DailyProfitTarget { get; set; }
    public decimal MaxDrawdownPercent { get; set; } = 2m;
    /// <summary>Trailing stop %; 0 = desligado.</summary>
    public decimal TrailingStopPercent { get; set; }
    public decimal LotSize { get; set; } = 0.01m;
    public bool AutomationEnabled { get; set; }
    /// <summary>Start/Stop — default false (Stop).</summary>
    public bool TradingEnabled { get; set; }
    public bool AllowNeutralEntries { get; set; } = true;
    /// <summary>Buy | Sell — força a direção; null = automático pela estratégia.</summary>
    public string? Direction { get; set; }
    public decimal? ReferenceBalance { get; set; }
    /// <summary>Confluence do dashboard (opcional — UI envia).</summary>
    public int? ConfluenceScore { get; set; }
    public string? Recommendation { get; set; }
    public string? Bias { get; set; }
    public string? RiskLevel { get; set; }
    /// <summary>Recomendação do painel Market Drivers (COMPRA MODERADA, VENDA…).</summary>
    public string? MarketDriversRecommendation { get; set; }
    public decimal? SuggestedEntry { get; set; }
    public decimal? SuggestedStopLoss { get; set; }
    public decimal? SuggestedTakeProfit { get; set; }
    public decimal? LastPrice { get; set; }
    public int? WyckoffScore { get; set; }
    /// <summary>Zonas operacionais TI (demanda/oferta) para alinhar entrada à tendência.</summary>
    public List<BoletaZoneDto>? OperationalZones { get; set; }
}

public sealed class BoletaZoneDto
{
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal PriceLow { get; set; }
    public decimal PriceHigh { get; set; }
    public int ConfluenceScore { get; set; }
}

public sealed class ExecuteBoletaRequest
{
    public Guid? SessionId { get; set; }
    public string Symbol { get; set; } = "";
    /// <summary>Se null, a estratégia decide.</summary>
    public string? Direction { get; set; }
    public decimal? Volume { get; set; }
    public decimal? EntryPrice { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }
    public int? TargetLevel { get; set; }
    public bool DryRun { get; set; }
    public bool? AllowNeutralEntries { get; set; }
    public int? ConfluenceScore { get; set; }
    public string? Recommendation { get; set; }
    public string? Bias { get; set; }
    public string? RiskLevel { get; set; }
    /// <summary>Recomendação do painel Market Drivers — trava lado contrário no servidor.</summary>
    public string? MarketDriversRecommendation { get; set; }
    public decimal? SuggestedEntry { get; set; }
    public decimal? SuggestedStopLoss { get; set; }
    public decimal? SuggestedTakeProfit { get; set; }
    public decimal? LastPrice { get; set; }
    public int? WyckoffScore { get; set; }
    public List<BoletaZoneDto>? OperationalZones { get; set; }
}

public sealed class Mt5TradeResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? Ticket { get; set; }
    public string? OrderId { get; set; }
    public decimal? ExecutedPrice { get; set; }
    public decimal? ExecutedVolume { get; set; }
    public bool QueuedForEa { get; set; }
}

public sealed class Mt5PositionSnapshot
{
    public string Symbol { get; set; } = "";
    public string Direction { get; set; } = "";
    public decimal Volume { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal Profit { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }
    public long Ticket { get; set; }
}
