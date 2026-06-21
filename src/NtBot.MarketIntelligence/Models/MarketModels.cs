using NtBot.MarketIntelligence.Configuration;

namespace NtBot.MarketIntelligence.Models;

public enum MarketStatus
{
    Unknown,
    Open,
    Closed,
    PreMarket,
    AfterHours
}

public enum MarketProviderHealth
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy,
    Disabled
}

public sealed class MarketSnapshot
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Provider { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public MarketCategory Category { get; init; }
    public decimal Price { get; init; }
    public decimal Change { get; init; }
    public decimal ChangePercent { get; init; }
    public long Volume { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal PreviousClose { get; init; }
    public MarketStatus MarketStatus { get; init; } = MarketStatus.Unknown;
}

public sealed class MarketOverview
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Provider { get; init; } = string.Empty;
    public IReadOnlyList<MarketSnapshot> Commodities { get; init; } = [];
    public IReadOnlyList<MarketSnapshot> Indexes { get; init; } = [];
    public IReadOnlyList<MarketSnapshot> Currencies { get; init; } = [];
    public IReadOnlyList<MarketSnapshot> Treasury { get; init; } = [];
    public IReadOnlyList<MarketSnapshot> Sectors { get; init; } = [];
    public string MarketRegime { get; init; } = "Neutral";
    public MarketSnapshot? Vix { get; init; }
}

public sealed class CorrelationPairResult
{
    public string SymbolA { get; init; } = string.Empty;
    public string SymbolB { get; init; } = string.Empty;
    public string LabelA { get; init; } = string.Empty;
    public string LabelB { get; init; } = string.Empty;
    public double Correlation30D { get; init; }
    public double Correlation60D { get; init; }
    public double Correlation120D { get; init; }
}

public sealed class ImpactFactor
{
    public string Symbol { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public double Correlation { get; init; }
    public double Weight { get; init; }
}

public sealed class AssetImpactResult
{
    public string Asset { get; init; } = string.Empty;
    public IReadOnlyList<ImpactFactor> Factors { get; init; } = [];
    public string Recommendation { get; init; } = "Neutral";
    public double ImpactScore { get; init; }
    public double? BasketWeightPercent { get; init; }
}

public sealed class CorrelationResult
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public IReadOnlyList<CorrelationPairResult> Pairs { get; init; } = [];
    public IReadOnlyList<AssetImpactResult> AssetImpacts { get; init; } = [];
}

public sealed class QuantScore
{
    public int Score { get; init; }
    public string Label { get; init; } = "Neutral";
    public IReadOnlyDictionary<string, decimal> ComponentScores { get; init; } =
        new Dictionary<string, decimal>();

    public IReadOnlyDictionary<string, decimal> Weights { get; init; } =
        new Dictionary<string, decimal>();
}

public sealed class MarketProviderStatusDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public int RefreshIntervalSeconds { get; init; }
    public MarketProviderHealth HealthStatus { get; init; }
    public DateTime? LastSync { get; init; }
    public IReadOnlyList<string> Capabilities { get; init; } = [];
}

public sealed class MarketProviderRuntimeInfo
{
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public MarketProviderHealth HealthStatus { get; init; }
    public DateTime? LastUpdate { get; init; }
    public IReadOnlyList<string> Capabilities { get; init; } = [];
}

public sealed class PriceHistoryPoint
{
    public DateTime Date { get; init; }
    public decimal Close { get; init; }
}

public sealed class HeatMapCell
{
    public string Symbol { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public MarketCategory Category { get; init; }
    public decimal ChangePercent { get; init; }
}
