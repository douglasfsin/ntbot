using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.Macro.Configuration;

namespace NtBot.Macro.Cache;

public interface IMacroCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class MacroCacheService : IMacroCacheService
{
    private readonly IMemoryCache _memory;
    private readonly MacroOptions _options;
    private readonly ILogger<MacroCacheService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MacroCacheService(IMemoryCache memory, IOptions<MacroOptions> options, ILogger<MacroCacheService> logger)
    {
        _memory = memory;
        _options = options.Value;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_memory.TryGetValue(key, out T? value))
        {
            return Task.FromResult(value);
        }

        // Redis hook: when UseRedis is enabled, extend here without changing callers.
        if (_options.UseRedis && !string.IsNullOrWhiteSpace(_options.RedisConnectionString))
        {
            _logger.LogDebug("Redis configured for macro cache; memory miss for {Key}", key);
        }

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
