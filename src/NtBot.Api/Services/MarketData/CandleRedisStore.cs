using System.Globalization;
using System.Text.Json;
using NtBot.Api.Services.Redis;
using NtBot.Shared.MarketData;
using StackExchange.Redis;

namespace NtBot.Api.Services.MarketData;

public interface ICandleRedisStore
{
    bool IsAvailable { get; }

    Task UpsertM1Async(string symbol, IEnumerable<OhlcvBar> bars, CancellationToken cancellationToken = default);

    Task UpsertTimeframeAsync(
        string symbol,
        string timeframe,
        IEnumerable<OhlcvBar> bars,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OhlcvBar>> GetM1Async(string symbol, int count, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OhlcvBar>> GetTimeframeAsync(
        string symbol,
        string timeframe,
        int count,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OhlcvBar>> GetAggregatedAsync(
        string symbol,
        string timeframe,
        int count,
        CancellationToken cancellationToken = default);

    Task SetLastPriceAsync(string symbol, decimal price, DateTime timestampUtc, CancellationToken cancellationToken = default);
    Task<(decimal Price, DateTime TimestampUtc)?> GetLastPriceAsync(string symbol, CancellationToken cancellationToken = default);
    Task ApplyTickToM1Async(string symbol, decimal price, DateTime timestampUtc, long volume = 0, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists candles in Redis (sorted set by unix open time) and last price for chart updates.
/// M1 is the canonical series; higher TFs are also cached and can be aggregated from M1 on read.
/// </summary>
public sealed class CandleRedisStore : ICandleRedisStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IRedisConnectionAccessor _redis;
    private readonly ILogger<CandleRedisStore> _logger;

    private const int MaxM1Bars = 20_000;
    private const int MaxTfBars = 5_000;
    /// <summary>M1 (and TF) candle keys expire after 24h unless refreshed by upserts.</summary>
    private static readonly TimeSpan CandleTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan PriceTtl = TimeSpan.FromHours(48);
    private static readonly HashSet<int> PreferAggregateFromM1 = [3, 5, 15, 30, 60];

    public CandleRedisStore(IRedisConnectionAccessor redis, ILogger<CandleRedisStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public bool IsAvailable => _redis.GetDatabase() is not null;

    public Task UpsertM1Async(
        string symbol,
        IEnumerable<OhlcvBar> bars,
        CancellationToken cancellationToken = default) =>
        UpsertTimeframeAsync(symbol, "M1", bars, cancellationToken);

    public async Task UpsertTimeframeAsync(
        string symbol,
        string timeframe,
        IEnumerable<OhlcvBar> bars,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        if (db is null)
            return;

        var normalized = CandleSymbolAliases.Canonical(symbol);
        var tf = ChartTimeframe.Normalize(timeframe);
        var key = CandleKey(normalized, tf);
        var interval = TimeSpan.FromMinutes(Math.Max(1, ChartTimeframe.ToMinutes(tf)));
        var entries = new List<SortedSetEntry>();

        foreach (var bar in bars)
        {
            if (bar.OpenTime == default)
                continue;

            var openUtc = EnsureUtc(bar.OpenTime);
            var floored = CandleAggregator.FloorToIntervalUtc(openUtc, interval);
            var score = new DateTimeOffset(floored).ToUnixTimeSeconds();
            var payload = JsonSerializer.Serialize(new RedisM1Bar
            {
                T = score,
                O = bar.Open,
                H = bar.High,
                L = bar.Low,
                C = bar.Close,
                V = bar.Volume
            }, JsonOptions);
            entries.Add(new SortedSetEntry(payload, score));
        }

        if (entries.Count == 0)
            return;

        try
        {
            foreach (var group in entries.GroupBy(e => e.Score))
                await db.SortedSetRemoveRangeByScoreAsync(key, group.Key, group.Key);

            await db.SortedSetAddAsync(key, entries.ToArray());

            var maxBars = tf == "M1" ? MaxM1Bars : MaxTfBars;
            var card = await db.SortedSetLengthAsync(key);
            if (card > maxBars)
            {
                var trim = card - maxBars;
                await db.SortedSetRemoveRangeByRankAsync(key, 0, trim - 1);
            }

            // Sliding 24h TTL per ticker series — refreshed on every upsert.
            await db.KeyExpireAsync(key, CandleTtl);

            _logger.LogDebug(
                "Redis candles upsert {Symbol} {Tf}: {Count} bars key={Key} ttl=24h",
                normalized, tf, entries.Count, key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis candle upsert failed for {Symbol} {Tf}", normalized, tf);
        }
    }

    public Task<IReadOnlyList<OhlcvBar>> GetM1Async(
        string symbol,
        int count,
        CancellationToken cancellationToken = default) =>
        GetTimeframeAsync(symbol, "M1", count, cancellationToken);

    public async Task<IReadOnlyList<OhlcvBar>> GetTimeframeAsync(
        string symbol,
        string timeframe,
        int count,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        if (db is null || count <= 0)
            return [];

        var normalized = CandleSymbolAliases.Canonical(symbol);
        var tf = ChartTimeframe.Normalize(timeframe);
        var key = CandleKey(normalized, tf);

        try
        {
            var values = await db.SortedSetRangeByRankAsync(key, -count, -1, Order.Ascending);
            var bars = values
                .Select(ParseBar)
                .Where(b => b is not null)
                .Cast<OhlcvBar>()
                .OrderBy(b => b.OpenTime)
                .ToList();

            _logger.LogDebug(
                "Redis candles read {Symbol} {Tf}: {Count}/{Requested} key={Key}",
                normalized, tf, bars.Count, count, key);
            return bars;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis candle read failed for {Symbol} {Tf}", normalized, tf);
            return [];
        }
    }

    public async Task<IReadOnlyList<OhlcvBar>> GetAggregatedAsync(
        string symbol,
        string timeframe,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (!ChartTimeframe.CanAggregateFromM1(timeframe))
            return [];

        var minutes = ChartTimeframe.ToMinutes(timeframe);
        if (minutes == 1)
            return await GetM1Async(symbol, count, cancellationToken);

        // Chart TFs 3/5/15/30/60: always build from Redis M1 (canonical series).
        if (PreferAggregateFromM1.Contains(minutes))
        {
            var m1Needed = CandleAggregator.RequiredM1Count(minutes, count);
            var m1 = await GetM1Async(symbol, m1Needed, cancellationToken);
            if (m1.Count < Math.Min(5, count))
                return [];

            return CandleAggregator.FromM1(m1, minutes, count);
        }

        // H4/D1: prefer pre-cached TF when present; else aggregate capped M1.
        var direct = await GetTimeframeAsync(symbol, timeframe, count, cancellationToken);
        if (direct.Count >= Math.Min(5, count))
            return direct;

        var m1ForHigher = await GetM1Async(symbol, CandleAggregator.RequiredM1Count(minutes, count), cancellationToken);
        if (m1ForHigher.Count < Math.Min(5, count))
            return [];

        return CandleAggregator.FromM1(m1ForHigher, minutes, count);
    }

    public async Task SetLastPriceAsync(
        string symbol,
        decimal price,
        DateTime timestampUtc,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        if (db is null || price <= 0)
            return;

        var normalized = CandleSymbolAliases.Canonical(symbol);
        var key = PriceKey(normalized);
        var payload = $"{price.ToString(CultureInfo.InvariantCulture)}|{new DateTimeOffset(EnsureUtc(timestampUtc)).ToUnixTimeSeconds()}";

        try
        {
            await db.StringSetAsync(key, payload, PriceTtl);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis last-price write failed for {Symbol}", normalized);
        }
    }

    public async Task<(decimal Price, DateTime TimestampUtc)?> GetLastPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        if (db is null)
            return null;

        var normalized = CandleSymbolAliases.Canonical(symbol);
        try
        {
            var raw = await db.StringGetAsync(PriceKey(normalized));
            if (raw.IsNullOrEmpty)
                return null;

            var parts = ((string)raw!).Split('|');
            if (parts.Length < 2)
                return null;

            if (!decimal.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                return null;
            if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
                return null;

            return (price, DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis last-price read failed for {Symbol}", symbol);
            return null;
        }
    }

    public async Task ApplyTickToM1Async(
        string symbol,
        decimal price,
        DateTime timestampUtc,
        long volume = 0,
        CancellationToken cancellationToken = default)
    {
        if (price <= 0)
            return;

        var openUtc = CandleAggregator.FloorToIntervalUtc(EnsureUtc(timestampUtc), TimeSpan.FromMinutes(1));
        var existing = await GetM1Async(symbol, 1, cancellationToken);
        OhlcvBar bar;

        if (existing.Count == 1 && existing[0].OpenTime == openUtc)
        {
            var cur = existing[0];
            bar = new OhlcvBar
            {
                OpenTime = openUtc,
                Open = cur.Open,
                High = Math.Max(cur.High, price),
                Low = Math.Min(cur.Low, price),
                Close = price,
                Volume = cur.Volume + volume
            };
        }
        else
        {
            bar = new OhlcvBar
            {
                OpenTime = openUtc,
                Open = price,
                High = price,
                Low = price,
                Close = price,
                Volume = volume
            };
        }

        await UpsertM1Async(symbol, [bar], cancellationToken);
        await SetLastPriceAsync(symbol, price, timestampUtc, cancellationToken);
    }

    private string CandleKey(string symbol, string timeframe) =>
        $"{_redis.InstancePrefix}candles:{timeframe.ToLowerInvariant()}:{symbol.ToUpperInvariant()}";

    private string PriceKey(string symbol) =>
        $"{_redis.InstancePrefix}price:{symbol.ToUpperInvariant()}";

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static OhlcvBar? ParseBar(RedisValue value)
    {
        if (value.IsNullOrEmpty)
            return null;

        try
        {
            var row = JsonSerializer.Deserialize<RedisM1Bar>((string)value!, JsonOptions);
            if (row is null || row.T <= 0)
                return null;

            return new OhlcvBar
            {
                OpenTime = DateTimeOffset.FromUnixTimeSeconds(row.T).UtcDateTime,
                Open = row.O,
                High = row.H,
                Low = row.L,
                Close = row.C,
                Volume = row.V
            };
        }
        catch
        {
            return null;
        }
    }

    private sealed class RedisM1Bar
    {
        public long T { get; set; }
        public decimal O { get; set; }
        public decimal H { get; set; }
        public decimal L { get; set; }
        public decimal C { get; set; }
        public long V { get; set; }
    }
}
