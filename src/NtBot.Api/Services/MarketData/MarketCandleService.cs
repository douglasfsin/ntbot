using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NtBot.Api.Configuration;
using NtBot.Api.Controllers;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;
using NtBot.MarketData.Clients;
using NtBot.Shared.MarketData;

namespace NtBot.Api.Services.MarketData;

public sealed class MarketCandleService : IMarketCandleService
{
    private static readonly ConcurrentDictionary<string, Task<CandleFetchResult>> InflightFetches = new(StringComparer.OrdinalIgnoreCase);

    private readonly IDbContextFactory<NtBotDbContext> _dbFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMarketDataApiClient _marketDataApi;
    private readonly QuantOptions _options;
    private readonly ILogger<MarketCandleService> _logger;
    private readonly IChartCandleCache _chartCache;
    private readonly ICandleRedisStore _candleRedis;
    private readonly ICandlePersistQueue _persistQueue;

    public MarketCandleService(
        IDbContextFactory<NtBotDbContext> dbFactory,
        IHttpClientFactory httpClientFactory,
        IMarketDataApiClient marketDataApi,
        IOptions<QuantOptions> options,
        IChartCandleCache chartCache,
        ICandleRedisStore candleRedis,
        ICandlePersistQueue persistQueue,
        ILogger<MarketCandleService> logger)
    {
        _dbFactory = dbFactory;
        _httpClientFactory = httpClientFactory;
        _marketDataApi = marketDataApi;
        _options = options.Value;
        _chartCache = chartCache;
        _candleRedis = candleRedis;
        _persistQueue = persistQueue;
        _logger = logger;
    }

    public async Task<CandleFetchResult> GetCandlesAsync(
        string symbol,
        int count = 100,
        string? timeframe = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var storageSymbol = NormalizeSymbol(symbol);
        var tf = NormalizeTimeframe(timeframe ?? _options.DefaultTimeframe);
        var cacheKey = $"{storageSymbol}|{tf}|{count}";

        var chartCached = TryGetChartCache(storageSymbol, tf, count);
        if (chartCached is not null)
        {
            _logger.LogDebug(
                "Candles span symbol={Symbol} timeframe={Timeframe} source=chart-cache count={Count} duration_ms={DurationMs} status=ok",
                storageSymbol, tf, chartCached.Candles.Count, sw.ElapsedMilliseconds);
            return new CandleFetchResult
            {
                Candles = chartCached.Candles.Select(row => new Candle
                {
                    Id = Guid.NewGuid(),
                    Symbol = storageSymbol,
                    Timeframe = tf,
                    OpenTime = DateTimeOffset.FromUnixTimeSeconds(row.Time).UtcDateTime,
                    CloseTime = DateTimeOffset.FromUnixTimeSeconds(row.Time).UtcDateTime,
                    Open = row.Open,
                    High = row.High,
                    Low = row.Low,
                    Close = row.Close,
                    CreatedAt = DateTime.UtcNow
                }).ToList(),
                Source = chartCached.Source
            };
        }

        try
        {
            var result = await CoalesceFetchAsync(cacheKey, async () =>
            {
                var fetched = await FetchCandlesCoreAsync(storageSymbol, tf, count, cancellationToken);
                StoreChartCache(storageSymbol, tf, count, fetched);
                return fetched;
            });
            _logger.LogInformation(
                "Candles span symbol={Symbol} timeframe={Timeframe} source={Source} count={Count} duration_ms={DurationMs} status=ok",
                storageSymbol, tf, result.Source, result.Candles.Count, sw.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException)
        {
            // Request/timeout cancelou no meio do fetch — devolve cache parcial ou vazio (nunca 500).
            var fallback = TryGetChartCache(storageSymbol, tf, count);
            if (fallback is not null)
            {
                _logger.LogInformation(
                    "Candles span symbol={Symbol} timeframe={Timeframe} source={Source} count={Count} duration_ms={DurationMs} status=canceled",
                    storageSymbol, tf, fallback.Source + "-canceled", fallback.Candles.Count, sw.ElapsedMilliseconds);
                return new CandleFetchResult
                {
                    Candles = fallback.Candles.Select(row => new Candle
                    {
                        Id = Guid.NewGuid(),
                        Symbol = storageSymbol,
                        Timeframe = tf,
                        OpenTime = DateTimeOffset.FromUnixTimeSeconds(row.Time).UtcDateTime,
                        CloseTime = DateTimeOffset.FromUnixTimeSeconds(row.Time).UtcDateTime,
                        Open = row.Open,
                        High = row.High,
                        Low = row.Low,
                        Close = row.Close,
                        CreatedAt = DateTime.UtcNow
                    }).ToList(),
                    Source = fallback.Source + "-canceled"
                };
            }

            _logger.LogDebug("Candle fetch canceled for {Symbol} {Tf} duration_ms={DurationMs}",
                storageSymbol, tf, sw.ElapsedMilliseconds);
            return new CandleFetchResult { Source = "canceled" };
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Candle fetch soft-failed on DbUpdate for {Symbol} {Tf}", storageSymbol, tf);
            return TryGetChartCache(storageSymbol, tf, count) is { } cached
                ? new CandleFetchResult
                {
                    Candles = cached.Candles.Select(row => new Candle
                    {
                        Id = Guid.NewGuid(),
                        Symbol = storageSymbol,
                        Timeframe = tf,
                        OpenTime = DateTimeOffset.FromUnixTimeSeconds(row.Time).UtcDateTime,
                        CloseTime = DateTimeOffset.FromUnixTimeSeconds(row.Time).UtcDateTime,
                        Open = row.Open,
                        High = row.High,
                        Low = row.Low,
                        Close = row.Close,
                        CreatedAt = DateTime.UtcNow
                    }).ToList(),
                    Source = cached.Source + "-db-softfail"
                }
                : new CandleFetchResult { Source = "db-softfail" };
        }
    }

    private ChartCandlesPush? TryGetChartCache(string symbol, string timeframe, int count)
    {
        // Chart stream caches with chart keys ("5"); this service stores normalized ("M5").
        var keys = new[] { timeframe, ChartTimeframe.ToChartKey(timeframe), ChartTimeframe.Normalize(timeframe) }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var key in keys)
        {
            var direct = _chartCache.Get(symbol, key, count);
            if (direct is not null && direct.Candles.Count >= Math.Min(count, 5))
                return direct;
        }

        foreach (var bucket in new[] { 120, 100, 80 })
        {
            if (bucket < count)
                continue;

            foreach (var key in keys)
            {
                var cached = _chartCache.Get(symbol, key, bucket);
                if (cached is not null && cached.Candles.Count >= Math.Min(count, 5))
                    return cached;
            }
        }

        return null;
    }

    private static async Task<CandleFetchResult> CoalesceFetchAsync(
        string key,
        Func<Task<CandleFetchResult>> factory)
    {
        while (true)
        {
            if (InflightFetches.TryGetValue(key, out var running))
                return await running.ConfigureAwait(false);

            var task = factory();
            if (InflightFetches.TryAdd(key, task))
            {
                try
                {
                    return await task.ConfigureAwait(false);
                }
                finally
                {
                    InflightFetches.TryRemove(key, out _);
                }
            }
        }
    }

    private void StoreChartCache(string symbol, string timeframe, int count, CandleFetchResult result)
    {
        if (!result.HasSufficientData(5))
            return;

        var chartTf = ChartTimeframe.ToChartKey(timeframe);
        var payload = new ChartCandlesPush
        {
            Symbol = symbol,
            Timeframe = chartTf,
            Source = result.Source,
            IsPartial = result.Source.Contains("partial", StringComparison.OrdinalIgnoreCase),
            Candles = result.Candles
                .OrderBy(c => c.OpenTime)
                .Select(c => new ChartCandleDto
                {
                    Time = new DateTimeOffset(c.OpenTime).ToUnixTimeSeconds(),
                    Open = c.Open,
                    High = c.High,
                    Low = c.Low,
                    Close = c.Close
                })
                .ToList(),
            UpdatedAt = DateTime.UtcNow
        };

        _chartCache.Set(symbol, chartTf, count, payload);
        _chartCache.Set(symbol, ChartTimeframe.Normalize(timeframe), count, payload);
    }

    private async Task<CandleFetchResult> FetchCandlesCoreAsync(
        string storageSymbol,
        string tf,
        int count,
        CancellationToken cancellationToken)
    {
        var minimum = Math.Min(count, _options.MinCandles);
        var prefersMt5 = PrefersMt5Source(storageSymbol);
        var useM1RedisPath = PreferM1RedisPath(tf);

        // Prefer Redis M1 (direct or aggregated) before remote/DB.
        var fromRedis = await TryGetFromRedisAsync(storageSymbol, tf, count, minimum, cancellationToken);
        if (fromRedis is not null)
            return fromRedis;

        // Chart path (1/3/5/15/30/60): seed M1 into Redis, then aggregate — never block on Postgres.
        if (useM1RedisPath)
        {
            if (prefersMt5 && !string.IsNullOrWhiteSpace(_options.Mt5ApiUrl))
            {
                var seeded = await SeedM1FromMt5Async(storageSymbol, tf, count, cancellationToken);
                if (seeded)
                {
                    fromRedis = await TryGetFromRedisAsync(storageSymbol, tf, count, minimum, cancellationToken);
                    if (fromRedis is not null)
                        return fromRedis.WithSource(AnnotateMt5RedisSource(fromRedis.Source));
                }
            }

            if (IsB3ProfitSymbol(storageSymbol))
            {
                var profitM1 = await FetchFromProfitDllAsync(storageSymbol, "M1",
                    Math.Min(MaxM1SeedBars, CandleAggregator.RequiredM1Count(ChartTimeframe.ToMinutes(tf), count, max: MaxM1SeedBars)),
                    cancellationToken);
                if (profitM1.Count >= 5)
                {
                    await PersistCandlesToRedisAsync(profitM1, cancellationToken);
                    EnqueuePostgresFlush(profitM1);
                    fromRedis = await TryGetFromRedisAsync(storageSymbol, tf, count, minimum, cancellationToken);
                    if (fromRedis is not null)
                        return fromRedis.WithSource(
                            fromRedis.Source.Contains("agg", StringComparison.Ordinal)
                                ? "profitdll-redis-agg"
                                : "profitdll-redis-m1");
                }
            }
        }

        // Forex/metals (XAUUSD, etc.): MT5 native TF fallback when M1 seed missed.
        if (prefersMt5 && !string.IsNullOrWhiteSpace(_options.Mt5ApiUrl))
        {
            var mt5Early = await TryFetchMt5ResultAsync(storageSymbol, tf, count, minimum, cancellationToken);
            if (mt5Early is not null)
                return mt5Early;
        }

        if (IsB3ProfitSymbol(storageSymbol) && !useM1RedisPath)
        {
            var profitCandles = await FetchFromProfitDllAsync(storageSymbol, tf, count, cancellationToken);
            if (profitCandles.Count >= minimum)
            {
                await PersistCandlesToRedisAsync(profitCandles, cancellationToken);
                EnqueuePostgresFlush(profitCandles);
                return new CandleFetchResult { Candles = profitCandles, Source = "profitdll" };
            }

            if (profitCandles.Count > 0)
            {
                await PersistCandlesToRedisAsync(profitCandles, cancellationToken);
                EnqueuePostgresFlush(profitCandles);
                return new CandleFetchResult { Candles = profitCandles, Source = "profitdll-partial" };
            }
        }

        // Prefer M1 rows from Postgres when charting aggregate TFs (warm Redis, then aggregate).
        if (useM1RedisPath && ChartTimeframe.ToMinutes(tf) > 1)
        {
            var m1Needed = Math.Min(
                MaxM1SeedBars,
                CandleAggregator.RequiredM1Count(ChartTimeframe.ToMinutes(tf), count, max: MaxM1SeedBars));
            var dbM1 = await LoadFromDatabaseAsync(storageSymbol, "M1", m1Needed, cancellationToken);
            if (dbM1.Count >= 5)
            {
                await PersistCandlesToRedisAsync(dbM1, cancellationToken);
                fromRedis = await TryGetFromRedisAsync(storageSymbol, tf, count, minimum, cancellationToken);
                if (fromRedis is not null)
                    return fromRedis.WithSource("database-redis-agg");
            }
        }

        var dbCandles = await LoadFromDatabaseAsync(storageSymbol, tf, count, cancellationToken);
        if (IsFresh(dbCandles, tf) && dbCandles.Count >= minimum)
        {
            // Warm Redis from DB so subsequent chart hits avoid Postgres.
            await PersistCandlesToRedisAsync(dbCandles, cancellationToken);
            return new CandleFetchResult { Candles = dbCandles, Source = "database" };
        }

        // Non-B3 fallback (or B3 after ProfitDLL miss): MT5 when configured.
        if (!prefersMt5 && !IsB3ProfitSymbol(storageSymbol) && !string.IsNullOrWhiteSpace(_options.Mt5ApiUrl))
        {
            var mt5Fallback = await TryFetchMt5ResultAsync(storageSymbol, tf, count, minimum, cancellationToken);
            if (mt5Fallback is not null)
                return mt5Fallback;
        }

        if (dbCandles.Count > 0)
        {
            await PersistCandlesToRedisAsync(dbCandles, cancellationToken);
            return new CandleFetchResult { Candles = dbCandles, Source = "database-stale" };
        }

        _logger.LogWarning(
            "Candles indisponíveis para {Symbol} ({Timeframe}). Configure MarketDataApi, Quant:Mt5ApiUrl ou o sync OHLCV do connector.",
            storageSymbol,
            tf);

        return new CandleFetchResult();
    }

    /// <summary>Chart timeframes that must be served from Redis M1 (or aggregated from it).</summary>
    private static bool PreferM1RedisPath(string tf)
    {
        var minutes = ChartTimeframe.ToMinutes(tf);
        return minutes is 1 or 3 or 5 or 15 or 30 or 60;
    }

    private static string AnnotateMt5RedisSource(string source) =>
        source.StartsWith("redis", StringComparison.Ordinal)
            ? source.Replace("redis", "mt5-redis", StringComparison.Ordinal)
            : "mt5-redis-agg";

    private async Task<CandleFetchResult?> TryGetFromRedisAsync(
        string storageSymbol,
        string tf,
        int count,
        int minimum,
        CancellationToken cancellationToken)
    {
        if (!_candleRedis.IsAvailable)
        {
            _logger.LogDebug("Redis unavailable — skipping candle cache for {Symbol} {Tf}", storageSymbol, tf);
            return null;
        }

        if (!ChartTimeframe.CanAggregateFromM1(tf))
            return null;

        try
        {
            var bars = await _candleRedis.GetAggregatedAsync(storageSymbol, tf, count, cancellationToken);
            if (bars.Count < Math.Min(minimum, 5))
                return null;

            var candles = bars.Select(b => ToCandle(storageSymbol, tf, b)).ToList();
            var minutes = ChartTimeframe.ToMinutes(tf);
            var source = minutes == 1 ? "redis-m1" : "redis-agg";

            return new CandleFetchResult
            {
                Candles = candles,
                Source = bars.Count >= minimum ? source : $"{source}-partial"
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Redis candle read failed for {Symbol} {Tf}", storageSymbol, tf);
            return null;
        }
    }

    /// <summary>
    /// Caps M1 backfill so MT5/HTTP never stalls chart requests (H1×80 ≈ 4800 bars timed out at 25s).
    /// </summary>
    private const int MaxM1SeedBars = 1_500;

    private async Task<bool> SeedM1FromMt5Async(
        string storageSymbol,
        string tf,
        int count,
        CancellationToken cancellationToken)
    {
        try
        {
            var minutes = ChartTimeframe.ToMinutes(tf);
            var m1Count = Math.Min(
                MaxM1SeedBars,
                CandleAggregator.RequiredM1Count(minutes, count, max: MaxM1SeedBars));
            if (m1Count < 5)
                return false;

            var mt5Symbol = ResolveMt5Symbol(storageSymbol);
            var fetched = await FetchFromMt5Async(mt5Symbol, storageSymbol, "M1", m1Count, cancellationToken);
            if (fetched.Count < 5)
                return false;

            // Redis only on the request path; Postgres via 15m background flush.
            await PersistCandlesToRedisAsync(fetched, CancellationToken.None);
            EnqueuePostgresFlush(fetched);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "M1 Redis seed skipped for {Symbol} {Tf}", storageSymbol, tf);
            return false;
        }
    }

    private async Task<CandleFetchResult?> TryFetchMt5ResultAsync(
        string storageSymbol,
        string tf,
        int count,
        int minimum,
        CancellationToken cancellationToken)
    {
        // Fast path: fetch the requested timeframe directly (80 H1 bars), not thousands of M1.
        var mt5Symbol = ResolveMt5Symbol(storageSymbol);
        var fetched = await FetchFromMt5Async(mt5Symbol, storageSymbol, tf, count, cancellationToken);
        if (fetched.Count >= minimum)
        {
            // Redis first (fast); Postgres queued for 15m flush — never block chart on N+1 upserts.
            await PersistCandlesToRedisAsync(fetched, cancellationToken);
            EnqueuePostgresFlush(fetched);
            if (PreferM1RedisPath(tf) && ChartTimeframe.ToMinutes(tf) > 1)
                _ = SeedM1InBackgroundAsync(storageSymbol, tf, count);

            return new CandleFetchResult { Candles = fetched, Source = "mt5" };
        }

        if (fetched.Count > 0)
        {
            await PersistCandlesToRedisAsync(fetched, cancellationToken);
            EnqueuePostgresFlush(fetched);
            return new CandleFetchResult { Candles = fetched, Source = "mt5-partial" };
        }

        // Last resort: capped M1 seed + aggregate (e.g. when only M1 exists on the host).
        if (ChartTimeframe.CanAggregateFromM1(tf) && ChartTimeframe.ToMinutes(tf) > 1)
        {
            var seeded = await SeedM1FromMt5Async(storageSymbol, tf, count, cancellationToken);
            if (seeded)
            {
                var fromRedis = await TryGetFromRedisAsync(storageSymbol, tf, count, minimum, cancellationToken);
                if (fromRedis is not null)
                    return fromRedis.WithSource(AnnotateMt5RedisSource(fromRedis.Source));
            }
        }

        return null;
    }

    private async Task SeedM1InBackgroundAsync(string storageSymbol, string tf, int count)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            await SeedM1FromMt5Async(storageSymbol, tf, count, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Background M1 seed failed for {Symbol}", storageSymbol);
        }
    }

    private static Candle ToCandle(string symbol, string timeframe, OhlcvBar bar) => new()
    {
        Id = Guid.NewGuid(),
        Symbol = symbol,
        Timeframe = ChartTimeframe.Normalize(timeframe),
        OpenTime = bar.OpenTime,
        CloseTime = bar.OpenTime,
        Open = bar.Open,
        High = bar.High,
        Low = bar.Low,
        Close = bar.Close,
        Volume = bar.Volume,
        CreatedAt = DateTime.UtcNow
    };

    /// <summary>
    /// Symbols that come from the MT5 Python host (not ProfitDLL / MarketData.API).
    /// </summary>
    private static bool PrefersMt5Source(string symbol) =>
        !IsB3ProfitSymbol(symbol) && symbol is
            "XAUUSD" or "EURUSD" or "NZDUSD" or "GBPUSD" or "USDJPY" or
            "BTCUSD" or "USOUSD" or "UKOUSD" or "USDBRL" or "USDMXN" or
            "NQ" or "ES" or "MNQ" or "MES";

    private void EnqueuePostgresFlush(IEnumerable<Candle> candles) =>
        _persistQueue.Enqueue(candles);

    public async Task<int> UpsertCandlesAsync(IEnumerable<Candle> candles, CancellationToken cancellationToken = default)
    {
        var batch = candles
            .Where(c => !string.IsNullOrWhiteSpace(c.Symbol) && c.OpenTime != default)
            .Select(c =>
            {
                c.Symbol = NormalizeSymbol(c.Symbol);
                c.Timeframe = NormalizeTimeframe(c.Timeframe);
                if (c.CloseTime == default)
                    c.CloseTime = c.OpenTime;
                return c;
            })
            .ToList();

        if (batch.Count == 0)
            return 0;

        // Hot path: Redis only (24h TTL). Postgres is flushed every ~15 minutes by CandlePostgresFlushWorker.
        await PersistCandlesToRedisAsync(batch, cancellationToken);
        EnqueuePostgresFlush(batch);
        return batch.Count;
    }

    private async Task PersistCandlesToRedisAsync(IReadOnlyList<Candle> batch, CancellationToken cancellationToken)
    {
        if (!_candleRedis.IsAvailable || batch.Count == 0)
            return;

        foreach (var bySymbolTf in batch.GroupBy(c => (Symbol: NormalizeSymbol(c.Symbol), Tf: NormalizeTimeframe(c.Timeframe))))
        {
            var bars = bySymbolTf.Select(c => new OhlcvBar
            {
                OpenTime = c.OpenTime,
                Open = c.Open,
                High = c.High,
                Low = c.Low,
                Close = c.Close,
                Volume = c.Volume
            }).ToList();

            if (bars.Count == 0)
                continue;

            await _candleRedis.UpsertTimeframeAsync(bySymbolTf.Key.Symbol, bySymbolTf.Key.Tf, bars, cancellationToken);

            var last = bars.OrderBy(b => b.OpenTime).LastOrDefault();
            if (last is not null && last.Close > 0)
                await _candleRedis.SetLastPriceAsync(bySymbolTf.Key.Symbol, last.Close, last.OpenTime, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<string>> GetAvailableSymbolsAsync(
        int minimum = 50,
        string? timeframe = null,
        CancellationToken cancellationToken = default)
    {
        var tf = NormalizeTimeframe(timeframe ?? _options.DefaultTimeframe);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Candles
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
        var tfAliases = ChartTimeframe.Aliases(timeframe);
        var symbolAliases = CandleSymbolAliases.Expand(symbol);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));

            await using var db = await _dbFactory.CreateDbContextAsync(timeout.Token);

            return await db.Candles
                .AsNoTracking()
                .Where(c => symbolAliases.Contains(c.Symbol) && tfAliases.Contains(c.Timeframe))
                .OrderByDescending(c => c.OpenTime)
                .Take(count)
                .OrderBy(c => c.OpenTime)
                .ToListAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug(
                "Consulta DB de candles cancelada/timeout para {Symbol} ({Timeframe})",
                symbol,
                timeframe);
            return [];
        }
        catch (Exception ex) when (ex is DbUpdateException or TimeoutException or System.Data.Common.DbException)
        {
            _logger.LogWarning(ex, "Consulta DB de candles soft-failed para {Symbol} ({Timeframe})", symbol, timeframe);
            return [];
        }
    }

    private bool IsFresh(IReadOnlyList<Candle> candles, string timeframe)
    {
        if (candles.Count == 0)
            return false;

        var latest = candles.Max(c => c.OpenTime);
        var age = DateTime.UtcNow - latest.ToUniversalTime();
        var maxAge = ChartTimeframe.Normalize(timeframe) switch
        {
            "M1" => TimeSpan.FromMinutes(2),
            "M3" => TimeSpan.FromMinutes(5),
            "M5" => TimeSpan.FromMinutes(10),
            "M15" => TimeSpan.FromMinutes(20),
            "M30" => TimeSpan.FromMinutes(35),
            "H1" => TimeSpan.FromMinutes(65),
            "H4" => TimeSpan.FromHours(5),
            "D1" => TimeSpan.FromHours(25),
            _ => TimeSpan.FromMinutes(_options.DatabaseMaxAgeMinutes)
        };

        return age <= maxAge;
    }

    private async Task<List<Candle>> FetchFromProfitDllAsync(
        string storageSymbol,
        string timeframe,
        int count,
        CancellationToken cancellationToken)
    {
        var minutes = TimeframeToMinutes(timeframe);
        try
        {
            using var profitTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            profitTimeout.CancelAfter(IsB3FuturesSymbol(storageSymbol)
                ? TimeSpan.FromSeconds(45)
                : TimeSpan.FromSeconds(35));

            var response = await _marketDataApi.GetCandlesAsync(
                storageSymbol,
                minutes,
                count,
                ct: profitTimeout.Token);
            if (response?.Candles is null || response.Candles.Count == 0)
                return [];

            return response.Candles
                .Select(row => new Candle
                {
                    Id = Guid.NewGuid(),
                    Symbol = storageSymbol,
                    Timeframe = timeframe,
                    OpenTime = row.Time.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(row.Time, DateTimeKind.Local)
                        : row.Time,
                    CloseTime = row.Time,
                    Open = row.Open,
                    High = row.High,
                    Low = row.Low,
                    Close = row.Close,
                    Volume = row.Volume,
                    CreatedAt = DateTime.UtcNow
                })
                .OrderBy(c => c.OpenTime)
                .ToList();
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException)
        {
            _logger.LogDebug(
                "ProfitDLL timeout/cancel para {Symbol} ({Timeframe}); usando fallback DB/MT5",
                storageSymbol,
                timeframe);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ProfitDLL indisponível para {Symbol} ({Timeframe})", storageSymbol, timeframe);
            return [];
        }
    }

    private static bool IsB3ProfitSymbol(string symbol) =>
        symbol is "WIN" or "WDO" or "PETR4" or "VALE3" or "ITUB4" or "BBDC4" or "WEGE3" or "ABEV3";

    private static bool IsB3FuturesSymbol(string symbol) =>
        symbol is "WIN" or "WDO";

    private static int TimeframeToMinutes(string timeframe) =>
        ChartTimeframe.ToMinutes(timeframe);

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
            // Native TF requests are small; keep timeout short so chart screens stay responsive.
            client.Timeout = TimeSpan.FromSeconds(count > 500 ? 20 : 12);

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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout surfaces as TaskCanceledException — do not break chart/TI screens.
            _logger.LogWarning(
                "MT5 OHLCV timeout for {Symbol} ({Timeframe}) count={Count} via {Mt5Symbol}",
                storageSymbol, timeframe, count, mt5Symbol);
            return [];
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
        CandleSymbolAliases.Canonical(string.IsNullOrWhiteSpace(symbol) ? string.Empty : symbol.Trim());

    internal static string NormalizeTimeframe(string timeframe) =>
        ChartTimeframe.Normalize(timeframe);

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
