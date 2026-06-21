using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NtBot.Infrastructure.Persistence;
using NtBot.Macro.Cache;
using NtBot.Macro.Configuration;

namespace NtBot.Macro.Providers.Fred;

public sealed class FredMacroProvider : IMacroProvider
{
    private readonly FredApiClient _client;
    private readonly IFredApiKeyResolver _apiKeyResolver;
    private readonly IMacroCacheService _cache;
    private readonly NtBotDbContext _db;
    private readonly ILogger<FredMacroProvider> _logger;

    public FredMacroProvider(
        FredApiClient client,
        IFredApiKeyResolver apiKeyResolver,
        IMacroCacheService cache,
        NtBotDbContext db,
        ILogger<FredMacroProvider> logger)
    {
        _client = client;
        _apiKeyResolver = apiKeyResolver;
        _cache = cache;
        _db = db;
        _logger = logger;
    }

    public string Name => MacroProviderNames.Fred;
    public int Priority => 1;
    public IReadOnlyList<string> Capabilities { get; } =
        ["rates", "inflation", "volatility", "employment", "liquidity"];

    public async Task<MacroProviderRuntimeInfo> GetRuntimeInfoAsync(CancellationToken cancellationToken = default)
    {
        var config = await _db.MacroProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Name == Name, cancellationToken);
        var enabled = config?.Enabled ?? false;
        var apiKey = await _apiKeyResolver.GetApiKeyAsync(cancellationToken);

        var health = !enabled
            ? MacroProviderHealth.Disabled
            : string.IsNullOrWhiteSpace(apiKey)
                ? MacroProviderHealth.Degraded
                : MacroProviderHealth.Healthy;

        return new MacroProviderRuntimeInfo
        {
            Name = Name,
            Enabled = enabled,
            Priority = config?.Priority ?? Priority,
            HealthStatus = health,
            LastUpdate = config?.LastSync,
            Capabilities = Capabilities
        };
    }

    public async Task<MacroProviderPayload?> FetchAsync(CancellationToken cancellationToken = default)
    {
        var config = await _db.MacroProviders.FirstOrDefaultAsync(p => p.Name == Name, cancellationToken);
        if (config is null || !config.Enabled)
        {
            return null;
        }

        var cacheKey = $"macro:provider:{Name}";
        var cached = await _cache.GetAsync<MacroProviderPayload>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var apiKey = await _apiKeyResolver.GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("FRED provider enabled but API key is missing");
            config.Status = "degraded";
            config.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }

        var series = new[]
        {
            FredSeries.FedFunds, FredSeries.Us10Y, FredSeries.Us2Y, FredSeries.Vix,
            FredSeries.Unemployment, FredSeries.Cpi, FredSeries.Pce, FredSeries.Payems
        };

        var indicators = new List<MacroIndicatorValue>();
        foreach (var id in series)
        {
            var obs = await _client.GetLatestObservationAsync(id, apiKey, cancellationToken);
            if (obs is null) continue;
            FredSeries.Labels.TryGetValue(id, out var label);
            indicators.Add(new MacroIndicatorValue
            {
                SeriesId = id,
                Label = label ?? id,
                Value = obs.Value,
                ObservedAt = obs.Date,
                Unit = id switch
                {
                    FredSeries.Vix => "index",
                    FredSeries.Cpi or FredSeries.Pce => "index",
                    FredSeries.Payems => "thousands",
                    _ => "%"
                }
            });
        }

        if (indicators.Count == 0)
        {
            _logger.LogWarning("FRED provider returned no indicators");
            return null;
        }

        var payload = new MacroProviderPayload
        {
            ProviderName = Name,
            FetchedAt = DateTime.UtcNow,
            Indicators = indicators
        };

        var ttl = TimeSpan.FromMinutes(config.RefreshIntervalMinutes > 0 ? config.RefreshIntervalMinutes : 30);
        await _cache.SetAsync(cacheKey, payload, ttl, cancellationToken);

        config.LastSync = DateTime.UtcNow;
        config.Status = "healthy";
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return payload;
    }
}
