using Microsoft.Extensions.Options;
using NtBot.Api.Configuration;
using StackExchange.Redis;

namespace NtBot.Api.Services.Redis;

public interface IRedisConnectionAccessor
{
    bool IsAvailable { get; }
    string InstancePrefix { get; }
    IConnectionMultiplexer? Connection { get; }
    IDatabase? GetDatabase();
}

public sealed class RedisConnectionAccessor : IRedisConnectionAccessor, IDisposable
{
    private readonly Lazy<IConnectionMultiplexer?> _connection;
    private readonly ILogger<RedisConnectionAccessor> _logger;

    public RedisConnectionAccessor(
        IOptions<RedisOptions> options,
        ILogger<RedisConnectionAccessor> logger)
    {
        _logger = logger;
        var cfg = options.Value;
        InstancePrefix = string.IsNullOrWhiteSpace(cfg.InstanceName) ? "NTBot:" : cfg.InstanceName;
        var connectionString = cfg.Configuration?.Trim() ?? string.Empty;

        _connection = new Lazy<IConnectionMultiplexer?>(() =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogInformation("Redis not configured — candle/analysis cache disabled");
                return null;
            }

            try
            {
                var redisOptions = ConfigurationOptions.Parse(connectionString);
                redisOptions.AbortOnConnectFail = false;
                redisOptions.ConnectTimeout = 2_000;
                redisOptions.SyncTimeout = 3_000;
                redisOptions.AsyncTimeout = 3_000;
                redisOptions.ConnectRetry = 2;

                var mux = ConnectionMultiplexer.Connect(redisOptions);
                if (mux.IsConnected)
                    _logger.LogInformation("Redis connected ({Endpoint})", connectionString);
                else
                    _logger.LogWarning(
                        "Redis multiplexer created but not connected yet ({Endpoint}) — will retry in background",
                        connectionString);
                return mux;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to connect Redis at {Endpoint}", connectionString);
                return null;
            }
        });
    }

    public string InstancePrefix { get; }

    public IConnectionMultiplexer? Connection => _connection.Value;

    public bool IsAvailable
    {
        get
        {
            try
            {
                return Connection is { IsConnected: true };
            }
            catch
            {
                return false;
            }
        }
    }

    public IDatabase? GetDatabase()
    {
        try
        {
            var mux = Connection;
            if (mux is null || !mux.IsConnected)
                return null;
            return mux.GetDatabase();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis GetDatabase failed");
            return null;
        }
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated && _connection.Value is not null)
            _connection.Value.Dispose();
    }
}
