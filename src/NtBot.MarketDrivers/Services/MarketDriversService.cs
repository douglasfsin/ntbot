using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.MarketDrivers.Cache;
using NtBot.MarketDrivers.Configuration;
using NtBot.MarketDrivers.Engine;
using NtBot.MarketDrivers.Models;
using NtBot.MarketDrivers.Providers;

namespace NtBot.MarketDrivers.Services;

public sealed class MarketDriversAIService
{
    public MarketDriversAISummary Summarize(
        MarketDriverContext context,
        IReadOnlyList<MarketDriver> drivers,
        DriverScore score)
    {
        var positive = drivers
            .Where(d => d.Impact is DriverImpactLevel.VeryPositive or DriverImpactLevel.Positive or DriverImpactLevel.SlightlyPositive)
            .Select(d => $"{d.Name}: {d.Recommendation} ({d.Variation:+0.0;-0.0;0.0}%)")
            .Take(5)
            .ToList();

        var negative = drivers
            .Where(d => d.Impact is DriverImpactLevel.VeryNegative or DriverImpactLevel.Negative or DriverImpactLevel.SlightlyNegative)
            .Select(d => $"{d.Name}: {d.Recommendation} ({d.Variation:+0.0;-0.0;0.0}%)")
            .Take(5)
            .ToList();

        var recent = drivers
            .Where(d => Math.Abs(d.Variation) >= 0.5m)
            .OrderByDescending(d => Math.Abs(d.Variation))
            .Select(d => $"{d.Name} moveu {d.Variation:+0.00;-0.00;0.00}%")
            .Take(4)
            .ToList();

        var events = context.Macro.UpcomingEvents
            .Where(e => e.Impact is "High" or "Medium")
            .OrderBy(e => e.EventTime)
            .Take(3)
            .Select(e => $"{e.EventName} ({e.Country}) em {e.EventTime:dd/MM HH:mm}")
            .ToList();

        var expected = score.Recommendation switch
        {
            "COMPRA FORTE" => $"Alta probabilidade de movimento favorável para {context.Asset} com suporte de macro e drivers de mercado.",
            "COMPRA" => $"Viés comprador moderado para {context.Asset}; monitorar confirmação de fluxo.",
            "VENDA FORTE" => $"Pressão vendedora significativa sobre {context.Asset} nos próximos pregões.",
            "VENDA" => $"Viés vendedor leve; reduzir exposição ou aguardar reversão.",
            _ => $"Ambiente equilibrado para {context.Asset}; aguardar catalisadores."
        };

        return new MarketDriversAISummary
        {
            PositiveFactors = positive,
            NegativeFactors = negative,
            RecentChanges = recent,
            RelevantEvents = events,
            ExpectedImpact = expected
        };
    }
}

public interface IMarketDriversUpdateNotifier
{
    Task NotifySnapshotUpdatedAsync(MarketDriversSnapshot snapshot, CancellationToken cancellationToken = default);
    Task NotifyDashboardUpdatedAsync(IReadOnlyList<MarketDriversDashboardItem> items, CancellationToken cancellationToken = default);
}

public interface IMarketDriversService
{
    Task<MarketDriversSnapshot?> GetSnapshotAsync(string asset, CancellationToken cancellationToken = default);
    MarketDriversSnapshot? GetLastKnownSnapshot(string asset);
    Task<IReadOnlyList<MarketDriversDashboardItem>> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task ForceRefreshAsync(CancellationToken cancellationToken = default);
}

public sealed class MarketDriversService : IMarketDriversService
{
    private static readonly ConcurrentDictionary<string, Task<MarketDriversSnapshot?>> InflightBuilds =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly MarketDriverContextBuilder _contextBuilder;
    private readonly IMarketDriverProvider _provider;
    private readonly IMarketDriverEngine _engine;
    private readonly IDriverCompositionStore _composition;
    private readonly IMarketDriversCacheService _redisCache;
    private readonly IOptions<MarketDriversOptions> _options;
    private readonly ILogger<MarketDriversService> _logger;
    private readonly Dictionary<string, (MarketDriversSnapshot Snapshot, DateTime Expires)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MarketDriversSnapshot> _lastKnown = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public MarketDriversService(
        MarketDriverContextBuilder contextBuilder,
        IMarketDriverProvider provider,
        IMarketDriverEngine engine,
        IDriverCompositionStore composition,
        IMarketDriversCacheService redisCache,
        IOptions<MarketDriversOptions> options,
        ILogger<MarketDriversService> logger)
    {
        _contextBuilder = contextBuilder;
        _provider = provider;
        _engine = engine;
        _composition = composition;
        _redisCache = redisCache;
        _options = options;
        _logger = logger;
    }

    public async Task<MarketDriversSnapshot?> GetSnapshotAsync(string asset, CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(asset);
        Task<MarketDriversSnapshot?> inflight;
        while (true)
        {
            if (InflightBuilds.TryGetValue(normalized, out var running))
            {
                inflight = running;
                break;
            }

            // Shared build must not abort when one HTTP client times out.
            var task = GetSnapshotCoreAsync(normalized, CancellationToken.None);
            if (InflightBuilds.TryAdd(normalized, task))
            {
                inflight = AwaitAndCleanupDriversAsync(normalized, task);
                break;
            }
        }

        try
        {
            return await inflight.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return GetLastKnownSnapshot(normalized);
        }
    }

    private static async Task<MarketDriversSnapshot?> AwaitAndCleanupDriversAsync(
        string key,
        Task<MarketDriversSnapshot?> task)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            InflightBuilds.TryRemove(key, out _);
        }
    }

    private async Task<MarketDriversSnapshot?> GetSnapshotCoreAsync(string normalized, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!await IsAssetSupportedAsync(normalized, cancellationToken))
                return null;

            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_cache.TryGetValue(normalized, out var cached) && cached.Expires > DateTime.UtcNow)
                {
                    _logger.LogDebug(
                        "Drivers span asset={Asset} source=memory duration_ms={DurationMs} status=ok",
                        normalized, sw.ElapsedMilliseconds);
                    return cached.Snapshot;
                }
            }
            finally
            {
                _lock.Release();
            }

            var fromRedis = await _redisCache.GetAsync(normalized, cancellationToken);
            if (fromRedis is not null)
            {
                await _lock.WaitAsync(cancellationToken);
                try
                {
                    _cache[normalized] = (fromRedis, DateTime.UtcNow.AddSeconds(_options.Value.DefaultRefreshSeconds));
                    _lastKnown[normalized] = fromRedis;
                }
                finally
                {
                    _lock.Release();
                }

                _logger.LogInformation(
                    "Drivers span asset={Asset} source=redis duration_ms={DurationMs} status=ok",
                    normalized, sw.ElapsedMilliseconds);
                return fromRedis;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(50));

            try
            {
                var context = await _contextBuilder.BuildAsync(normalized, timeoutCts.Token);
                var drivers = await _provider.BuildDriversAsync(context, timeoutCts.Token);
                var snapshot = _engine.BuildSnapshot(context, drivers);

                await _lock.WaitAsync(cancellationToken);
                try
                {
                    _cache[normalized] = (snapshot, DateTime.UtcNow.AddSeconds(_options.Value.DefaultRefreshSeconds));
                    _lastKnown[normalized] = snapshot;
                }
                finally
                {
                    _lock.Release();
                }

                await _redisCache.SetAsync(normalized, snapshot, cancellationToken);
                _logger.LogInformation(
                    "Drivers span asset={Asset} source=build duration_ms={DurationMs} status=ok",
                    normalized, sw.ElapsedMilliseconds);
                return snapshot;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Market drivers snapshot timed out for {Asset} duration_ms={DurationMs}",
                    normalized, sw.ElapsedMilliseconds);
                if (_lastKnown.TryGetValue(normalized, out var stale))
                    return stale;

                return RememberDegraded(normalized, "Timeout ao consultar Yahoo/macro — tente Atualizar em alguns segundos.");
            }
        }
        catch (OperationCanceledException)
        {
            if (_lastKnown.TryGetValue(normalized, out var stale))
                return stale;
            return RememberDegraded(normalized, "Requisição cancelada.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Market drivers snapshot failed for {Asset} duration_ms={DurationMs}",
                normalized, sw.ElapsedMilliseconds);
            if (_lastKnown.TryGetValue(normalized, out var stale))
                return stale;
            return RememberDegraded(normalized, ex.Message);
        }
    }

    private MarketDriversSnapshot RememberDegraded(string asset, string reason)
    {
        var degraded = CreateDegradedSnapshot(asset, reason);
        _lastKnown[asset] = degraded;
        _cache[asset] = (degraded, DateTime.UtcNow.AddSeconds(30));
        return degraded;
    }

    private static MarketDriversSnapshot CreateDegradedSnapshot(string asset, string reason) =>
        new()
        {
            Asset = asset,
            Timestamp = DateTime.UtcNow,
            Drivers = [],
            Score = new DriverScore
            {
                Score = 50,
                Label = "Neutro",
                Classification = "Neutro",
                Recommendation = "AGUARDAR DADOS",
                Confidence = 0,
                DataQuality = "Insuficiente",
                KnownComponentCount = 0
            },
            Explanation = $"Drivers temporariamente indisponíveis para {asset}.\n\n{reason}",
            HeatMap = [],
            AiSummary = new MarketDriversAISummary
            {
                ExpectedImpact = reason
            }
        };

    public MarketDriversSnapshot? GetLastKnownSnapshot(string asset)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(asset);
        return _lastKnown.TryGetValue(normalized, out var snapshot) ? snapshot : null;
    }

    public async Task<IReadOnlyList<MarketDriversDashboardItem>> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<MarketDriversDashboardItem>();
        foreach (var asset in _options.Value.DashboardAssets)
        {
            var snapshot = await GetSnapshotAsync(asset, cancellationToken);
            if (snapshot is null) continue;

            items.Add(new MarketDriversDashboardItem
            {
                Asset = snapshot.Asset,
                Score = snapshot.Score,
                TopDrivers = snapshot.Drivers.Take(6).ToList(),
                ExplanationPreview = snapshot.Explanation.Split('\n').FirstOrDefault() ?? string.Empty
            });
        }

        return items;
    }

    private async Task<bool> IsAssetSupportedAsync(string normalized, CancellationToken cancellationToken)
    {
        if (Configuration.MarketDriversCatalog.IsSupported(normalized))
            return true;
        if (await _composition.HasCustomCompositionAsync(normalized, cancellationToken: cancellationToken))
            return true;
        return _options.Value.DashboardAssets.Any(a =>
            string.Equals(a, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public async Task ForceRefreshAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();
        foreach (var asset in _options.Value.DashboardAssets)
            await _redisCache.RemoveAsync(asset, cancellationToken);
    }
}

public sealed class MarketDriversRefreshWorker : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<MarketDriversOptions> _options;
    private readonly ILogger<MarketDriversRefreshWorker> _logger;

    public MarketDriversRefreshWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<MarketDriversOptions> options,
        ILogger<MarketDriversRefreshWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IMarketDriversService>();
                    var notifier = scope.ServiceProvider.GetService<IMarketDriversUpdateNotifier>();

                    await service.ForceRefreshAsync(stoppingToken);
                    var dashboard = await service.GetDashboardAsync(stoppingToken);
                    if (notifier is not null)
                        await notifier.NotifyDashboardUpdatedAsync(dashboard, stoppingToken);

                    foreach (var asset in _options.Value.DashboardAssets)
                    {
                        var snapshot = await service.GetSnapshotAsync(asset, stoppingToken);
                        if (snapshot is not null && notifier is not null)
                            await notifier.NotifySnapshotUpdatedAsync(snapshot, stoppingToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Market drivers refresh cycle failed");
                }

                await Task.Delay(TimeSpan.FromSeconds(_options.Value.DefaultRefreshSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown normal da API
        }
    }
}
