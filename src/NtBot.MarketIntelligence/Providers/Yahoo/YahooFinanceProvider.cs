using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NtBot.Infrastructure.Persistence;
using NtBot.MarketIntelligence.Cache;
using NtBot.MarketIntelligence.Configuration;
using NtBot.MarketIntelligence.Models;

namespace NtBot.MarketIntelligence.Providers.Yahoo;

public sealed class YahooFinanceProvider : IMarketDataProvider
{
    private readonly YahooFinanceClient _client;
    private readonly IMarketIntelligenceCacheService _cache;
    private readonly NtBotDbContext _db;
    private readonly MarketIntelligenceOptions _options;
    private readonly ILogger<YahooFinanceProvider> _logger;

    public YahooFinanceProvider(
        YahooFinanceClient client,
        IMarketIntelligenceCacheService cache,
        NtBotDbContext db,
        Microsoft.Extensions.Options.IOptions<MarketIntelligenceOptions> options,
        ILogger<YahooFinanceProvider> logger)
    {
        _client = client;
        _cache = cache;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => MarketProviderNames.YahooFinance;
    public IReadOnlyList<string> Capabilities { get; } =
        ["commodities", "indexes", "currencies", "treasury", "sectors", "history"];

    public async Task<MarketProviderRuntimeInfo> GetRuntimeInfoAsync(CancellationToken cancellationToken = default)
    {
        var config = await _db.MarketIntelligenceProviders.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == Name, cancellationToken);

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
        var config = await _db.MarketIntelligenceProviders.FirstOrDefaultAsync(p => p.Name == Name, cancellationToken);
        if (config is null || !config.Enabled)
            return [];

        var cacheKey = "market:snapshots:yahoo";
        var cached = await _cache.GetAsync<List<MarketSnapshot>>(cacheKey, cancellationToken);
        if (cached is not null && cached.Count > 0)
            return cached;

        var snapshots = new List<MarketSnapshot>();
        foreach (var asset in MarketAssetCatalog.All)
        {
            var chart = await _client.GetChartAsync(asset.Symbol, "1d", "5d", cancellationToken);
            if (chart is null) continue;

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
                cancellationToken);
        }

        if (snapshots.Count == 0)
        {
            _logger.LogWarning("Yahoo Finance returned no market snapshots");
            return [];
        }

        var ttl = TimeSpan.FromSeconds(config.RefreshIntervalSeconds > 0
            ? config.RefreshIntervalSeconds
            : _options.DefaultRefreshSeconds);

        await _cache.SetAsync(cacheKey, snapshots, ttl, cancellationToken);

        config.LastSync = DateTime.UtcNow;
        config.Status = "healthy";
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return snapshots;
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
}
