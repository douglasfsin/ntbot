using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.MarketIntelligence.Configuration;
using NtBot.MarketIntelligence.Models;

namespace NtBot.MarketIntelligence.Providers.Yahoo;

/// <summary>
/// Cliente HTTP compartilhado para Yahoo Finance Chart API.
/// </summary>
public sealed class YahooFinanceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<YahooFinanceClient> _logger;

    public YahooFinanceClient(HttpClient http, ILogger<YahooFinanceClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<YahooChartPayload?> GetChartAsync(
        string symbol,
        string interval,
        string range,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval={interval}&range={range}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "NtBot/1.0");

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Yahoo chart {Symbol} HTTP {Status}", symbol, (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<YahooChartResponse>(cancellationToken);
            var result = payload?.Chart?.Result?.FirstOrDefault();
            if (result is null)
                return null;

            var quote = result.Indicators?.Quote?.FirstOrDefault();
            var meta = result.Meta;
            if (quote?.Close is null || result.Timestamp is null)
                return null;

            var points = new List<PriceHistoryPoint>();
            for (var i = 0; i < result.Timestamp.Count; i++)
            {
                if (i >= quote.Close.Count || !quote.Close[i].HasValue)
                    continue;

                points.Add(new PriceHistoryPoint
                {
                    Date = DateTimeOffset.FromUnixTimeSeconds(result.Timestamp[i]).UtcDateTime,
                    Close = (decimal)quote.Close[i]!.Value
                });
            }

            if (points.Count == 0)
                return null;

            var last = points[^1];
            var prev = points.Count > 1 ? points[^2] : last;
            var change = last.Close - prev.Close;
            var changePct = prev.Close != 0 ? change / prev.Close * 100m : 0m;

            var lastIdx = points.Count - 1;
            decimal? open = quote.Open?.ElementAtOrDefault(lastIdx) is double o ? (decimal)o : null;
            decimal? high = quote.High?.ElementAtOrDefault(lastIdx) is double h ? (decimal)h : null;
            decimal? low = quote.Low?.ElementAtOrDefault(lastIdx) is double l ? (decimal)l : null;
            long? volume = quote.Volume?.ElementAtOrDefault(lastIdx);

            return new YahooChartPayload
            {
                Symbol = symbol,
                Points = points,
                Price = last.Close,
                Change = change,
                ChangePercent = changePct,
                Open = open ?? last.Close,
                High = high ?? last.Close,
                Low = low ?? last.Close,
                PreviousClose = prev.Close,
                Volume = volume ?? 0,
                MarketStatus = MapStatus(meta?.MarketState)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Yahoo chart fetch failed for {Symbol}", symbol);
            return null;
        }
    }

    private static MarketStatus MapStatus(string? state) => state?.ToUpperInvariant() switch
    {
        "REGULAR" => MarketStatus.Open,
        "CLOSED" => MarketStatus.Closed,
        "PRE" or "PREPRE" => MarketStatus.PreMarket,
        "POST" or "POSTPOST" => MarketStatus.AfterHours,
        _ => MarketStatus.Unknown
    };

    public sealed class YahooChartPayload
    {
        public string Symbol { get; init; } = string.Empty;
        public IReadOnlyList<PriceHistoryPoint> Points { get; init; } = [];
        public decimal Price { get; init; }
        public decimal Change { get; init; }
        public decimal ChangePercent { get; init; }
        public decimal Open { get; init; }
        public decimal High { get; init; }
        public decimal Low { get; init; }
        public decimal PreviousClose { get; init; }
        public long Volume { get; init; }
        public MarketStatus MarketStatus { get; init; }
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

        [JsonPropertyName("meta")]
        public YahooMeta? Meta { get; set; }
    }

    private sealed class YahooMeta
    {
        [JsonPropertyName("marketState")]
        public string? MarketState { get; set; }
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

        [JsonPropertyName("open")]
        public List<double?>? Open { get; set; }

        [JsonPropertyName("high")]
        public List<double?>? High { get; set; }

        [JsonPropertyName("low")]
        public List<double?>? Low { get; set; }

        [JsonPropertyName("volume")]
        public List<long?>? Volume { get; set; }
    }
}
