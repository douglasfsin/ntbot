using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NtBot.Infrastructure.Persistence;
using NtBot.Macro.Cache;
using NtBot.Macro.Configuration;

namespace NtBot.Macro.Providers.Calendar;

public sealed class Mt5CalendarMacroProvider : IMacroProvider
{
    private readonly IMacroCacheService _cache;
    private readonly NtBotDbContext _db;
    private readonly ILogger<Mt5CalendarMacroProvider> _logger;

    public Mt5CalendarMacroProvider(
        IMacroCacheService cache,
        NtBotDbContext db,
        ILogger<Mt5CalendarMacroProvider> logger)
    {
        _cache = cache;
        _db = db;
        _logger = logger;
    }

    public string Name => MacroProviderNames.Mt5Calendar;
    public int Priority => 2;
    public IReadOnlyList<string> Capabilities { get; } = ["calendar", "events"];

    public async Task<MacroProviderRuntimeInfo> GetRuntimeInfoAsync(CancellationToken cancellationToken = default)
    {
        var config = await _db.MacroProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Name == Name, cancellationToken);
        var enabled = config?.Enabled ?? false;

        MacroProviderHealth health;
        if (!enabled)
        {
            health = MacroProviderHealth.Disabled;
        }
        else if (config?.LastSync is null)
        {
            health = MacroProviderHealth.Degraded;
        }
        else
        {
            health = MacroProviderHealth.Healthy;
        }

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

        var from = DateTime.UtcNow.AddHours(-1);
        var to = DateTime.UtcNow.AddDays(14);

        var events = await _db.EconomicEvents
            .AsNoTracking()
            .Where(e => e.EventTime >= from && e.EventTime <= to)
            .OrderBy(e => e.EventTime)
            .Take(100)
            .Select(e => new MacroCalendarEventDto
            {
                Id = e.Id,
                EventName = e.EventName,
                Country = e.Country,
                Currency = e.Currency,
                Impact = e.Impact.ToString(),
                EventTime = e.EventTime,
                Actual = e.Actual,
                Forecast = e.Forecast,
                Previous = e.Previous
            })
            .ToListAsync(cancellationToken);

        var payload = new MacroProviderPayload
        {
            ProviderName = Name,
            FetchedAt = DateTime.UtcNow,
            Events = events
        };

        var ttl = TimeSpan.FromMinutes(config.RefreshIntervalMinutes > 0 ? config.RefreshIntervalMinutes : 5);
        await _cache.SetAsync(cacheKey, payload, ttl, cancellationToken);

        config.LastSync = DateTime.UtcNow;
        config.Status = "healthy";
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (events.Count == 0)
        {
            _logger.LogDebug("MT5 calendar provider: no upcoming events in database");
        }

        return payload;
    }
}
