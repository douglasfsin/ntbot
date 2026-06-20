namespace NtBot.Shared.Normalized;

/// <summary>
/// Modelos normalizados — única superfície entre Connector Windows e API/Web.
/// Nenhum consumer deve depender de Profit, MT5 ou NinjaTrader.
/// </summary>

public enum BrokerSource
{
    Unknown = 0,
    Profit = 1,
    MT5 = 2,
    NinjaTrader = 3,
    TradingView = 4
}

public enum NormalizedSide
{
    Buy,
    Sell,
    Flat
}

public enum NormalizedOrderStatus
{
    Pending,
    Open,
    PartiallyFilled,
    Filled,
    Cancelled,
    Rejected
}

public record NormalizedMarketTick
{
    public string Symbol { get; init; } = string.Empty;
    public BrokerSource Source { get; init; }
    public decimal? Last { get; init; }
    public decimal? Bid { get; init; }
    public decimal? Ask { get; init; }
    public long? Volume { get; init; }
    public DateTime TimestampUtc { get; init; }
    public string? TenantId { get; init; }
}

public record NormalizedOrder
{
    public string OrderId { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public BrokerSource Source { get; init; }
    public NormalizedSide Side { get; init; }
    public NormalizedOrderStatus Status { get; init; }
    public decimal Quantity { get; init; }
    public decimal? Price { get; init; }
    public decimal? FilledQuantity { get; init; }
    public DateTime TimestampUtc { get; init; }
}

public record NormalizedPosition
{
    public string Symbol { get; init; } = string.Empty;
    public BrokerSource Source { get; init; }
    public NormalizedSide Side { get; init; }
    public decimal Quantity { get; init; }
    public decimal AveragePrice { get; init; }
    public decimal UnrealizedPnL { get; init; }
    public decimal RealizedPnL { get; init; }
    public DateTime TimestampUtc { get; init; }
}

public record NormalizedExecution
{
    public string ExecutionId { get; init; } = string.Empty;
    public string OrderId { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public BrokerSource Source { get; init; }
    public NormalizedSide Side { get; init; }
    public decimal Quantity { get; init; }
    public decimal Price { get; init; }
    public decimal Commission { get; init; }
    public DateTime TimestampUtc { get; init; }
}

public record NormalizedSignal
{
    public string SignalId { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public BrokerSource Source { get; init; }
    public NormalizedSide Direction { get; init; }
    public string Strategy { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public string? Message { get; init; }
    public DateTime TimestampUtc { get; init; }
}

public record NormalizedAccount
{
    public string AccountId { get; init; } = string.Empty;
    public BrokerSource Source { get; init; }
    public decimal Balance { get; init; }
    public decimal Equity { get; init; }
    public decimal Margin { get; init; }
    public decimal FreeMargin { get; init; }
    public string Currency { get; init; } = "USD";
    public DateTime TimestampUtc { get; init; }
}

public record NormalizedBrokerStatus
{
    public BrokerSource Source { get; init; }
    public bool IsConnected { get; init; }
    public string Status { get; init; } = "unknown";
    public string? Message { get; init; }
    public DateTime TimestampUtc { get; init; }
}

public record NormalizedIngestBatch
{
    public string SessionId { get; init; } = string.Empty;
    public string ConnectorVersion { get; init; } = string.Empty;
    public bool IsDelta { get; init; }
    public List<NormalizedMarketTick>? Ticks { get; init; }
    public List<NormalizedPosition>? Positions { get; init; }
    public List<NormalizedOrder>? Orders { get; init; }
    public List<NormalizedExecution>? Executions { get; init; }
    public List<NormalizedSignal>? Signals { get; init; }
    public NormalizedAccount? Account { get; init; }
    public List<NormalizedBrokerStatus>? BrokerStatuses { get; init; }
}
