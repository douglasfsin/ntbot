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
/// Yahoo Finance — cotações de DXY, petróleo e índices via chart API pública.
/// </summary>
public sealed class YahooFinanceMacroProvider : IMacroProvider
{
    private static readonly (string YahooSymbol, string Label, string SeriesKey)[] Quotes =
    [
        ("DX-Y.NYB", "US Dollar Index", "YAHOO_DXY"),
        ("CL=F", "WTI Crude Oil", "YAHOO_OIL"),
        ("^GSPC", "S&P 500", "YAHOO_SPX")
    ];

    private readonly HttpClient _http;
    private readonly IMacroCacheService _cache;
    private readonly NtBotDbContext _db;
    private readonly ILogger<YahooFinanceMacroProvider> _logger;

    public YahooFinanceMacroProvider(
        HttpClient http,
        IMacroCacheService cache,
        NtBotDbContext db,
        ILogger<YahooFinanceMacroProvider> logger)
    {
        _http = http;
        _cache = cache;
        _db = db;
        _logger = logger;
    }

    public string Name => MacroProviderNames.YahooFinance;
    public int Priority => 4;
    public IReadOnlyList<string> Capabilities { get; } = ["fx", "equities", "commodities"];

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
        foreach (var (yahooSymbol, label, key) in Quotes)
        {
            var quote = await FetchQuoteAsync(yahooSymbol, cancellationToken);
            if (quote is null) continue;

            indicators.Add(new MacroIndicatorValue
            {
                SeriesId = key,
                Label = label,
                Value = quote.Value.Value,
                ObservedAt = quote.Value.Date,
                Unit = "index"
            });
        }

        if (indicators.Count == 0)
        {
            _logger.LogWarning("Yahoo Finance provider returned no indicators");
            return null;
        }

        var payload = new MacroProviderPayload
        {
            ProviderName = Name,
            FetchedAt = DateTime.UtcNow,
            Indicators = indicators
        };

        var ttl = TimeSpan.FromMinutes(config.RefreshIntervalMinutes > 0 ? config.RefreshIntervalMinutes : 15);
        await _cache.SetAsync(cacheKey, payload, ttl, cancellationToken);

        config.LastSync = DateTime.UtcNow;
        config.Status = "healthy";
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return payload;
    }

    private async Task<(decimal Value, DateTime? Date)?> FetchQuoteAsync(string symbol, CancellationToken cancellationToken)
    {
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=5d";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "NtBot/1.0");

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var payload = await response.Content.ReadFromJsonAsync<YahooChartResponse>(cancellationToken);
            var result = payload?.Chart?.Result?.FirstOrDefault();
            var closes = result?.Indicators?.Quote?.FirstOrDefault()?.Close;
            var timestamps = result?.Timestamp;

            if (closes is null || closes.Count == 0)
                return null;

            var lastIndex = closes.FindLastIndex(c => c.HasValue);
            if (lastIndex < 0) return null;

            var value = closes[lastIndex]!.Value;
            DateTime? date = timestamps is not null && lastIndex < timestamps.Count
                ? DateTimeOffset.FromUnixTimeSeconds(timestamps[lastIndex]).UtcDateTime
                : null;

            return ((decimal)value, date);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Yahoo fetch failed for {Symbol}", symbol);
            return null;
        }
    }

    private sealed class YahooChartResponse
    {
        [JsonPropertyName("chart")]
        public YahooChart? Chart { get; set; }
    }

    private sealed class YahooChart
    {
        [JsonPropertyName("result")]
        public List<YahooChartResult>? Result { get; set; }
    }

    private sealed class YahooChartResult
    {
        [JsonPropertyName("timestamp")]
        public List<long>? Timestamp { get; set; }

        [JsonPropertyName("indicators")]
        public YahooIndicators? Indicators { get; set; }
    }

    private sealed class YahooIndicators
    {
        [JsonPropertyName("quote")]
        public List<YahooQuote>? Quote { get; set; }
    }

    private sealed class YahooQuote
    {
        [JsonPropertyName("close")]
        public List<double?>? Close { get; set; }
    }
}
