using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NtBot.Infrastructure.Cache;
using NtBot.Infrastructure.Persistence;
using NtBot.MarketIntelligence.Cache;
using NtBot.MarketIntelligence.Configuration;
using NtBot.MarketIntelligence.Models;

namespace NtBot.MarketIntelligence.Providers.Yahoo;

public sealed class YahooFinanceProvider : IMarketDataProvider
{
    private const int MaxParallelFetches = 4;
    private static readonly string SnapshotsCacheKey = "market:snapshots:yahoo";

    private readonly YahooFinanceClient _client;
    private readonly IMarketIntelligenceCacheService _cache;
    private readonly IDbConfigurationCache _configCache;
    private readonly NtBotDbContext _db;
    private readonly MarketIntelligenceOptions _options;
    private readonly ILogger<YahooFinanceProvider> _logger;

    public YahooFinanceProvider(
        YahooFinanceClient client,
        IMarketIntelligenceCacheService cache,
        IDbConfigurationCache configCache,
        NtBotDbContext db,
        Microsoft.Extensions.Options.IOptions<MarketIntelligenceOptions> options,
        ILogger<YahooFinanceProvider> logger)
    {
        _client = client;
        _cache = cache;
        _configCache = configCache;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => MarketProviderNames.YahooFinance;
    public IReadOnlyList<string> Capabilities { get; } =
        ["commodities", "indexes", "currencies", "treasury", "sectors", "history"];

    public async Task<MarketProviderRuntimeInfo> GetRuntimeInfoAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configCache.GetMarketIntelligenceProviderByNameAsync(Name, cancellationToken);

        var enabled = config?.Enabled ?? true;
        return new MarketProviderRuntimeInfo
        {
            Name = Name,
            Enabled = enabled,
            HealthStatus = enabled
                ? config?.LastSync is null ? MarketProviderHealth.Degraded : MarketProviderHealth.Healthy
                : MarketProviderHealth.Disabled,
            LastUpdate = config?.LastSync,
            Capabilities = Capabilities
        };
    }

    public async Task<IReadOnlyList<MarketSnapshot>> FetchSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        Domain.Entities.MarketIntelligenceProvider? config;
        try
        {
            config = await _configCache.GetMarketIntelligenceProviderByNameAsync(Name, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return await ReadCachedSnapshotsAsync(CancellationToken.None) ?? [];
        }

        if (config is null || !config.Enabled)
            return [];

        var cached = await ReadCachedSnapshotsAsync(cancellationToken);
        if (cached is { Count: > 0 })
            return cached;

        try
        {
            var snapshots = await FetchAllSnapshotsParallelAsync(cancellationToken);
            if (snapshots.Count == 0)
            {
                _logger.LogWarning("Yahoo Finance returned no market snapshots");
                return cached ?? [];
            }

            var ttl = TimeSpan.FromSeconds(config.RefreshIntervalSeconds > 0
                ? config.RefreshIntervalSeconds
                : _options.DefaultRefreshSeconds);

            await _cache.SetAsync(SnapshotsCacheKey, snapshots, ttl, cancellationToken);
            _ = PersistProviderSyncAsync(config.Id);
            return snapshots;
        }
        catch (OperationCanceledException)
        {
            return await ReadCachedSnapshotsAsync(CancellationToken.None) ?? cached ?? [];
        }
    }

    public async Task<IReadOnlyList<PriceHistoryPoint>> FetchHistoryAsync(
        string symbol,
        int days,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"market:history:{symbol}";
        var cached = await _cache.GetAsync<List<PriceHistoryPoint>>(cacheKey, cancellationToken);
        if (cached is not null && cached.Count >= Math.Min(days, 30))
            return cached;

        var range = days switch
        {
            <= 30 => "1mo",
            <= 90 => "3mo",
            <= 180 => "6mo",
            _ => "1y"
        };

        var chart = await _client.GetChartAsync(symbol, "1d", range, cancellationToken);
        if (chart is null)
            return cached ?? [];

        var points = chart.Points.TakeLast(days).ToList();
        await _cache.SetAsync(cacheKey, points, TimeSpan.FromMinutes(30), cancellationToken);
        return points;
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var chart = await _client.GetChartAsync("^GSPC", "1d", "5d", cancellationToken);
        return chart is not null;
    }

    private async Task<List<MarketSnapshot>?> ReadCachedSnapshotsAsync(CancellationToken cancellationToken) =>
        await _cache.GetAsync<List<MarketSnapshot>>(SnapshotsCacheKey, cancellationToken);

    private async Task<List<MarketSnapshot>> FetchAllSnapshotsParallelAsync(CancellationToken cancellationToken)
    {
        var snapshots = new ConcurrentBag<MarketSnapshot>();
        // Keep gate alive until every parallel worker finishes Release — avoid
        // ObjectDisposedException when cancellation races with using-dispose.
        var gate = new SemaphoreSlim(MaxParallelFetches, MaxParallelFetches);
        try
        {
            var tasks = MarketAssetCatalog.All.Select(async asset =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var chart = await _client.GetChartAsync(asset.Symbol, "1d", "5d", cancellationToken)
                        .ConfigureAwait(false);
                    if (chart is null)
                        return;

                    snapshots.Add(new MarketSnapshot
                    {
                        Timestamp = DateTime.UtcNow,
                        Provider = Name,
                        Symbol = asset.Symbol,
                        Name = asset.Name,
                        Category = asset.Category,
                        Price = chart.Price,
                        Change = chart.Change,
                        ChangePercent = chart.ChangePercent,
                        Volume = chart.Volume,
                        Open = chart.Open,
                        High = chart.High,
                        Low = chart.Low,
                        PreviousClose = chart.PreviousClose,
                        MarketStatus = chart.MarketStatus
                    });

                    await _cache.SetAsync(
                        $"market:history:{asset.Symbol}",
                        chart.Points.ToList(),
                        TimeSpan.FromMinutes(30),
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    try { gate.Release(); }
                    catch (ObjectDisposedException) { /* shut down race */ }
                }
            }).ToArray();

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return snapshots.ToList();
        }
        finally
        {
            gate.Dispose();
        }
    }

    private async Task PersistProviderSyncAsync(Guid providerId)
    {
        try
        {
            var config = await _db.MarketIntelligenceProviders.FindAsync(providerId);
            if (config is null)
                return;

            config.LastSync = DateTime.UtcNow;
            config.Status = "healthy";
            config.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None);
            _configCache.UpdateMarketIntelligenceProvider(config);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to persist Yahoo Finance sync metadata");
        }
    }
}
