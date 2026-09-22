using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.MarketDrivers.Configuration;
using NtBot.MarketDrivers.Models;
using StackExchange.Redis;

namespace NtBot.MarketDrivers.Cache;

public interface IMarketDriversCacheService
{
    Task<MarketDriversSnapshot?> GetAsync(string asset, CancellationToken cancellationToken = default);
    Task SetAsync(string asset, MarketDriversSnapshot snapshot, CancellationToken cancellationToken = default);
    Task RemoveAsync(string asset, CancellationToken cancellationToken = default);
}

public sealed class MarketDriversCacheService : IMarketDriversCacheService, IDisposable
{
    private readonly MarketDriversOptions _options;
    private readonly ILogger<MarketDriversCacheService> _logger;
    private readonly Lazy<IConnectionMultiplexer?> _redis;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MarketDriversCacheService(
        IOptions<MarketDriversOptions> options,
        ILogger<MarketDriversCacheService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _redis = new Lazy<IConnectionMultiplexer?>(CreateRedis);
    }

    public async Task<MarketDriversSnapshot?> GetAsync(string asset, CancellationToken cancellationToken = default)
    {
        if (!_options.UseRedis || _redis.Value is null)
            return null;

        var key = BuildKey(asset);
        try
        {
            var payload = await _redis.Value.GetDatabase().StringGetAsync(key);
            if (payload.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<MarketDriversSnapshot>(payload!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis read failed for market drivers key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync(string asset, MarketDriversSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (!_options.UseRedis || _redis.Value is null)
            return;

        var key = BuildKey(asset);
        var ttl = TimeSpan.FromSeconds(Math.Max(30, _options.CacheTtlSeconds));
        try
        {
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            await _redis.Value.GetDatabase().StringSetAsync(key, json, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis write failed for market drivers key {Key}", key);
        }
    }

    public async Task RemoveAsync(string asset, CancellationToken cancellationToken = default)
    {
        if (!_options.UseRedis || _redis.Value is null)
            return;

        try
        {
            await _redis.Value.GetDatabase().KeyDeleteAsync(BuildKey(asset));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis delete failed for market drivers {Asset}", asset);
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
            _logger.LogWarning(ex, "Unable to connect Redis for market drivers cache");
            return null;
        }
    }

    private static string BuildKey(string asset) =>
        $"md:snapshot:{asset.ToUpperInvariant()}";
}
