using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;
using NtBot.Macro.Cache;
using NtBot.Macro.Configuration;

namespace NtBot.Macro.Providers.Calendar;

public sealed class Mt5CalendarMacroProvider : IMacroProvider
{
    private readonly IMacroCacheService _cache;
    private readonly NtBotDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Mt5CalendarMacroProvider> _logger;

    public Mt5CalendarMacroProvider(
        IMacroCacheService cache,
        NtBotDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<Mt5CalendarMacroProvider> logger)
    {
        _cache = cache;
        _db = db;
        _httpClientFactory = httpClientFactory;
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

        if (!string.IsNullOrWhiteSpace(config.ApiUrl))
        {
            await TryIngestFromMt5ApiAsync(config.ApiUrl, cancellationToken);
        }

        var events = await QueryUpcomingEventsAsync(cancellationToken);

        var payload = new MacroProviderPayload
        {
            ProviderName = Name,
            FetchedAt = DateTime.UtcNow,
            Events = events
        };

        var ttl = TimeSpan.FromMinutes(config.RefreshIntervalMinutes > 0 ? config.RefreshIntervalMinutes : 5);
        await _cache.SetAsync(cacheKey, payload, ttl, cancellationToken);

        config.LastSync = DateTime.UtcNow;
        config.Status = events.Count > 0 ? "healthy" : "degraded";
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (events.Count == 0)
        {
            _logger.LogDebug("MT5 calendar provider: no upcoming events in database");
        }

        return payload;
    }

    public async Task<IReadOnlyList<MacroCalendarEventDto>> QueryUpcomingEventsAsync(CancellationToken cancellationToken = default)
    {
        var from = DateTime.UtcNow.AddHours(-2);
        var to = DateTime.UtcNow.AddDays(14);

        return await _db.EconomicEvents
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
    }

    private async Task TryIngestFromMt5ApiAsync(string apiUrl, CancellationToken cancellationToken)
    {
        var url = $"{apiUrl.TrimEnd('/')}/api/calendar";
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(Mt5CalendarMacroProvider));
            client.Timeout = TimeSpan.FromSeconds(20);

            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("MT5 calendar API {Url} returned {StatusCode}", url, (int)response.StatusCode);
                return;
            }

            var remoteEvents = await response.Content.ReadFromJsonAsync<List<Mt5RemoteCalendarEvent>>(cancellationToken);
            if (remoteEvents is null || remoteEvents.Count == 0)
                return;

            var upserted = 0;
            foreach (var item in remoteEvents)
            {
                if (string.IsNullOrWhiteSpace(item.EventName) || item.EventTime == default)
                    continue;

                var eventTime = item.EventTime.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(item.EventTime, DateTimeKind.Utc)
                    : item.EventTime.ToUniversalTime();

                var impact = ParseImpact(item.Impact);
                var existing = await _db.EconomicEvents.FirstOrDefaultAsync(
                    e => e.EventName == item.EventName && e.EventTime == eventTime,
                    cancellationToken);

                if (existing is null)
                {
                    _db.EconomicEvents.Add(new EconomicEvent
                    {
                        Id = Guid.NewGuid(),
                        EventName = item.EventName,
                        Country = item.Country ?? string.Empty,
                        Currency = item.Currency ?? string.Empty,
                        Impact = impact,
                        EventTime = eventTime,
                        Actual = item.Actual,
                        Forecast = item.Forecast,
                        Previous = item.Previous,
                        BlockBeforeMinutes = impact == EventImpact.HIGH ? 30 : 15,
                        BlockAfterMinutes = impact == EventImpact.HIGH ? 15 : 10,
                        CreatedAt = DateTime.UtcNow
                    });
                    upserted++;
                }
                else
                {
                    existing.Actual = item.Actual ?? existing.Actual;
                    existing.Forecast = item.Forecast ?? existing.Forecast;
                    existing.Previous = item.Previous ?? existing.Previous;
                    if (existing.Impact != impact)
                        existing.Impact = impact;
                }
            }

            if (upserted > 0)
                await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("MT5 calendar ingest from {Url}: {Count} new event(s)", url, upserted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "MT5 calendar ingest failed for {Url}", url);
        }
    }

    private static EventImpact ParseImpact(string? impact) => impact?.ToUpperInvariant() switch
    {
        "HIGH" => EventImpact.HIGH,
        "MEDIUM" => EventImpact.MEDIUM,
        _ => EventImpact.LOW
    };

    private sealed class Mt5RemoteCalendarEvent
    {
        [JsonPropertyName("event_name")]
        public string EventName { get; set; } = string.Empty;

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("impact")]
        public string? Impact { get; set; }

        [JsonPropertyName("event_time")]
        public DateTime EventTime { get; set; }

        [JsonPropertyName("actual")]
        public string? Actual { get; set; }

        [JsonPropertyName("forecast")]
        public string? Forecast { get; set; }

        [JsonPropertyName("previous")]
        public string? Previous { get; set; }
    }
}
