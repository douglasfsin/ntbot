using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;
using NtBot.Macro.Cache;
using NtBot.Macro.Configuration;
using NtBot.Macro.Providers.Fred;

namespace NtBot.Macro.Services;

public interface IEconomicCalendarSyncService
{
    Task<int> SyncUpcomingEventsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Syncs FRED release dates into <see cref="EconomicEvent"/> for the MT5 calendar provider.
/// </summary>
public sealed class FredEconomicCalendarSyncService : IEconomicCalendarSyncService
{
    internal const int SyncHorizonDays = 14;

    private static readonly HashSet<string> HighImpactKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Employment Situation",
        "Consumer Price Index",
        "Producer Price Index",
        "FOMC",
        "Gross Domestic Product",
        "Personal Income and Outlays",
        "Retail Sales",
        "Industrial Production"
    };

    private readonly HttpClient _http;
    private readonly IFredApiKeyResolver _apiKeyResolver;
    private readonly IMacroCacheService _cache;
    private readonly IOptions<MacroOptions> _options;
    private readonly NtBotDbContext _db;
    private readonly ILogger<FredEconomicCalendarSyncService> _logger;

    public FredEconomicCalendarSyncService(
        HttpClient http,
        IFredApiKeyResolver apiKeyResolver,
        IMacroCacheService cache,
        IOptions<MacroOptions> options,
        NtBotDbContext db,
        ILogger<FredEconomicCalendarSyncService> logger)
    {
        _http = http;
        _apiKeyResolver = apiKeyResolver;
        _cache = cache;
        _options = options;
        _db = db;
        _logger = logger;
    }

    public async Task<int> SyncUpcomingEventsAsync(CancellationToken cancellationToken = default)
    {
        var calendarConfig = await _db.MacroProviders
            .FirstOrDefaultAsync(p => p.Name == MacroProviderNames.Mt5Calendar, cancellationToken);

        if (calendarConfig is null || !calendarConfig.Enabled)
            return 0;

        var apiKey = await _apiKeyResolver.GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogDebug("Skipping FRED calendar sync — API key not configured");
            return 0;
        }

        var from = DateOnly.FromDateTime(DateTime.UtcNow);
        var to = from.AddDays(SyncHorizonDays);
        var releaseDates = await FetchReleaseDatesAsync(apiKey, from, to, cancellationToken);
        if (releaseDates.Count == 0)
            return 0;

        var releaseNames = await FetchReleaseNamesAsync(apiKey, releaseDates.Keys, cancellationToken);
        var upserted = 0;

        foreach (var (releaseId, dates) in releaseDates)
        {
            if (!releaseNames.TryGetValue(releaseId, out var releaseName))
                continue;

            foreach (var date in dates)
            {
                var eventTime = date.ToDateTime(new TimeOnly(13, 30), DateTimeKind.Utc);
                var impact = ClassifyImpact(releaseName);
                var existing = await _db.EconomicEvents.FirstOrDefaultAsync(
                    e => e.EventName == releaseName && e.EventTime == eventTime,
                    cancellationToken);

                if (existing is null)
                {
                    _db.EconomicEvents.Add(new EconomicEvent
                    {
                        Id = Guid.NewGuid(),
                        EventName = releaseName,
                        Country = "United States",
                        Currency = "USD",
                        Impact = impact,
                        EventTime = eventTime,
                        BlockBeforeMinutes = impact == EventImpact.HIGH ? 30 : 15,
                        BlockAfterMinutes = impact == EventImpact.HIGH ? 15 : 10,
                        CreatedAt = DateTime.UtcNow
                    });
                    upserted++;
                }
                else if (existing.Impact != impact)
                {
                    existing.Impact = impact;
                    upserted++;
                }
            }
        }

        if (upserted > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            await _cache.RemoveAsync($"macro:provider:{MacroProviderNames.Mt5Calendar}", cancellationToken);
            await _cache.RemoveAsync("macro:snapshot:all", cancellationToken);
        }

        calendarConfig.LastSync = DateTime.UtcNow;
        calendarConfig.Status = upserted > 0 ? "healthy" : calendarConfig.Status;
        calendarConfig.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("FRED calendar sync completed — {Count} event(s) upserted", upserted);
        return upserted;
    }

    private async Task<Dictionary<int, HashSet<DateOnly>>> FetchReleaseDatesAsync(
        string apiKey,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var baseUrl = _options.Value.FredBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/releases/dates?{BuildQuery(apiKey, from, to)}";

        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("FRED releases/dates HTTP {StatusCode}", (int)response.StatusCode);
                return [];
            }

            var payload = await response.Content.ReadFromJsonAsync<FredReleaseDatesResponse>(cancellationToken);
            var grouped = new Dictionary<int, HashSet<DateOnly>>();

            foreach (var item in payload?.ReleaseDates ?? [])
            {
                if (!int.TryParse(item.ReleaseId, out var releaseId))
                    continue;
                if (!DateOnly.TryParse(item.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    continue;

                if (!grouped.TryGetValue(releaseId, out var dates))
                {
                    dates = [];
                    grouped[releaseId] = dates;
                }

                dates.Add(date);
            }

            return grouped;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "FRED releases/dates fetch failed");
            return [];
        }
    }

    private async Task<Dictionary<int, string>> FetchReleaseNamesAsync(
        string apiKey,
        IEnumerable<int> releaseIds,
        CancellationToken cancellationToken)
    {
        var baseUrl = _options.Value.FredBaseUrl.TrimEnd('/');
        var names = new Dictionary<int, string>();

        foreach (var releaseId in releaseIds.Distinct())
        {
            var url = $"{baseUrl}/release?release_id={releaseId}&{BuildQuery(apiKey)}";
            try
            {
                using var response = await _http.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    continue;

                var payload = await response.Content.ReadFromJsonAsync<FredReleaseResponse>(cancellationToken);
                if (!string.IsNullOrWhiteSpace(payload?.Release?.Name))
                    names[releaseId] = payload.Release.Name;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "FRED release lookup failed for {ReleaseId}", releaseId);
            }
        }

        return names;
    }

    private static string BuildQuery(string apiKey, DateOnly? from = null, DateOnly? to = null)
    {
        var parts = new List<string>
        {
            $"api_key={Uri.EscapeDataString(apiKey)}",
            "file_type=json"
        };

        if (from is not null)
            parts.Add($"realtime_start={from:yyyy-MM-dd}");
        if (to is not null)
            parts.Add($"realtime_end={to:yyyy-MM-dd}");

        parts.Add("include_release_dates_with_no_data=true");
        return string.Join("&", parts);
    }

    internal static EventImpact ClassifyImpact(string releaseName)
    {
        foreach (var keyword in HighImpactKeywords)
        {
            if (releaseName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return EventImpact.HIGH;
        }

        return releaseName.Contains("Unemployment", StringComparison.OrdinalIgnoreCase)
            || releaseName.Contains("Housing", StringComparison.OrdinalIgnoreCase)
            ? EventImpact.MEDIUM
            : EventImpact.LOW;
    }

    private sealed class FredReleaseDatesResponse
    {
        [JsonPropertyName("release_dates")]
        public List<FredReleaseDateDto>? ReleaseDates { get; set; }
    }

    private sealed class FredReleaseDateDto
    {
        [JsonPropertyName("release_id")]
        public string ReleaseId { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;
    }

    private sealed class FredReleaseResponse
    {
        [JsonPropertyName("release")]
        public FredReleaseDto? Release { get; set; }
    }

    private sealed class FredReleaseDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}

public sealed class EconomicCalendarSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EconomicCalendarSyncWorker> _logger;

    public EconomicCalendarSyncWorker(IServiceScopeFactory scopeFactory, ILogger<EconomicCalendarSyncWorker> logger)
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
                var sync = scope.ServiceProvider.GetRequiredService<IEconomicCalendarSyncService>();
                await sync.SyncUpcomingEventsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Economic calendar sync failed");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
