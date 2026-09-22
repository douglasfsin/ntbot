using System.Collections.Concurrent;

namespace NtBot.Connector.Windows.MarketData;

public sealed class MarketDataCache : IMarketDataCache
{
    private readonly ConcurrentDictionary<string, MarketTick> _ticks = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _ticks.Count;

    public void Upsert(MarketTick tick) => _ticks[tick.CacheKey] = tick;

    public bool TryGet(string cacheKey, out MarketTick tick) =>
        _ticks.TryGetValue(cacheKey, out tick!);

    public bool Remove(string cacheKey) => _ticks.TryRemove(cacheKey, out _);

    public int RemoveWhere(Func<MarketTick, bool> predicate)
    {
        var removed = 0;
        foreach (var tick in _ticks.Values)
        {
            if (!predicate(tick))
                continue;
            if (_ticks.TryRemove(tick.CacheKey, out _))
                removed++;
        }

        return removed;
    }

    public IReadOnlyCollection<MarketTick> Snapshot() =>
        _ticks.Values.ToArray();
}
