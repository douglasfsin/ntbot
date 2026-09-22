using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using NtBot.TradingIntelligence.Configuration;

namespace NtBot.TradingIntelligence.Cache;

public sealed class CachedEngineEntry<T>
{
    public required T Value { get; init; }
    public DateTime ComputedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastCandleTime { get; init; }
    public string Source { get; init; } = string.Empty;
}

public interface ITradingEngineCacheService
{
    CachedEngineEntry<T>? Get<T>(string asset, string engineKey);
    void Set<T>(string asset, string engineKey, T value, DateTime? lastCandleTime, string source);
    bool IsFresh(string asset, string engineKey, DateTime? latestCandleTime);
    void InvalidateAsset(string asset);
}

internal sealed class EngineCacheItem
{
    public required object Value { get; init; }
    public DateTime ComputedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastCandleTime { get; init; }
    public string Source { get; init; } = string.Empty;
}

public sealed class TradingEngineCacheService : ITradingEngineCacheService
{
    private readonly ConcurrentDictionary<string, EngineCacheItem> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly TradingIntelligenceOptions _options;

    public TradingEngineCacheService(IOptions<TradingIntelligenceOptions> options) =>
        _options = options.Value;

    public CachedEngineEntry<T>? Get<T>(string asset, string engineKey)
    {
        if (!_entries.TryGetValue(BuildKey(asset, engineKey), out var item))
            return null;

        if (DateTime.UtcNow - item.ComputedAt > TimeSpan.FromSeconds(_options.CacheTtlSeconds))
            return null;

        if (item.Value is not T typed)
            return null;

        return new CachedEngineEntry<T>
        {
            Value = typed,
            ComputedAt = item.ComputedAt,
            LastCandleTime = item.LastCandleTime,
            Source = item.Source
        };
    }

    public void Set<T>(string asset, string engineKey, T value, DateTime? lastCandleTime, string source) =>
        _entries[BuildKey(asset, engineKey)] = new EngineCacheItem
        {
            Value = value!,
            ComputedAt = DateTime.UtcNow,
            LastCandleTime = lastCandleTime,
            Source = source
        };

    public bool IsFresh(string asset, string engineKey, DateTime? latestCandleTime)
    {
        if (!_entries.TryGetValue(BuildKey(asset, engineKey), out var item))
            return false;

        if (DateTime.UtcNow - item.ComputedAt > TimeSpan.FromSeconds(_options.CacheTtlSeconds))
            return false;

        if (latestCandleTime.HasValue && item.LastCandleTime.HasValue &&
            latestCandleTime.Value > item.LastCandleTime.Value)
            return false;

        return true;
    }

    public void InvalidateAsset(string asset)
    {
        var prefix = $"{asset.ToUpperInvariant()}:";
        foreach (var key in _entries.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            _entries.TryRemove(key, out _);
    }

    private static string BuildKey(string asset, string engineKey) =>
        $"{asset.ToUpperInvariant()}:{engineKey}";
}
