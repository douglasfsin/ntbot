using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NtBot.MarketIntelligence.Configuration;

namespace NtBot.MarketIntelligence.Cache;

public interface IMarketIntelligenceCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class MarketIntelligenceCacheService : IMarketIntelligenceCacheService
{
    private readonly IMemoryCache _memory;
    private readonly MarketIntelligenceOptions _options;
    private readonly ILogger<MarketIntelligenceCacheService> _logger;

    public MarketIntelligenceCacheService(
        IMemoryCache memory,
        IOptions<MarketIntelligenceOptions> options,
        ILogger<MarketIntelligenceCacheService> logger)
    {
        _memory = memory;
        _options = options.Value;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_memory.TryGetValue(key, out T? value))
            return Task.FromResult(value);

        if (_options.UseRedis && !string.IsNullOrWhiteSpace(_options.RedisConnectionString))
            _logger.LogDebug("Redis configured; memory miss for {Key}", key);

        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        _memory.Set(key, value, ttl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _memory.Remove(key);
        return Task.CompletedTask;
    }
}
