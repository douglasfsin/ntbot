using System.Collections.Concurrent;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Services;

public interface IConnectorLiveState
{
    void ApplyBatch(Guid tenantId, NormalizedIngestBatch batch);
    ConnectorLiveSnapshot? GetSnapshot(Guid tenantId);
}

public sealed class ConnectorLiveSnapshot
{
    public DateTime UpdatedUtc { get; init; }
    public bool IsLive { get; init; }
    public int TotalTicksReceived { get; init; }
    public double SecondsSinceLastData { get; init; }
    public IReadOnlyDictionary<string, NormalizedMarketTick> Ticks { get; init; } =
        new Dictionary<string, NormalizedMarketTick>();

    public IReadOnlyList<NormalizedBrokerStatus> BrokerStatuses { get; init; } = [];
}

internal sealed class TenantLiveState
{
    public ConcurrentDictionary<string, NormalizedMarketTick> Ticks { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<NormalizedBrokerStatus> BrokerStatuses { get; set; } = [];
    public int TotalTicksReceived;
    public DateTime LastUpdateUtc;
}

public sealed class ConnectorLiveState : IConnectorLiveState
{
    private static readonly TimeSpan LiveWindow = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<Guid, TenantLiveState> _tenants = new();

    public void ApplyBatch(Guid tenantId, NormalizedIngestBatch batch)
    {
        var state = _tenants.GetOrAdd(tenantId, _ => new TenantLiveState());
        var now = DateTime.UtcNow;

        foreach (var tick in batch.Ticks ?? [])
        {
            state.Ticks[tick.Symbol] = tick;
            state.TotalTicksReceived++;

            if (tick.Symbol.Equals("WIN", StringComparison.OrdinalIgnoreCase))
                state.Ticks["WINFUT"] = tick with { Symbol = "WINFUT" };
        }

        if (batch.BrokerStatuses?.Count > 0)
            state.BrokerStatuses = batch.BrokerStatuses;

        state.LastUpdateUtc = now;
    }

    public ConnectorLiveSnapshot? GetSnapshot(Guid tenantId)
    {
        if (!_tenants.TryGetValue(tenantId, out var state) || state.LastUpdateUtc == default)
            return null;

        var age = DateTime.UtcNow - state.LastUpdateUtc;
        return new ConnectorLiveSnapshot
        {
            UpdatedUtc = state.LastUpdateUtc,
            IsLive = age <= LiveWindow,
            TotalTicksReceived = state.TotalTicksReceived,
            SecondsSinceLastData = age.TotalSeconds,
            Ticks = state.Ticks.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            BrokerStatuses = state.BrokerStatuses.ToList()
        };
    }
}
