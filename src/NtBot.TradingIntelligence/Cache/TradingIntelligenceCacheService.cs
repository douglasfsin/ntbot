using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Models;
using StackExchange.Redis;

namespace NtBot.TradingIntelligence.Cache;

public interface ITradingIntelligenceCacheService
{
    Task<TradingIntelligenceSnapshot?> GetSnapshotAsync(string asset, Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task SetSnapshotAsync(string asset, TradingIntelligenceSnapshot snapshot, Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task RemoveSnapshotAsync(string asset, Guid? tenantId = null, CancellationToken cancellationToken = default);
}

public sealed class TradingIntelligenceCacheService : ITradingIntelligenceCacheService, IDisposable
{
    private readonly IMemoryCache _memory;
    private readonly TradingIntelligenceOptions _options;
    private readonly ILogger<TradingIntelligenceCacheService> _logger;
    private readonly Lazy<IConnectionMultiplexer?> _redis;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TradingIntelligenceCacheService(
        IMemoryCache memory,
        IOptions<TradingIntelligenceOptions> options,
        ILogger<TradingIntelligenceCacheService> logger)
    {
        _memory = memory;
        _options = options.Value;
        _logger = logger;
        _redis = new Lazy<IConnectionMultiplexer?>(CreateRedis);
    }

    public async Task<TradingIntelligenceSnapshot?> GetSnapshotAsync(
        string asset,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(asset, tenantId);
        if (_memory.TryGetValue(key, out TradingIntelligenceSnapshot? cached) && cached is not null)
            return cached;

        if (!_options.UseRedis || _redis.Value is null)
            return null;

        try
        {
            var db = _redis.Value.GetDatabase();
            var payload = await db.StringGetAsync(key);
            if (payload.IsNullOrEmpty)
                return null;

            var snapshot = JsonSerializer.Deserialize<TradingIntelligenceSnapshot>(payload!, JsonOptions);
            if (snapshot is not null)
                _memory.Set(key, snapshot, TimeSpan.FromSeconds(_options.CacheTtlSeconds));

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis read failed for trading intelligence key {Key}", key);
            return null;
        }
    }

    public async Task SetSnapshotAsync(
        string asset,
        TradingIntelligenceSnapshot snapshot,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(asset, tenantId);
        var ttl = TimeSpan.FromSeconds(_options.CacheTtlSeconds);
        _memory.Set(key, snapshot, ttl);

        if (!_options.UseRedis || _redis.Value is null)
            return;

        try
        {
            var db = _redis.Value.GetDatabase();
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            await db.StringSetAsync(key, json, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis write failed for trading intelligence key {Key}", key);
        }
    }

    public async Task RemoveSnapshotAsync(string asset, Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(asset, tenantId);
        _memory.Remove(key);

        if (!_options.UseRedis || _redis.Value is null)
            return;

        try
        {
            await _redis.Value.GetDatabase().KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis delete failed for trading intelligence key {Key}", key);
        }
    }

    public void Dispose()
    {
        if (_redis.IsValueCreated && _redis.Value is not null)
            _redis.Value.Dispose();
    }

    private IConnectionMultiplexer? CreateRedis()
    {
        if (!_options.UseRedis || string.IsNullOrWhiteSpace(_options.RedisConnectionString))
            return null;

        try
        {
            return ConnectionMultiplexer.Connect(_options.RedisConnectionString);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to connect Redis for trading intelligence cache");
            return null;
        }
    }

    private static string BuildKey(string asset, Guid? tenantId) =>
        $"ti:snapshot:{tenantId?.ToString() ?? "global"}:{asset.ToUpperInvariant()}";
}
