using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.Macro.Configuration;

namespace NtBot.Macro.Providers.Fred;

/// <summary>
/// Client for the FRED API v1 (<see href="https://fred.stlouisfed.org/docs/api/fred/"/>).
/// Uses <c>fred/series/observations</c> to fetch the latest data point per series.
/// </summary>
public sealed class FredApiClient
{
    /// <summary>Default lookback when <c>observation_start</c> is not specified.</summary>
    internal const int DefaultObservationLookbackYears = 5;

    /// <summary>
    /// Number of recent observations to request (descending) so missing values ("." ) can be skipped.
    /// FRED allows 1–100000; see series/observations <c>limit</c> parameter.
    /// </summary>
    internal const int LatestObservationFetchLimit = 20;

    private readonly HttpClient _http;
    private readonly MacroOptions _options;
    private readonly ILogger<FredApiClient> _logger;

    public FredApiClient(HttpClient http, IOptions<MacroOptions> options, ILogger<FredApiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FredObservation?> GetLatestObservationAsync(
        string seriesId,
        string? apiKeyOverride = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = ResolveApiKey(apiKeyOverride);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning(
                "FRED API key not configured (set Macro:FredApiKey, Macro__FredApiKey, or provider ApiKey in Settings)");
            return null;
        }

        if (string.IsNullOrWhiteSpace(seriesId))
        {
            _logger.LogWarning("FRED series_id is required");
            return null;
        }

        var url = BuildSeriesObservationsUrl(_options, seriesId, apiKey);

        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var fredError = TryParseFredError(body);
                _logger.LogError(
                    "FRED HTTP {StatusCode} for {SeriesId}: {Message}",
                    (int)response.StatusCode,
                    seriesId,
                    fredError ?? body);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<FredObservationsResponse>(cancellationToken);
            if (payload?.ErrorCode is not null)
            {
                _logger.LogError(
                    "FRED error {Code} for {SeriesId}: {Message}",
                    payload.ErrorCode,
                    seriesId,
                    payload.ErrorMessage);
                return null;
            }

            var obs = payload?.Observations?
                .FirstOrDefault(o => !IsMissingValue(o.Value));
            if (obs is null || !TryParseObservationValue(obs.Value, out var value))
            {
                return null;
            }

            DateTime? date = DateTime.TryParse(obs.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;
            return new FredObservation(seriesId, value, date);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FRED fetch failed for {SeriesId}", seriesId);
            return null;
        }
    }

    /// <summary>
    /// Builds the FRED <c>series/observations</c> URL per official docs.
    /// Required: <c>series_id</c>, <c>api_key</c>. Optional: <c>file_type=json</c>,
    /// <c>sort_order=desc</c>, <c>limit</c>, <c>observation_start</c>.
    /// </summary>
    internal string? ResolveApiKey(string? apiKeyOverride)
    {
        if (!string.IsNullOrWhiteSpace(apiKeyOverride))
            return apiKeyOverride.Trim();

        return string.IsNullOrWhiteSpace(_options.FredApiKey) ? null : _options.FredApiKey.Trim();
    }

    internal static string BuildSeriesObservationsUrl(
        MacroOptions options,
        string seriesId,
        string apiKey,
        DateOnly? observationStart = null)
    {
        var start = observationStart ?? DateOnly.FromDateTime(
            DateTime.UtcNow.AddYears(-DefaultObservationLookbackYears));

        var baseUrl = options.FredBaseUrl.TrimEnd('/');
        var query = string.Join("&",
            $"series_id={Uri.EscapeDataString(seriesId)}",
            $"api_key={Uri.EscapeDataString(apiKey)}",
            "file_type=json",
            "sort_order=desc",
            $"limit={LatestObservationFetchLimit}",
            $"observation_start={start:yyyy-MM-dd}");

        return $"{baseUrl}/series/observations?{query}";
    }

    internal static bool IsMissingValue(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == ".";

    internal static bool TryParseObservationValue(string? value, out decimal parsed)
    {
        parsed = default;
        return !IsMissingValue(value) &&
               decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed);
    }

    private static string? TryParseFredError(string body)
    {
        try
        {
            var err = System.Text.Json.JsonSerializer.Deserialize<FredObservationsResponse>(body);
            return err?.ErrorCode is not null ? err.ErrorMessage : null;
        }
        catch
        {
            return null;
        }
    }
}

public sealed record FredObservation(string SeriesId, decimal Value, DateTime? Date);

internal sealed class FredObservationsResponse
{
    [JsonPropertyName("observations")]
    public List<FredObservationDto>? Observations { get; set; }

    [JsonPropertyName("error_code")]
    public int? ErrorCode { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

internal sealed class FredObservationDto
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public static class FredSeries
{
    public const string FedFunds = "FEDFUNDS";
    public const string Us10Y = "DGS10";
    public const string Us2Y = "DGS2";
    public const string Vix = "VIXCLS";
    public const string Unemployment = "UNRATE";
    public const string Cpi = "CPIAUCSL";
    public const string Pce = "PCEPI";
    public const string Payems = "PAYEMS";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [FedFunds] = "Fed Funds Rate",
        [Us10Y] = "US 10Y Treasury",
        [Us2Y] = "US 2Y Treasury",
        [Vix] = "VIX",
        [Unemployment] = "Unemployment Rate",
        [Cpi] = "CPI",
        [Pce] = "PCE Price Index",
        [Payems] = "Nonfarm Payrolls"
    };
}
