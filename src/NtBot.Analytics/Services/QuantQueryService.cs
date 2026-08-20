using NtBot.Analytics.Configuration;
using NtBot.Analytics.Engines;
using NtBot.Analytics.Maths;
using NtBot.Analytics.Model;
using NtBot.Infrastructure.Persistence;
using NtBot.Shared.MarketData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace NtBot.Analytics.Services;

public sealed class QuantProbabilityQuery
{
    public string? Symbol { get; init; }
    public string? Strategy { get; init; }
    public string? Timeframe { get; init; }
    public string? Direction { get; init; }
    public string? Regime { get; init; }
    public string? Session { get; init; }
    public string? DeltaBucket { get; init; }
    public decimal? VolumeZMin { get; init; }
    public decimal? BookImbalanceMin { get; init; }
    public string? PriceVsVwap { get; init; }
    public string? Trend { get; init; }
    public string? Alignment { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string Horizon { get; init; } = "5m";
}

public sealed class QuantProbabilityResult
{
    public required string Symbol { get; init; }
    public required string Strategy { get; init; }
    public required string Horizon { get; init; }
    public required StatisticalSummary Summary { get; init; }
    public string Disclaimer { get; init; } =
        "Nas N ocorrências históricas semelhantes, X% apresentaram resultado positivo dentro da janela Y, com intervalo de confiança Z. Isto não é uma garantia de performance.";
    public bool Reliable => Summary.SampleClass is not "INSUFFICIENT_SAMPLE";
}

public interface IQuantQueryService
{
    Task<QuantProbabilityResult> ProbabilitiesAsync(QuantProbabilityQuery query, CancellationToken ct);
    Task<IReadOnlyList<object>> StrategiesAsync(QuantProbabilityQuery query, CancellationToken ct);
    Task<IReadOnlyList<object>> RegimesAsync(QuantProbabilityQuery query, CancellationToken ct);
    Task<IReadOnlyList<object>> OpeningAuctionAsync(string? symbol, CancellationToken ct);
    Task<IReadOnlyList<object>> TimeAsync(QuantProbabilityQuery query, CancellationToken ct);
    Task<IReadOnlyList<object>> RankingAsync(string orderBy, CancellationToken ct);
    Task<IReadOnlyList<object>> CorrelationsAsync(string? symbol, string? window, CancellationToken ct);
}

public sealed class QuantQueryService : IQuantQueryService
{
    private readonly NtBotDbContext _db;
    private readonly IStatisticalEngine _stats;
    private readonly IMemoryCache _cache;
    private readonly IOptions<QuantStatisticsOptions> _options;

    public QuantQueryService(
        NtBotDbContext db,
        IStatisticalEngine stats,
        IMemoryCache cache,
        IOptions<QuantStatisticsOptions> options)
    {
        _db = db;
        _stats = stats;
        _cache = cache;
        _options = options;
    }

    public async Task<QuantProbabilityResult> ProbabilitiesAsync(QuantProbabilityQuery query, CancellationToken ct)
    {
        var key = $"quant:p:{query.Symbol}:{query.Strategy}:{query.Direction}:{query.Regime}:{query.Session}:{query.DeltaBucket}:{query.VolumeZMin}:{query.BookImbalanceMin}:{query.PriceVsVwap}:{query.Trend}:{query.Alignment}:{query.Horizon}:{query.From}:{query.To}";
        if (_cache.TryGetValue(key, out QuantProbabilityResult? cached) && cached is not null)
            return cached;

        var symbol = CandleSymbolAliases.Canonical(query.Symbol ?? "WIN");
        var signals = _db.QuantSignalEvents.AsNoTracking().AsQueryable();
        if (query.Symbol is not null)
            signals = signals.Where(s => s.Symbol == symbol);
        if (query.Strategy is not null)
            signals = signals.Where(s => s.Strategy == query.Strategy);
        if (query.Timeframe is not null)
            signals = signals.Where(s => s.Timeframe == query.Timeframe);
        if (query.Direction is not null)
            signals = signals.Where(s => s.Direction == query.Direction);
        if (query.Regime is not null)
            signals = signals.Where(s => s.MarketRegime == query.Regime);
        if (query.Session is not null)
            signals = signals.Where(s => s.Session == query.Session);
        if (query.From is not null)
            signals = signals.Where(s => s.Timestamp >= query.From);
        if (query.To is not null)
            signals = signals.Where(s => s.Timestamp <= query.To);

        var rows = await (
            from signal in signals
            join outcome in _db.QuantSignalOutcomes.AsNoTracking() on signal.Id equals outcome.SignalId
            join feature in _db.QuantMarketFeatures.AsNoTracking() on signal.FeatureId equals feature.Id into fj
            from feature in fj.DefaultIfEmpty()
            select new { signal, outcome, feature }
        ).ToListAsync(ct);

        var filtered = new List<(decimal Return, decimal? Mfe, decimal? Mae, decimal? R)>();
        foreach (var row in rows)
        {
            var feature = row.feature;
            if (feature is not null)
                LookAheadGuard.EnsureNoFuture(row.signal.Timestamp, [feature.Timestamp]);

            if (query.DeltaBucket is not null)
            {
                if (feature is null || BucketClassifier.Delta(feature.DeltaZscore) != query.DeltaBucket)
                    continue;
            }
            if (query.VolumeZMin is not null)
            {
                if (feature is null || (feature.VolumeZscore ?? 0) < query.VolumeZMin)
                    continue;
            }
            if (query.BookImbalanceMin is not null)
            {
                if (feature is null || (feature.BookImbalance ?? 0) < query.BookImbalanceMin)
                    continue;
            }
            if (query.PriceVsVwap is not null)
            {
                if (feature is null ||
                    !string.Equals(BucketClassifier.PriceVsVwap(feature.Close, feature.Vwap), query.PriceVsVwap, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            if (query.Trend is not null)
            {
                if (feature is null ||
                    !string.Equals(feature.TrendDirection, query.Trend, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            if (query.Alignment is not null)
            {
                if (feature is null ||
                    !string.Equals(feature.MultiTimeframeAlignment, query.Alignment, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            var ret = HorizonReturn(row.outcome, query.Horizon);
            if (ret is null)
                continue;
            filtered.Add((ret.Value, HorizonMfe(row.outcome, query.Horizon), HorizonMae(row.outcome, query.Horizon), row.outcome.ReturnR));
        }

        var summary = _stats.Summarize(
            filtered.Select(f => f.Return).ToArray(),
            filtered.Select(f => f.Mfe).Where(v => v.HasValue).Select(v => v!.Value).ToArray(),
            filtered.Select(f => f.Mae).Where(v => v.HasValue).Select(v => v!.Value).ToArray(),
            filtered.Select(f => f.R).Where(v => v.HasValue).Select(v => v!.Value).ToArray(),
            _options.Value.MinimumSampleSize,
            _options.Value.LowSampleSize,
            _options.Value.MediumSampleSize,
            _options.Value.ConfidenceLevel);

        var result = new QuantProbabilityResult
        {
            Symbol = symbol,
            Strategy = query.Strategy ?? "ALL",
            Horizon = query.Horizon,
            Summary = summary,
            Disclaimer = summary.SampleClass == "INSUFFICIENT_SAMPLE"
                ? "Amostra insuficiente. A probabilidade não é apresentada como confiável."
                : $"Nas {summary.SampleCount} ocorrências históricas semelhantes, {(summary.SuccessProbability ?? 0) * 100:0.0}% apresentaram resultado positivo em {query.Horizon}, com intervalo de confiança {summary.ConfidenceLow:0.000}–{summary.ConfidenceHigh:0.000}. Isto não é uma garantia de performance."
        };
        _cache.Set(key, result, TimeSpan.FromSeconds(Math.Max(5, _options.Value.CacheTtlSeconds)));
        return result;
    }

    public async Task<IReadOnlyList<object>> StrategiesAsync(QuantProbabilityQuery query, CancellationToken ct)
    {
        var groups = await _db.QuantStatisticalObservations.AsNoTracking()
            .Where(o => (query.Symbol == null || o.Symbol == query.Symbol) && o.FeatureGroup == "delta_zscore")
            .ToListAsync(ct);
        return groups
            .GroupBy(g => new { g.Symbol, g.Strategy, g.Direction })
            .Select(g => g.OrderByDescending(x => x.SampleCount).First())
            .OrderByDescending(o => o.Expectancy ?? 0)
            .ThenByDescending(o => o.SampleCount)
            .Select(MapObservation)
            .ToList();
    }

    public async Task<IReadOnlyList<object>> RegimesAsync(QuantProbabilityQuery query, CancellationToken ct)
    {
        var rows = await _db.QuantStatisticalObservations.AsNoTracking()
            .Where(o => (query.Symbol == null || o.Symbol == query.Symbol) && (query.Strategy == null || o.Strategy == query.Strategy))
            .ToListAsync(ct);
        return rows
            .GroupBy(o => o.MarketRegime)
            .Select(g => (object)new
            {
                regime = g.Key,
                samples = g.Sum(x => x.SampleCount),
                expectancy = g.Average(x => x.Expectancy),
                winRate = g.Average(x => x.SuccessProbability)
            })
            .ToList();
    }

    public async Task<IReadOnlyList<object>> OpeningAuctionAsync(string? symbol, CancellationToken ct)
    {
        var canonical = symbol is null ? null : CandleSymbolAliases.Canonical(symbol);
        var rows = await _db.QuantOpeningAuctions.AsNoTracking()
            .Where(a => canonical == null || a.Symbol == canonical)
            .OrderByDescending(a => a.Date)
            .Take(90)
            .ToListAsync(ct);
        return rows.Select(a => (object)new
        {
            a.Date,
            a.Symbol,
            a.OpeningPrice,
            a.GapPoints,
            a.GapPercent,
            a.AuctionScore,
            a.AuctionClassification,
            a.Volume,
            a.Delta,
            a.IndicativeDataAvailable
        }).ToList();
    }

    public async Task<IReadOnlyList<object>> TimeAsync(QuantProbabilityQuery query, CancellationToken ct)
    {
        var rows = await _db.QuantSignalEvents.AsNoTracking()
            .Where(s => (query.Symbol == null || s.Symbol == query.Symbol) && s.Outcome != null)
            .Select(s => new { s.Timestamp, s.Direction, s.Outcome!.Success, s.Outcome.Return5m })
            .ToListAsync(ct);

        var weekday = rows
            .GroupBy(r => QuantSessionClock.ToSession(r.Timestamp, _options.Value).DayOfWeek)
            .Select(g => (object)new
            {
                bucket = "weekday",
                key = g.Key.ToString(),
                samples = g.Count(),
                winRate = g.Average(x => x.Success == true ? 1m : 0m),
                averageReturn = g.Average(x => x.Return5m)
            });

        var hourly = rows
            .GroupBy(r => QuantSessionClock.ToSession(r.Timestamp, _options.Value).ToString("HH:00"))
            .OrderBy(g => g.Key)
            .Select(g => (object)new
            {
                bucket = "hour",
                key = g.Key,
                samples = g.Count(),
                winRate = g.Average(x => x.Success == true ? 1m : 0m),
                averageReturn = g.Average(x => x.Return5m)
            });

        return weekday.Concat(hourly).ToList();
    }

    public async Task<IReadOnlyList<object>> RankingAsync(string orderBy, CancellationToken ct)
    {
        var rows = await _db.QuantStatisticalObservations.AsNoTracking()
            .Where(o => o.SampleCount >= _options.Value.MinimumSampleSize)
            .ToListAsync(ct);
        IEnumerable<NtBot.Domain.Entities.Quant.QuantStatisticalObservation> ordered = orderBy.ToLowerInvariant() switch
        {
            "winrate" => rows.OrderByDescending(r => r.SuccessProbability),
            "profitfactor" => rows.OrderByDescending(r => r.ProfitFactor),
            _ => rows.OrderByDescending(r => r.Expectancy).ThenByDescending(r => r.SampleCount)
        };
        return ordered.Take(50).Select(MapObservation).ToList();
    }

    public async Task<IReadOnlyList<object>> CorrelationsAsync(string? symbol, string? window, CancellationToken ct)
    {
        var canonical = symbol is null ? null : CandleSymbolAliases.Canonical(symbol);
        var rows = await _db.QuantAssetCorrelations.AsNoTracking()
            .Where(c =>
                (canonical == null || c.SymbolA == canonical || c.SymbolB == canonical)
                && (window == null || c.Window == window))
            .OrderByDescending(c => c.Timestamp)
            .Take(200)
            .ToListAsync(ct);
        return rows.Select(c => (object)new
        {
            c.SymbolA,
            c.SymbolB,
            c.Window,
            c.Pearson,
            c.Beta,
            c.ReturnA,
            c.ReturnB,
            c.VolatilityA,
            c.VolatilityB,
            c.SampleCount,
            c.Timestamp
        }).ToList();
    }

    private static object MapObservation(NtBot.Domain.Entities.Quant.QuantStatisticalObservation o) => new
    {
        o.Symbol,
        o.Strategy,
        o.SampleCount,
        o.SampleClass,
        o.SuccessProbability,
        confidenceInterval = new { level = o.ConfidenceLevel, low = o.ConfidenceLow, high = o.ConfidenceHigh },
        o.AverageReturn,
        o.MedianReturn,
        o.Expectancy,
        o.ProfitFactor,
        o.AverageMfe,
        o.AverageMae,
        o.MaxDrawdown,
        o.MarketRegime,
        o.Direction
    };

    private static decimal? HorizonReturn(NtBot.Domain.Entities.Quant.QuantSignalOutcome o, string horizon) => horizon switch
    {
        "15s" => o.Return15s,
        "30s" => o.Return30s,
        "1m" => o.Return1m,
        "15m" => o.Return15m,
        "30m" => o.Return30m,
        "60m" => o.Return60m,
        _ => o.Return5m
    };

    private static decimal? HorizonMfe(NtBot.Domain.Entities.Quant.QuantSignalOutcome o, string horizon) => horizon switch
    {
        "15s" => o.Mfe15s,
        "30s" => o.Mfe30s,
        "1m" => o.Mfe1m,
        "15m" => o.Mfe15m,
        "30m" => o.Mfe30m,
        "60m" => o.Mfe60m,
        _ => o.Mfe5m
    };

    private static decimal? HorizonMae(NtBot.Domain.Entities.Quant.QuantSignalOutcome o, string horizon) => horizon switch
    {
        "15s" => o.Mae15s,
        "30s" => o.Mae30s,
        "1m" => o.Mae1m,
        "15m" => o.Mae15m,
        "30m" => o.Mae30m,
        "60m" => o.Mae60m,
        _ => o.Mae5m
    };
}
