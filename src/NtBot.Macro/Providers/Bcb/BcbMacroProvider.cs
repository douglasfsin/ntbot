using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NtBot.Infrastructure.Persistence;
using NtBot.Macro.Cache;
using NtBot.Macro.Configuration;

namespace NtBot.Macro.Providers;

/// <summary>
/// Banco Central do Brasil — Selic e IPCA via API SGS.
/// </summary>
public sealed class BcbMacroProvider : IMacroProvider
{
    private static readonly (int SeriesId, string Label, string SeriesKey)[] Series =
    [
        (432, "Selic Meta", "BCB_SELIC"),
        (433, "IPCA Mensal", "BCB_IPCA")
    ];

    private readonly HttpClient _http;
    private readonly IMacroCacheService _cache;
    private readonly NtBotDbContext _db;
    private readonly ILogger<BcbMacroProvider> _logger;

    public BcbMacroProvider(
        HttpClient http,
        IMacroCacheService cache,
        NtBotDbContext db,
        ILogger<BcbMacroProvider> logger)
    {
        _http = http;
        _cache = cache;
        _db = db;
        _logger = logger;
    }

    public string Name => MacroProviderNames.CentralBank;
    public int Priority => 3;
    public IReadOnlyList<string> Capabilities { get; } = ["rates", "policy", "inflation"];

    public async Task<MacroProviderRuntimeInfo> GetRuntimeInfoAsync(CancellationToken cancellationToken = default)
    {
        var config = await _db.MacroProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Name == Name, cancellationToken);
        var enabled = config?.Enabled ?? false;
        return new MacroProviderRuntimeInfo
        {
            Name = Name,
            Enabled = enabled,
            Priority = config?.Priority ?? Priority,
            HealthStatus = enabled
                ? config?.LastSync is null ? MacroProviderHealth.Degraded : MacroProviderHealth.Healthy
                : MacroProviderHealth.Disabled,
            LastUpdate = config?.LastSync,
            Capabilities = Capabilities
        };
    }

    public async Task<MacroProviderPayload?> FetchAsync(CancellationToken cancellationToken = default)
    {
        var config = await _db.MacroProviders.FirstOrDefaultAsync(p => p.Name == Name, cancellationToken);
        if (config is null || !config.Enabled) return null;

        var cacheKey = $"macro:provider:{Name}";
        var cached = await _cache.GetAsync<MacroProviderPayload>(cacheKey, cancellationToken);
        if (cached is not null) return cached;

        var indicators = new List<MacroIndicatorValue>();
        foreach (var (seriesId, label, key) in Series)
        {
            var reading = await FetchSeriesAsync(seriesId, cancellationToken);
            if (reading is null) continue;

            indicators.Add(new MacroIndicatorValue
            {
                SeriesId = key,
                Label = label,
                Value = reading.Value.Value,
                ObservedAt = reading.Value.Date,
                Unit = "%"
            });
        }

        if (indicators.Count == 0)
        {
            _logger.LogWarning("BCB provider returned no indicators");
            return null;
        }

        var payload = new MacroProviderPayload
        {
            ProviderName = Name,
            FetchedAt = DateTime.UtcNow,
            Indicators = indicators
        };

        var ttl = TimeSpan.FromMinutes(config.RefreshIntervalMinutes > 0 ? config.RefreshIntervalMinutes : 60);
        await _cache.SetAsync(cacheKey, payload, ttl, cancellationToken);

        config.LastSync = DateTime.UtcNow;
        config.Status = "healthy";
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return payload;
    }

    private async Task<(decimal Value, DateTime? Date)?> FetchSeriesAsync(int seriesId, CancellationToken cancellationToken)
    {
        var url = $"https://api.bcb.gov.br/dados/serie/bcdata.sgs.{seriesId}/dados/ultimos/1?formato=json";
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var rows = await response.Content.ReadFromJsonAsync<List<BcbDataPoint>>(cancellationToken);
            var row = rows?.FirstOrDefault();
            if (row is null) return null;

            if (!decimal.TryParse(row.Valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                return null;

            DateTime? date = DateTime.TryParse(row.Data, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;

            return (value, date);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "BCB fetch failed for series {SeriesId}", seriesId);
            return null;
        }
    }

    private sealed class BcbDataPoint
    {
        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;

        [JsonPropertyName("valor")]
        public string Valor { get; set; } = string.Empty;
    }
}
