using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.MarketData.Configuration;

namespace NtBot.MarketData.Clients;

public interface IMarketDataApiClient
{
    Task<MarketDataPriceSnapshot?> GetPriceAsync(string ticker, CancellationToken ct = default);
    Task<MarketDataCandleResponse?> GetCandlesAsync(
        string ticker,
        int timeframeMinutes,
        int count,
        DateTime? date = null,
        CancellationToken ct = default);
}

public sealed class MarketDataApiClient : IMarketDataApiClient
{
    private readonly HttpClient _http;
    private readonly MarketDataApiOptions _options;
    private readonly ILogger<MarketDataApiClient> _logger;

    public MarketDataApiClient(
        HttpClient http,
        IOptions<MarketDataApiOptions> options,
        ILogger<MarketDataApiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MarketDataPriceSnapshot?> GetPriceAsync(string ticker, CancellationToken ct = default)
    {
        var profitTicker = ResolveProfitTicker(ticker);
        var url = $"{_options.BaseUrl.TrimEnd('/')}/api/market/{Uri.EscapeDataString(profitTicker)}";

        try
        {
            return await _http.GetFromJsonAsync<MarketDataPriceSnapshot>(url, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout do HttpClient (não do caller) — trata como indisponível.
            _logger.LogDebug("MarketData.API timeout para {Ticker}", profitTicker);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "MarketData.API indisponível para {Ticker}", profitTicker);
            return null;
        }
    }

    public async Task<MarketDataCandleResponse?> GetCandlesAsync(
        string ticker,
        int timeframeMinutes,
        int count,
        DateTime? date = null,
        CancellationToken ct = default)
    {
        var profitTicker = ResolveProfitTicker(ticker);
        var datePart = date.HasValue ? $"&date={date.Value:yyyy-MM-dd}" : string.Empty;
        var url =
            $"{_options.BaseUrl.TrimEnd('/')}/api/candles/{Uri.EscapeDataString(profitTicker)}?timeframe={timeframeMinutes}&count={count}{datePart}";

        try
        {
            return await _http.GetFromJsonAsync<MarketDataCandleResponse>(url, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogDebug("Candles ProfitDLL timeout para {Ticker}", profitTicker);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Candles ProfitDLL indisponíveis para {Ticker}", profitTicker);
            return null;
        }
    }

    internal static string ResolveProfitTicker(string ticker)
    {
        var t = ticker.Trim().ToUpperInvariant();
        return t switch
        {
            "WIN" => "WINFUT",
            "WDO" => "WDOFUT",
            "DOL" => "DOLFUT",
            "IND" => "INDFUT",
            _ => t
        };
    }
}

public sealed class MarketDataPriceSnapshot
{
    [JsonPropertyName("Ticker")]
    public string Ticker { get; set; } = string.Empty;

    [JsonPropertyName("LastPrice")]
    public double LastPrice { get; set; }

    [JsonPropertyName("Bid")]
    public double Bid { get; set; }

    [JsonPropertyName("Ask")]
    public double Ask { get; set; }

    [JsonPropertyName("Timestamp")]
    public DateTime Timestamp { get; set; }
}

public sealed class MarketDataCandleResponse
{
    [JsonPropertyName("Ticker")]
    public string Ticker { get; set; } = string.Empty;

    [JsonPropertyName("TimeframeMinutes")]
    public int TimeframeMinutes { get; set; }

    [JsonPropertyName("Source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("Candles")]
    public List<MarketDataCandleRow> Candles { get; set; } = [];
}

public sealed class MarketDataCandleRow
{
    [JsonPropertyName("Time")]
    public DateTime Time { get; set; }

    [JsonPropertyName("Open")]
    public decimal Open { get; set; }

    [JsonPropertyName("High")]
    public decimal High { get; set; }

    [JsonPropertyName("Low")]
    public decimal Low { get; set; }

    [JsonPropertyName("Close")]
    public decimal Close { get; set; }

    [JsonPropertyName("Volume")]
    public long Volume { get; set; }
}
