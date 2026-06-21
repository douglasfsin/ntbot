using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NtBot.MarketDrivers.Providers;
using NtBot.TradingIntelligence.Cache;
using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Persistence;
using NtBot.TradingIntelligence.Services;

namespace NtBot.TradingIntelligence;

public static class DependencyInjection
{
    public static IServiceCollection AddTradingIntelligence(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TradingIntelligenceOptions>(
            configuration.GetSection(TradingIntelligenceOptions.SectionName));

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        services.AddScoped<IConfluenceEngine, ConfluenceEngine>();
        services.AddScoped<IOperationalZoneEngine, OperationalZoneEngine>();
        services.AddSingleton<ISmcEngine, SmcEngine>();
        services.AddSingleton<ITradingIntelligenceCacheService, TradingIntelligenceCacheService>();
        services.AddScoped<ITradingIntelligenceService, TradingIntelligenceService>();
        services.AddScoped<IDriverCompositionAdminService, DriverCompositionAdminService>();
        services.AddScoped<IDriverCompositionRepository, DriverCompositionRepository>();
        services.AddScoped<IDriverCompositionStore, DriverCompositionStore>();
        services.AddHostedService<TradingIntelligenceRefreshWorker>();

        return services;
    }
}

public sealed class TradingIntelligenceRefreshWorker : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<TradingIntelligenceOptions> _options;
    private readonly ILogger<TradingIntelligenceRefreshWorker> _logger;
    private int _cycle;

    public TradingIntelligenceRefreshWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<TradingIntelligenceOptions> options,
        ILogger<TradingIntelligenceRefreshWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ITradingIntelligenceService>();
                var notifier = scope.ServiceProvider.GetService<ITradingIntelligenceUpdateNotifier>();

                _cycle++;
                var aiInterval = Math.Max(1,
                    _options.Value.AiRefreshSeconds / Math.Max(1, _options.Value.DefaultRefreshSeconds));
                var forceAiRefresh = _cycle % aiInterval == 0;

                if (forceAiRefresh)
                {
                    await service.RefreshAllAsync(notifyClients: notifier is not null, cancellationToken: stoppingToken);
                }
                else
                {
                    foreach (var asset in _options.Value.SupportedAssets)
                    {
                        var snapshot = await service.GetSnapshotAsync(asset, cancellationToken: stoppingToken);
                        if (snapshot is not null && notifier is not null)
                            await notifier.NotifySnapshotUpdatedAsync(snapshot, stoppingToken);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Trading intelligence refresh cycle failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.Value.DefaultRefreshSeconds), stoppingToken);
        }
    }
}
