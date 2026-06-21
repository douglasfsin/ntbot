using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    Task<IReadOnlyList<MarketDriversDashboardItem>> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task ForceRefreshAsync(CancellationToken cancellationToken = default);
}

public sealed class MarketDriversService : IMarketDriversService
{
    private readonly MarketDriverContextBuilder _contextBuilder;
    private readonly IMarketDriverProvider _provider;
    private readonly IMarketDriverEngine _engine;
    private readonly IOptions<MarketDriversOptions> _options;
    private readonly ILogger<MarketDriversService> _logger;
    private readonly Dictionary<string, (MarketDriversSnapshot Snapshot, DateTime Expires)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public MarketDriversService(
        MarketDriverContextBuilder contextBuilder,
        IMarketDriverProvider provider,
        IMarketDriverEngine engine,
        IOptions<MarketDriversOptions> options,
        ILogger<MarketDriversService> logger)
    {
        _contextBuilder = contextBuilder;
        _provider = provider;
        _engine = engine;
        _options = options;
        _logger = logger;
    }

    public async Task<MarketDriversSnapshot?> GetSnapshotAsync(string asset, CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(asset);
        if (!Configuration.MarketDriversCatalog.IsSupported(normalized))
            return null;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(normalized, out var cached) && cached.Expires > DateTime.UtcNow)
                return cached.Snapshot;
        }
        finally
        {
            _lock.Release();
        }

        try
        {
            var context = await _contextBuilder.BuildAsync(normalized, cancellationToken);
            var drivers = await _provider.BuildDriversAsync(context, cancellationToken);
            var snapshot = _engine.BuildSnapshot(context, drivers);

            await _lock.WaitAsync(cancellationToken);
            try
            {
                _cache[normalized] = (snapshot, DateTime.UtcNow.AddSeconds(_options.Value.DefaultRefreshSeconds));
            }
            finally
            {
                _lock.Release();
            }

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Market drivers snapshot failed for {Asset}", normalized);
            return null;
        }
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

    public Task ForceRefreshAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();
        return Task.CompletedTask;
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
}
