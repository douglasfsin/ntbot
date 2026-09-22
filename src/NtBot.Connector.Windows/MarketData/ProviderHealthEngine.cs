using System.Collections.Concurrent;
using NtBot.Connector.Windows.Configuration;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.MarketData;

public sealed class ProviderHealthEngine : IProviderHealth
{
    private sealed class ProviderState
    {
        public DateTime? LastTickUtc;
        public int ReconnectCount;
        public int FailureCount;
        public readonly ConcurrentDictionary<string, byte> Symbols = new(StringComparer.OrdinalIgnoreCase);
        public long TickCount;
        public DateTime WindowStartUtc = DateTime.UtcNow;
    }

    private readonly ConcurrentDictionary<BrokerSource, ProviderState> _states = new();

    public void RecordTick(BrokerSource provider, string symbol, DateTime timestampUtc)
    {
        var state = _states.GetOrAdd(provider, _ => new ProviderState());
        state.LastTickUtc = timestampUtc;
        state.Symbols[symbol] = 0;
        Interlocked.Increment(ref state.TickCount);
    }

    public void RecordReconnect(BrokerSource provider)
    {
        var state = _states.GetOrAdd(provider, _ => new ProviderState());
        Interlocked.Increment(ref state.ReconnectCount);
    }

    public void RecordFailure(BrokerSource provider, string message)
    {
        var state = _states.GetOrAdd(provider, _ => new ProviderState());
        Interlocked.Increment(ref state.FailureCount);
        _ = message;
    }

    public ProviderHealthSnapshot GetSnapshot(BrokerSource provider, ProviderHealthLevel level, TimeSpan? staleThreshold)
    {
        if (!_states.TryGetValue(provider, out var state))
        {
            return new ProviderHealthSnapshot
            {
                Provider = provider,
                Level = ProviderHealthLevel.Disconnected,
                Message = "Sem ticks registrados"
            };
        }

        var now = DateTime.UtcNow;
        var since = state.LastTickUtc.HasValue ? now - state.LastTickUtc.Value : (TimeSpan?)null;
        var tps = ComputeTicksPerSecond(state, now);

        return new ProviderHealthSnapshot
        {
            Provider = provider,
            Level = level,
            LastTickUtc = state.LastTickUtc,
            TimeSinceLastTick = since,
            ReconnectCount = state.ReconnectCount,
            FailureCount = state.FailureCount,
            TicksPerSecond = tps,
            ActiveSymbols = state.Symbols.Count,
            Message = BuildMessage(level, since, staleThreshold)
        };
    }

    public IReadOnlyList<ProviderHealthSnapshot> GetAllSnapshots(TimeSpan warningThreshold, TimeSpan reconnectThreshold)
    {
        var providers = _states.Keys
            .Concat([BrokerSource.Profit, BrokerSource.MT5])
            .Distinct()
            .ToList();

        return providers
            .Select(p => GetSnapshot(p, ResolveLevel(p, warningThreshold, reconnectThreshold), reconnectThreshold))
            .ToList();
    }

    private ProviderHealthLevel ResolveLevel(BrokerSource provider, TimeSpan warningThreshold, TimeSpan reconnectThreshold)
    {
        if (!_states.TryGetValue(provider, out var state) || !state.LastTickUtc.HasValue)
            return ProviderHealthLevel.Disconnected;

        var age = DateTime.UtcNow - state.LastTickUtc.Value;
        if (age >= reconnectThreshold)
            return ProviderHealthLevel.Stale;
        if (age >= warningThreshold)
            return ProviderHealthLevel.Warning;
        return ProviderHealthLevel.Healthy;
    }

    private static double ComputeTicksPerSecond(ProviderState state, DateTime now)
    {
        var window = now - state.WindowStartUtc;
        if (window.TotalSeconds < 1)
            return state.TickCount;

        var tps = state.TickCount / window.TotalSeconds;
        state.TickCount = 0;
        state.WindowStartUtc = now;
        return tps;
    }

    private static string? BuildMessage(ProviderHealthLevel level, TimeSpan? since, TimeSpan? staleThreshold)
    {
        return level switch
        {
            ProviderHealthLevel.Healthy => null,
            ProviderHealthLevel.Warning => since.HasValue
                ? $"Sem tick há {since.Value.TotalSeconds:N0}s (warning)"
                : "Sem tick recente",
            ProviderHealthLevel.Stale => since.HasValue
                ? $"Sem tick há {since.Value.TotalSeconds:N0}s (stale ≥ {staleThreshold?.TotalSeconds:N0}s)"
                : "Provider stale",
            ProviderHealthLevel.Disconnected => "Desconectado ou sem dados",
            _ => null
        };
    }
}

public sealed class ProviderMonitor : IProviderMonitor
{
    private readonly IMarketDataBus _bus;
    private readonly IMarketDataCache _cache;
    private readonly IProviderHealth _health;
    private readonly MarketDataGatewayOptions _options;
    private long _processedTicks;
    private DateTime _windowStartUtc = DateTime.UtcNow;
    private double _latencySumMs;
    private long _latencySamples;
    private double _maxLatencyMs;

    public ProviderMonitor(
        IMarketDataBus bus,
        IMarketDataCache cache,
        IProviderHealth health,
        Microsoft.Extensions.Options.IOptions<MarketDataGatewayOptions> options)
    {
        _bus = bus;
        _cache = cache;
        _health = health;
        _options = options.Value;
    }

    public void RecordProcessedTick(MarketTick tick)
    {
        Interlocked.Increment(ref _processedTicks);
        var latencyMs = (DateTime.UtcNow - tick.TimestampUtc).TotalMilliseconds;
        if (latencyMs >= 0)
        {
            Interlocked.Increment(ref _latencySamples);
            _latencySumMs += latencyMs;
            if (latencyMs > _maxLatencyMs)
                _maxLatencyMs = latencyMs;
        }
    }

    public MarketDataDiagnosticsSnapshot GetDiagnostics()
    {
        var now = DateTime.UtcNow;
        var window = now - _windowStartUtc;
        var tps = window.TotalSeconds >= 1
            ? Interlocked.Read(ref _processedTicks) / window.TotalSeconds
            : Interlocked.Read(ref _processedTicks);

        if (window.TotalSeconds >= 5)
        {
            Interlocked.Exchange(ref _processedTicks, 0);
            _windowStartUtc = now;
        }

        var samples = Interlocked.Read(ref _latencySamples);
        var avgLatency = samples > 0 ? _latencySumMs / samples : 0;

        return new MarketDataDiagnosticsSnapshot
        {
            CacheSymbolCount = _cache.Count,
            ChannelQueueDepth = _bus.QueuedCount,
            PublishedTotal = _bus.PublishedCount,
            DroppedTotal = _bus.DroppedCount,
            TicksPerSecond = tps,
            AverageLatencyMs = avgLatency,
            MaxLatencyMs = _maxLatencyMs,
            Providers = _health.GetAllSnapshots(
                TimeSpan.FromMilliseconds(_options.WarningStaleMs),
                TimeSpan.FromMilliseconds(_options.ReconnectStaleMs))
        };
    }
}
