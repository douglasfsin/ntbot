using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NtBot.Infrastructure.Cache;
using NtBot.Infrastructure.Persistence;
using NtBot.MarketIntelligence.Cache;
using NtBot.MarketIntelligence.Configuration;
using NtBot.MarketIntelligence.Engine;
using NtBot.MarketIntelligence.Models;
using NtBot.MarketIntelligence.Providers;

namespace NtBot.MarketIntelligence.Services;

public interface IMarketIntelligenceService
{
    Task<MarketOverview> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketSnapshot>> GetByCategoryAsync(MarketCategory category, CancellationToken cancellationToken = default);
    Task<MarketSnapshot?> GetVixAsync(CancellationToken cancellationToken = default);
    Task<CorrelationResult> GetCorrelationAsync(CancellationToken cancellationToken = default);
    Task<QuantScore> GetQuantScoreAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketProviderStatusDto>> GetProvidersAsync(CancellationToken cancellationToken = default);
    Task EnableProviderAsync(Guid id, CancellationToken cancellationToken = default);
    Task DisableProviderAsync(Guid id, CancellationToken cancellationToken = default);
    Task ForceSyncAsync(CancellationToken cancellationToken = default);
}

public sealed class MarketIntelligenceService : IMarketIntelligenceService
{
    private readonly IEnumerable<IMarketDataProvider> _providers;
    private readonly IEnumerable<IMarketOverviewEnricher> _enrichers;
    private readonly IEnumerable<ICorrelationHistoryEnricher> _correlationEnrichers;
    private readonly IMarketIntelligenceEngine _engine;
    private readonly CorrelationEngine _correlationEngine;
    private readonly QuantScoreEngine _quantScoreEngine;
    private readonly IMarketIntelligenceCacheService _cache;
    private readonly IDbConfigurationCache _configCache;
    private readonly NtBotDbContext _db;
    private readonly ILogger<MarketIntelligenceService> _logger;

    public MarketIntelligenceService(
        IEnumerable<IMarketDataProvider> providers,
        IEnumerable<IMarketOverviewEnricher> enrichers,
        IEnumerable<ICorrelationHistoryEnricher> correlationEnrichers,
        IMarketIntelligenceEngine engine,
        CorrelationEngine correlationEngine,
        QuantScoreEngine quantScoreEngine,
        IMarketIntelligenceCacheService cache,
        IDbConfigurationCache configCache,
        NtBotDbContext db,
        ILogger<MarketIntelligenceService> logger)
    {
        _providers = providers;
        _enrichers = enrichers;
        _correlationEnrichers = correlationEnrichers;
        _engine = engine;
        _correlationEngine = correlationEngine;
        _quantScoreEngine = quantScoreEngine;
        _cache = cache;
        _configCache = configCache;
        _db = db;
        _logger = logger;
    }

    public async Task<MarketOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetAsync<MarketOverview>("market:overview", cancellationToken);
        if (cached is not null)
            return cached;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(25));

        try
        {
            var (snapshots, provider) = await FetchSnapshotsAsync(timeoutCts.Token);
            var overview = _engine.BuildOverview(snapshots, provider);

            foreach (var enricher in _enrichers)
            {
                try
                {
                    using var enrichCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    enrichCts.CancelAfter(TimeSpan.FromSeconds(10));
                    overview = await enricher.EnrichAsync(overview, enrichCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Market overview enricher timed out — keeping base overview");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Market overview enricher failed — keeping base overview");
                }
            }

            await _cache.SetAsync("market:overview", overview, TimeSpan.FromSeconds(60), CancellationToken.None);
            return overview;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Market overview timed out — returning empty overview");
            return _engine.BuildOverview([], "timeout");
        }
    }

    public async Task<IReadOnlyList<MarketSnapshot>> GetByCategoryAsync(
        MarketCategory category,
        CancellationToken cancellationToken = default)
    {
        var overview = await GetOverviewAsync(cancellationToken);
        return category switch
        {
            MarketCategory.Commodity => overview.Commodities,
            MarketCategory.Index => overview.Indexes,
            MarketCategory.Currency => overview.Currencies,
            MarketCategory.Treasury => overview.Treasury,
            MarketCategory.Sector => overview.Sectors,
            _ => []
        };
    }

    public async Task<MarketSnapshot?> GetVixAsync(CancellationToken cancellationToken = default)
    {
        var overview = await GetOverviewAsync(cancellationToken);
        return overview.Vix;
    }

    public async Task<CorrelationResult> GetCorrelationAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetAsync<CorrelationResult>("market:correlation", cancellationToken);
        if (cached is not null)
            return cached;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(25));

        try
        {
            var provider = _providers.First();
            var history = new ConcurrentDictionary<string, IReadOnlyList<PriceHistoryPoint>>(StringComparer.OrdinalIgnoreCase);

            var symbols = MarketAssetCatalog.All.Select(a => a.Symbol)
                .Concat(MarketAssetRelations.All.SelectMany(r => r.Drivers.Select(d => d.Symbol)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var gate = new SemaphoreSlim(4);
            try
            {
                var fetchTasks = symbols.Select(async symbol =>
                {
                    await gate.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                    try
                    {
                        var points = await provider.FetchHistoryAsync(symbol, 130, timeoutCts.Token)
                            .ConfigureAwait(false);
                        if (points.Count > 0)
                            history[symbol] = points;
                    }
                    catch (Exception ex) when (!IsBenignCancellation(ex))
                    {
                        _logger.LogDebug(ex, "Histórico de correlação indisponível para {Symbol}", symbol);
                    }
                    finally
                    {
                        try { gate.Release(); }
                        catch (ObjectDisposedException) { /* shut down race */ }
                    }
                }).ToArray();
                await Task.WhenAll(fetchTasks).ConfigureAwait(false);
            }
            finally
            {
                gate.Dispose();
            }

            foreach (var enricher in _correlationEnrichers)
                await enricher.EnrichHistoryAsync(history, timeoutCts.Token);

            var pairs = new List<CorrelationPairResult>();
            var keyPairs = new (string A, string LA, string B, string LB)[]
            {
                ("CL=F", "WTI", "BZ=F", "Brent"),
                ("HG=F", "Copper", "GC=F", "Gold"),
                ("^GSPC", "S&P500", "^IXIC", "NASDAQ"),
                ("BRL=X", "USD/BRL", "CL=F", "WTI")
            };

            foreach (var (a, la, b, lb) in keyPairs)
            {
                if (!history.TryGetValue(a, out var ha) || !history.TryGetValue(b, out var hb))
                    continue;

                pairs.Add(_correlationEngine.CalculatePair(a, la, b, lb, ha, hb));
            }

            var result = new CorrelationResult
            {
                Timestamp = DateTime.UtcNow,
                Pairs = pairs,
                AssetImpacts = _correlationEngine.BuildAssetImpacts(history)
            };

            await _cache.SetAsync("market:correlation", result, TimeSpan.FromMinutes(30), CancellationToken.None);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Market correlation timed out — returning empty correlation");
            return new CorrelationResult { Timestamp = DateTime.UtcNow };
        }
    }

    public async Task<QuantScore> GetQuantScoreAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetAsync<QuantScore>("market:quantscore", cancellationToken);
        if (cached is not null)
            return cached;

        var overview = await GetOverviewAsync(cancellationToken);
        var correlation = await GetCorrelationAsync(cancellationToken);
        var score = _quantScoreEngine.Calculate(overview, correlation);
        await _cache.SetAsync("market:quantscore", score, TimeSpan.FromSeconds(60), cancellationToken);
        return score;
    }

    public async Task<IReadOnlyList<MarketProviderStatusDto>> GetProvidersAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _configCache.GetMarketIntelligenceProvidersAsync(cancellationToken);
        return rows.Select(MapProvider).ToList();
    }

    public async Task EnableProviderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _db.MarketIntelligenceProviders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Provider not found");
        row.Enabled = true;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        _configCache.InvalidateMarketIntelligenceProviders();
        await InvalidateCacheAsync(cancellationToken);
    }

    public async Task DisableProviderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _db.MarketIntelligenceProviders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Provider not found");
        row.Enabled = false;
        row.Status = "disabled";
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        _configCache.InvalidateMarketIntelligenceProviders();
        await InvalidateCacheAsync(cancellationToken);
    }

    public async Task ForceSyncAsync(CancellationToken cancellationToken = default)
    {
        await InvalidateCacheAsync(cancellationToken);
        await GetOverviewAsync(cancellationToken);
        await GetCorrelationAsync(cancellationToken);
        await GetQuantScoreAsync(cancellationToken);
    }

    private async Task<(IReadOnlyList<MarketSnapshot> Snapshots, string Provider)> FetchSnapshotsAsync(
        CancellationToken cancellationToken)
    {
        foreach (var provider in _providers)
        {
            try
            {
                var info = await provider.GetRuntimeInfoAsync(cancellationToken);
                if (!info.Enabled) continue;

                var snapshots = await provider.FetchSnapshotsAsync(cancellationToken);
                if (snapshots.Count > 0)
                    return (snapshots, provider.Name);
            }
            catch (OperationCanceledException)
            {
                // upstream request cancelled — another in-flight refresh will finish
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Market provider {Provider} failed", provider.Name);
            }
        }

        return ([], "unavailable");
    }

    private async Task InvalidateCacheAsync(CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync("market:overview", cancellationToken);
        await _cache.RemoveAsync("market:correlation", cancellationToken);
        await _cache.RemoveAsync("market:quantscore", cancellationToken);
        await _cache.RemoveAsync("market:snapshots:yahoo", cancellationToken);
    }

    private static MarketProviderStatusDto MapProvider(Domain.Entities.MarketIntelligenceProvider row)
    {
        List<string> caps;
        try { caps = JsonSerializer.Deserialize<List<string>>(row.Capabilities) ?? []; }
        catch { caps = []; }

        return new MarketProviderStatusDto
        {
            Id = row.Id,
            Name = row.Name,
            Enabled = row.Enabled,
            RefreshIntervalSeconds = row.RefreshIntervalSeconds,
            HealthStatus = row.Enabled
                ? row.LastSync is null ? MarketProviderHealth.Degraded : MarketProviderHealth.Healthy
                : MarketProviderHealth.Disabled,
            LastSync = row.LastSync,
            Capabilities = caps
        };
    }

    private static bool IsBenignCancellation(Exception ex) =>
        ex is OperationCanceledException or TaskCanceledException
        || ex.InnerException is OperationCanceledException or TaskCanceledException;
}

public sealed class MarketRefreshWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<MarketIntelligenceOptions> _options;
    private readonly ILogger<MarketRefreshWorker> _logger;

    public MarketRefreshWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<MarketIntelligenceOptions> options,
        ILogger<MarketRefreshWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var market = scope.ServiceProvider.GetRequiredService<IMarketIntelligenceService>();
                    var notifier = scope.ServiceProvider.GetRequiredService<IMarketUpdateNotifier>();

                    var overview = await market.GetOverviewAsync(stoppingToken);
                    await notifier.NotifyOverviewUpdatedAsync(overview, stoppingToken);

                    var correlation = await market.GetCorrelationAsync(stoppingToken);
                    await notifier.NotifyCorrelationUpdatedAsync(correlation, stoppingToken);

                    var score = await market.GetQuantScoreAsync(stoppingToken);
                    await notifier.NotifyQuantScoreUpdatedAsync(score, stoppingToken);

                    var providers = await market.GetProvidersAsync(stoppingToken);
                    await notifier.NotifyProvidersUpdatedAsync(providers, stoppingToken);
                }
                catch (Exception ex) when (!IsBenignCancellation(ex))
                {
                    _logger.LogWarning(ex, "Market intelligence refresh cycle failed");
                }

                await Task.Delay(TimeSpan.FromSeconds(_options.Value.DefaultRefreshSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown normal da API
        }
    }

    private static bool IsBenignCancellation(Exception ex) =>
        ex is OperationCanceledException or TaskCanceledException
        || ex.InnerException is OperationCanceledException or TaskCanceledException;
}
