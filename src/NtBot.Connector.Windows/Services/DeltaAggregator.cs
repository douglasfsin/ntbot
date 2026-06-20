using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Core;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.Services;

public interface IDeltaAggregator
{
    NormalizedIngestBatch Diff(NormalizedIngestBatch current, NormalizedIngestBatch? previous);
}

public class DeltaAggregator : IDeltaAggregator
{
    public NormalizedIngestBatch Diff(NormalizedIngestBatch current, NormalizedIngestBatch? previous)
    {
        if (previous == null)
            return current with { IsDelta = false };

        return new NormalizedIngestBatch
        {
            SessionId = current.SessionId,
            ConnectorVersion = current.ConnectorVersion,
            IsDelta = true,
            Ticks = DiffList(current.Ticks, previous.Ticks, t => $"{t.Source}:{t.Symbol}"),
            Positions = DiffList(current.Positions, previous.Positions, p => $"{p.Source}:{p.Symbol}"),
            Orders = DiffList(current.Orders, previous.Orders, o => $"{o.Source}:{o.OrderId}"),
            Executions = DiffList(current.Executions, previous.Executions, e => $"{e.Source}:{e.ExecutionId}"),
            Signals = DiffList(current.Signals, previous.Signals, s => $"{s.Source}:{s.SignalId}"),
            Account = AccountChanged(current.Account, previous.Account) ? current.Account : null,
            BrokerStatuses = DiffList(current.BrokerStatuses, previous.BrokerStatuses, b => b.Source.ToString())
        };
    }

    private static List<T>? DiffList<T>(List<T>? current, List<T>? previous, Func<T, string> keySelector)
    {
        if (current == null || current.Count == 0) return null;

        var prevMap = previous?.ToDictionary(keySelector, x => x) ?? new Dictionary<string, T>();
        var changed = current.Where(item =>
        {
            var key = keySelector(item);
            return !prevMap.TryGetValue(key, out var old) || !EqualityComparer<T>.Default.Equals(old, item);
        }).ToList();

        return changed.Count == 0 ? null : changed;
    }

    private static bool AccountChanged(NormalizedAccount? current, NormalizedAccount? previous)
    {
        if (current == null) return false;
        if (previous == null) return true;
        return current.Balance != previous.Balance
            || current.Equity != previous.Equity
            || current.Margin != previous.Margin
            || current.FreeMargin != previous.FreeMargin;
    }
}

public class ConnectorSessionState
{
    public string SessionId { get; set; } = string.Empty;
}

public class ProviderOrchestrator
{
    private readonly IEnumerable<IBrokerPlugin> _plugins;
    private readonly IDeltaAggregator _delta;
    private readonly IMemoryCache _cache;
    private readonly ConnectorSessionState _session;
    private readonly ConnectorOptions _options;

    public ProviderOrchestrator(
        IEnumerable<IBrokerPlugin> plugins,
        IDeltaAggregator delta,
        IMemoryCache cache,
        ConnectorSessionState session,
        IOptions<ConnectorOptions> options)
    {
        _plugins = plugins;
        _delta = delta;
        _cache = cache;
        _session = session;
        _options = options.Value;
    }

    public async Task<NormalizedIngestBatch> CollectAsync(CancellationToken ct)
    {
        var positions = new List<NormalizedPosition>();
        var orders = new List<NormalizedOrder>();
        var executions = new List<NormalizedExecution>();
        var statuses = new List<NormalizedBrokerStatus>();
        NormalizedAccount? account = null;

        foreach (var plugin in _plugins)
        {
            positions.AddRange(await plugin.GetPositionsAsync(ct));
            orders.AddRange(await plugin.GetOrdersAsync(ct));
            executions.AddRange(await plugin.GetRecentExecutionsAsync(ct));
            statuses.Add(new NormalizedBrokerStatus
            {
                Source = plugin.Source,
                IsConnected = plugin.IsConnected,
                Status = plugin.IsConnected ? "connected" : "disconnected",
                TimestampUtc = DateTime.UtcNow
            });

            account ??= await plugin.GetAccountAsync(ct);
        }

        var ticks = (_cache.Get<List<NormalizedMarketTick>>("connector:ticks") ?? [])
            .Select(t => t with { })
            .ToList();

        var batch = new NormalizedIngestBatch
        {
            SessionId = _session.SessionId,
            ConnectorVersion = _options.Version,
            Ticks = ticks,
            Positions = positions,
            Orders = orders,
            Executions = executions,
            BrokerStatuses = statuses,
            Account = account
        };

        var snapshot = SnapshotBatch(batch);
        var previous = _cache.Get<NormalizedIngestBatch>("connector:last-batch");
        var delta = _delta.Diff(snapshot, previous);
        _cache.Set("connector:last-batch", snapshot, TimeSpan.FromMinutes(10));
        return delta;
    }

    private static NormalizedIngestBatch SnapshotBatch(NormalizedIngestBatch batch) =>
        batch with
        {
            Ticks = batch.Ticks?.Select(t => t with { }).ToList(),
            Positions = batch.Positions?.Select(p => p with { }).ToList(),
            Orders = batch.Orders?.Select(o => o with { }).ToList(),
            Executions = batch.Executions?.Select(e => e with { }).ToList(),
            Signals = batch.Signals?.Select(s => s with { }).ToList(),
            BrokerStatuses = batch.BrokerStatuses?.Select(b => b with { }).ToList(),
            Account = batch.Account is null ? null : batch.Account with { }
        };

    public void PushTick(NormalizedMarketTick tick)
    {
        var list = _cache.GetOrCreate("connector:ticks", _ => new List<NormalizedMarketTick>())!;
        var idx = list.FindIndex(t => t.Source == tick.Source && t.Symbol == tick.Symbol);
        if (idx >= 0) list[idx] = tick;
        else list.Add(tick);
        _cache.Set("connector:ticks", list, TimeSpan.FromMinutes(10));
    }
}
