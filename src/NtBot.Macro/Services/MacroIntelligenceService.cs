using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NtBot.Infrastructure.Persistence;
using NtBot.Macro.Cache;
using NtBot.Macro.Configuration;
using NtBot.Macro.Engine;
using NtBot.Macro.Providers;
using NtBot.Macro.Providers.Calendar;
using NtBot.Macro.Providers.Fred;
using NtBot.Macro.Rules;

namespace NtBot.Macro.Services;

public interface IMacroIntelligenceService
{
    Task<MacroSnapshot> GetCurrentSnapshotAsync(string? symbol = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MacroProviderStatusDto>> GetProvidersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MacroCalendarEventDto>> GetCalendarAsync(CancellationToken cancellationToken = default);
    Task EnableProviderAsync(Guid id, CancellationToken cancellationToken = default);
    Task DisableProviderAsync(Guid id, CancellationToken cancellationToken = default);
    Task ConfigureProviderAsync(Guid id, MacroProviderConfigureRequest request, CancellationToken cancellationToken = default);
}

public sealed class MacroIntelligenceService : IMacroIntelligenceService
{
    private readonly IEnumerable<IMacroProvider> _providers;
    private readonly IMacroEngine _engine;
    private readonly IMacroCacheService _cache;
    private readonly NtBotDbContext _db;
    private readonly ILogger<MacroIntelligenceService> _logger;

    public MacroIntelligenceService(
        IEnumerable<IMacroProvider> providers,
        IMacroEngine engine,
        IMacroCacheService cache,
        NtBotDbContext db,
        ILogger<MacroIntelligenceService> logger)
    {
        _providers = providers;
        _engine = engine;
        _cache = cache;
        _db = db;
        _logger = logger;
    }

    public async Task<MacroSnapshot> GetCurrentSnapshotAsync(string? symbol = null, CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = string.IsNullOrWhiteSpace(symbol)
            ? "all"
            : MacroSymbolAliases.Normalize(symbol);
        var cacheKey = $"macro:snapshot:{normalizedSymbol}";
        var cached = await _cache.GetAsync<MacroSnapshot>(cacheKey, cancellationToken);
        if (cached is not null) return cached;

        var payloads = new List<MacroProviderPayload>();
        foreach (var provider in _providers.OrderBy(p => p.Priority))
        {
            try
            {
                var info = await provider.GetRuntimeInfoAsync(cancellationToken);
                if (!info.Enabled) continue;

                var payload = await provider.FetchAsync(cancellationToken);
                if (payload is not null) payloads.Add(payload);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Macro provider {Provider} failed", provider.Name);
            }
        }

        var snapshot = _engine.BuildSnapshot(payloads, symbol);
        await _cache.SetAsync(cacheKey, snapshot, TimeSpan.FromMinutes(1), cancellationToken);
        return snapshot;
    }

    public async Task<IReadOnlyList<MacroProviderStatusDto>> GetProvidersAsync(CancellationToken cancellationToken = default)
    {
        var dbProviders = await _db.MacroProviders.AsNoTracking().OrderBy(p => p.Priority).ToListAsync(cancellationToken);
        var result = new List<MacroProviderStatusDto>();

        foreach (var db in dbProviders)
        {
            var runtime = _providers.FirstOrDefault(p => p.Name == db.Name);
            MacroProviderRuntimeInfo? info = null;
            if (runtime is not null)
            {
                info = await runtime.GetRuntimeInfoAsync(cancellationToken);
            }

            result.Add(new MacroProviderStatusDto
            {
                Id = db.Id,
                Name = db.Name,
                Enabled = db.Enabled,
                Priority = db.Priority,
                HealthStatus = info?.HealthStatus ?? ParseHealth(db.Status),
                LastUpdate = db.LastSync ?? info?.LastUpdate,
                RefreshIntervalMinutes = db.RefreshIntervalMinutes,
                Capabilities = ParseCapabilities(db.Capabilities),
                ApiUrl = db.ApiUrl
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<MacroCalendarEventDto>> GetCalendarAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCurrentSnapshotAsync(cancellationToken: cancellationToken);
        return snapshot.UpcomingEvents;
    }

    public async Task EnableProviderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var provider = await _db.MacroProviders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Provider {id} not found");
        provider.Enabled = true;
        provider.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await InvalidateProviderCachesAsync(provider.Name, cancellationToken);
    }

    public async Task DisableProviderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var provider = await _db.MacroProviders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Provider {id} not found");
        provider.Enabled = false;
        provider.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await InvalidateProviderCachesAsync(provider.Name, cancellationToken);
    }

    public async Task ConfigureProviderAsync(Guid id, MacroProviderConfigureRequest request, CancellationToken cancellationToken = default)
    {
        var provider = await _db.MacroProviders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Provider {id} not found");

        if (request.ApiUrl is not null) provider.ApiUrl = request.ApiUrl;
        if (request.ApiKey is not null) provider.ApiKey = request.ApiKey;
        if (request.RefreshIntervalMinutes is > 0) provider.RefreshIntervalMinutes = request.RefreshIntervalMinutes.Value;
        if (request.Priority is > 0) provider.Priority = request.Priority.Value;
        provider.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await InvalidateProviderCachesAsync(provider.Name, cancellationToken);
    }

    private async Task InvalidateProviderCachesAsync(string providerName, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync($"macro:provider:{providerName}", cancellationToken);
        await _cache.RemoveAsync("macro:snapshot:all", cancellationToken);
    }

    private static MacroProviderHealth ParseHealth(string status) => status.ToLowerInvariant() switch
    {
        "healthy" => MacroProviderHealth.Healthy,
        "degraded" => MacroProviderHealth.Degraded,
        "unhealthy" => MacroProviderHealth.Unhealthy,
        "disabled" => MacroProviderHealth.Disabled,
        _ => MacroProviderHealth.Unknown
    };

    private static IReadOnlyList<string> ParseCapabilities(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

public sealed class MacroRefreshWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MacroRefreshWorker> _logger;

    public MacroRefreshWorker(IServiceScopeFactory scopeFactory, ILogger<MacroRefreshWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var macro = scope.ServiceProvider.GetRequiredService<IMacroIntelligenceService>();
                var notifier = scope.ServiceProvider.GetRequiredService<IMacroUpdateNotifier>();
                var snapshot = await macro.GetCurrentSnapshotAsync(cancellationToken: stoppingToken);
                await notifier.NotifySnapshotUpdatedAsync(snapshot, stoppingToken);
                var providers = await macro.GetProvidersAsync(stoppingToken);
                await notifier.NotifyProvidersUpdatedAsync(providers, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Macro refresh cycle failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
