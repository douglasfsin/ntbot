using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.MarketData;

public enum ProviderHealthLevel
{
    Healthy,
    Warning,
    Stale,
    Disconnected
}

public sealed record ProviderHealthSnapshot
{
    public BrokerSource Provider { get; init; }
    public ProviderHealthLevel Level { get; init; }
    public DateTime? LastTickUtc { get; init; }
    public TimeSpan? TimeSinceLastTick { get; init; }
    public int ReconnectCount { get; init; }
    public int FailureCount { get; init; }
    public double TicksPerSecond { get; init; }
    public int ActiveSymbols { get; init; }
    public string? Message { get; init; }
}

public interface IProviderHealth
{
    void RecordTick(BrokerSource provider, string symbol, DateTime timestampUtc);

    void RecordReconnect(BrokerSource provider);

    void RecordFailure(BrokerSource provider, string message);

    ProviderHealthSnapshot GetSnapshot(BrokerSource provider, ProviderHealthLevel level, TimeSpan? staleThreshold);

    IReadOnlyList<ProviderHealthSnapshot> GetAllSnapshots(TimeSpan warningThreshold, TimeSpan reconnectThreshold);
}

public interface IProviderMonitor
{
    MarketDataDiagnosticsSnapshot GetDiagnostics();
}

public sealed record MarketDataDiagnosticsSnapshot
{
    public int CacheSymbolCount { get; init; }
    public int ChannelQueueDepth { get; init; }
    public long PublishedTotal { get; init; }
    public long DroppedTotal { get; init; }
    public double TicksPerSecond { get; init; }
    public double AverageLatencyMs { get; init; }
    public double MaxLatencyMs { get; init; }
    public IReadOnlyList<ProviderHealthSnapshot> Providers { get; init; } = [];
}
