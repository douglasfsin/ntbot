using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NtBot.Api.Configuration;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Api.Services.MarketData;

public sealed class MarketCandleService : IMarketCandleService
{
    private readonly NtBotDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly QuantOptions _options;
    private readonly ILogger<MarketCandleService> _logger;

    public MarketCandleService(
        NtBotDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<QuantOptions> options,
        ILogger<MarketCandleService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CandleFetchResult> GetCandlesAsync(
        string symbol,
        int count = 100,
        string? timeframe = null,
        CancellationToken cancellationToken = default)
    {
        var storageSymbol = NormalizeSymbol(symbol);
        var tf = NormalizeTimeframe(timeframe ?? _options.DefaultTimeframe);
        var minimum = Math.Min(count, _options.MinCandles);

        var dbCandles = await LoadFromDatabaseAsync(storageSymbol, tf, count, cancellationToken);
        if (IsFresh(dbCandles) && dbCandles.Count >= minimum)
        {
            return new CandleFetchResult { Candles = dbCandles, Source = "database" };
        }

        if (!string.IsNullOrWhiteSpace(_options.Mt5ApiUrl))
        {
            var mt5Symbol = ResolveMt5Symbol(storageSymbol);
            var fetched = await FetchFromMt5Async(mt5Symbol, storageSymbol, tf, count, cancellationToken);
            if (fetched.Count >= minimum)
            {
                await UpsertCandlesAsync(fetched, cancellationToken);
                return new CandleFetchResult { Candles = fetched, Source = "mt5" };
            }

            if (fetched.Count > 0)
            {
                await UpsertCandlesAsync(fetched, cancellationToken);
                return new CandleFetchResult { Candles = fetched, Source = "mt5-partial" };
            }
        }

        if (dbCandles.Count > 0)
            return new CandleFetchResult { Candles = dbCandles, Source = "database-stale" };

        _logger.LogWarning(
            "Candles indisponíveis para {Symbol} ({Timeframe}). Configure Quant:Mt5ApiUrl ou o sync OHLCV do connector.",
            storageSymbol,
            tf);

        return new CandleFetchResult();
    }

    public async Task<int> UpsertCandlesAsync(IEnumerable<Candle> candles, CancellationToken cancellationToken = default)
    {
        var batch = candles
            .Where(c => !string.IsNullOrWhiteSpace(c.Symbol) && c.OpenTime != default)
            .ToList();

        if (batch.Count == 0)
            return 0;

        var upserted = 0;
        foreach (var candle in batch)
        {
            candle.Symbol = NormalizeSymbol(candle.Symbol);
            candle.Timeframe = NormalizeTimeframe(candle.Timeframe);
            if (candle.CloseTime == default)
                candle.CloseTime = candle.OpenTime;

            var existing = await _db.Candles.FirstOrDefaultAsync(
                c => c.Symbol == candle.Symbol
                     && c.Timeframe == candle.Timeframe
                     && c.OpenTime == candle.OpenTime,
                cancellationToken);

            if (existing is null)
            {
                candle.Id = candle.Id == Guid.Empty ? Guid.NewGuid() : candle.Id;
                candle.CreatedAt = DateTime.UtcNow;
                _db.Candles.Add(candle);
                upserted++;
            }
            else
            {
                existing.Open = candle.Open;
                existing.High = candle.High;
                existing.Low = candle.Low;
                existing.Close = candle.Close;
                existing.Volume = candle.Volume;
                existing.CloseTime = candle.CloseTime;
            }
        }

        if (upserted > 0 || _db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(cancellationToken);

        return upserted;
    }

    public async Task<IReadOnlyList<string>> GetAvailableSymbolsAsync(
        int minimum = 50,
        string? timeframe = null,
        CancellationToken cancellationToken = default)
    {
        var tf = NormalizeTimeframe(timeframe ?? _options.DefaultTimeframe);

        return await _db.Candles
            .AsNoTracking()
            .Where(c => c.Timeframe == tf)
            .GroupBy(c => c.Symbol)
            .Where(g => g.Count() >= minimum)
            .Select(g => g.Key)
            .OrderBy(s => s)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Candle>> LoadFromDatabaseAsync(
        string symbol,
        string timeframe,
        int count,
        CancellationToken cancellationToken)
    {
        return await _db.Candles
            .AsNoTracking()
            .Where(c => c.Symbol == symbol && c.Timeframe == timeframe)
            .OrderByDescending(c => c.OpenTime)
            .Take(count)
            .OrderBy(c => c.OpenTime)
            .ToListAsync(cancellationToken);
    }

    private bool IsFresh(IReadOnlyList<Candle> candles)
    {
        if (candles.Count == 0)
            return false;

        var latest = candles.Max(c => c.OpenTime);
        return DateTime.UtcNow - latest.ToUniversalTime() <= TimeSpan.FromMinutes(_options.DatabaseMaxAgeMinutes);
    }

    private async Task<List<Candle>> FetchFromMt5Async(
        string mt5Symbol,
        string storageSymbol,
        string timeframe,
        int count,
        CancellationToken cancellationToken)
    {
        var baseUrl = _options.Mt5ApiUrl!.TrimEnd('/');
        var url =
            $"{baseUrl}/api/ohlcv/{Uri.EscapeDataString(mt5Symbol)}?timeframe={Uri.EscapeDataString(timeframe)}&count={count}";

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(MarketCandleService));
            client.Timeout = TimeSpan.FromSeconds(25);

            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("MT5 OHLCV {Url} → HTTP {Status}", url, (int)response.StatusCode);
                return [];
            }

            var payload = await response.Content.ReadFromJsonAsync<Mt5OhlcvResponse>(cancellationToken);
            if (payload?.Candles is null || payload.Candles.Count == 0)
                return [];

            return payload.Candles
                .Select(row => MapMt5Candle(storageSymbol, timeframe, row))
                .Where(c => c is not null)
                .Cast<Candle>()
                .OrderBy(c => c.OpenTime)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Falha ao buscar OHLCV MT5 para {Symbol} via {Mt5Symbol}", storageSymbol, mt5Symbol);
            return [];
        }
    }

    private static Candle? MapMt5Candle(string storageSymbol, string timeframe, Mt5OhlcvRow row)
    {
        if (!DateTime.TryParse(row.Time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var openTime))
            return null;

        openTime = openTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(openTime, DateTimeKind.Utc)
            : openTime.ToUniversalTime();

        return new Candle
        {
            Id = Guid.NewGuid(),
            Symbol = storageSymbol,
            Timeframe = timeframe,
            OpenTime = openTime,
            CloseTime = openTime,
            Open = row.Open,
            High = row.High,
            Low = row.Low,
            Close = row.Close,
            Volume = row.RealVolume > 0 ? row.RealVolume : row.TickVolume,
            CreatedAt = DateTime.UtcNow
        };
    }

    private string ResolveMt5Symbol(string symbol)
    {
        if (_options.SymbolMap.TryGetValue(symbol, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
            return mapped.Trim().ToUpperInvariant();

        return symbol.ToUpperInvariant();
    }

    internal static string NormalizeSymbol(string symbol) =>
        string.IsNullOrWhiteSpace(symbol) ? string.Empty : symbol.Trim().ToUpperInvariant();

    internal static string NormalizeTimeframe(string timeframe)
    {
        if (string.IsNullOrWhiteSpace(timeframe))
            return "M5";

        var tf = timeframe.Trim().ToUpperInvariant();
        return tf switch
        {
            "1M" or "M1" => "M1",
            "5M" or "M5" => "M5",
            "15M" or "M15" => "M15",
            "30M" or "M30" => "M30",
            "1H" or "H1" => "H1",
            "4H" or "H4" => "H4",
            "1D" or "D1" => "D1",
            _ => tf
        };
    }

    private sealed class Mt5OhlcvResponse
    {
        [JsonPropertyName("candles")]
        public List<Mt5OhlcvRow> Candles { get; set; } = [];
    }

    private sealed class Mt5OhlcvRow
    {
        [JsonPropertyName("time")]
        public string Time { get; set; } = string.Empty;

        [JsonPropertyName("open")]
        public decimal Open { get; set; }

        [JsonPropertyName("high")]
        public decimal High { get; set; }

        [JsonPropertyName("low")]
        public decimal Low { get; set; }

        [JsonPropertyName("close")]
        public decimal Close { get; set; }

        [JsonPropertyName("tick_volume")]
        public long TickVolume { get; set; }

        [JsonPropertyName("real_volume")]
        public long RealVolume { get; set; }
    }
}
