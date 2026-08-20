using System.Diagnostics;
using NtBot.Analytics.Configuration;
using NtBot.Analytics.Engines;
using NtBot.Analytics.Maths;
using NtBot.Analytics.Model;
using NtBot.Domain.Entities.Quant;
using NtBot.Infrastructure.Persistence;
using NtBot.Shared.MarketData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NtBot.Analytics.Services;

public interface IQuantOutcomeProcessor
{
    Task ProcessPendingAsync(CancellationToken ct);
}

public sealed class QuantOutcomeProcessor : IQuantOutcomeProcessor
{
    private static readonly Dictionary<string, TimeSpan> Horizons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["15s"] = TimeSpan.FromSeconds(15),
        ["30s"] = TimeSpan.FromSeconds(30),
        ["1m"] = TimeSpan.FromMinutes(1),
        ["5m"] = TimeSpan.FromMinutes(5),
        ["15m"] = TimeSpan.FromMinutes(15),
        ["30m"] = TimeSpan.FromMinutes(30),
        ["60m"] = TimeSpan.FromMinutes(60)
    };

    private readonly NtBotDbContext _db;
    private readonly IQuantRepository _repository;
    private readonly IOutcomeEngine _outcomes;
    private readonly ILogger<QuantOutcomeProcessor> _logger;

    public QuantOutcomeProcessor(
        NtBotDbContext db,
        IQuantRepository repository,
        IOutcomeEngine outcomes,
        ILogger<QuantOutcomeProcessor> logger)
    {
        _db = db;
        _repository = repository;
        _outcomes = outcomes;
        _logger = logger;
    }

    public async Task ProcessPendingAsync(CancellationToken ct)
    {
        using var activity = QuantActivity.Source.StartActivity("OutcomeCalculation");
        var sw = Stopwatch.StartNew();
        try
        {
            var pending = await _repository.IncompleteSignalsAsync(50, ct);
            foreach (var signal in pending)
                await ProcessOneAsync(signal, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            QuantMeters.CalculationErrors.Add(1);
            _logger.LogWarning(ex, "Outcome processor failed");
        }
        finally
        {
            QuantMeters.OutcomeLatency.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    private async Task ProcessOneAsync(QuantSignalEvent signal, CancellationToken ct)
    {
        var aliases = CandleSymbolAliases.Expand(signal.Symbol);
        var paths = new Dictionary<string, HorizonPath>(StringComparer.OrdinalIgnoreCase);
        decimal? max = null;
        decimal? min = null;
        foreach (var (name, span) in Horizons)
        {
            var until = signal.Timestamp.Add(span);
            if (until > DateTime.UtcNow)
            {
                paths[name] = new HorizonPath();
                continue;
            }

            var future = await _db.Candles.AsNoTracking()
                .Where(c => aliases.Contains(c.Symbol) && c.OpenTime > signal.Timestamp && c.OpenTime <= until)
                .ToListAsync(ct);
            var ticks = await _db.QuantMarketFeatures.AsNoTracking()
                .Where(f => f.Symbol == signal.Symbol && f.Timeframe.EndsWith("s") && f.Timestamp > signal.Timestamp && f.Timestamp <= until)
                .ToListAsync(ct);

            if (future.Count == 0 && ticks.Count == 0)
            {
                paths[name] = new HorizonPath();
                continue;
            }

            var high = future.Select(c => c.High).Concat(ticks.Select(t => t.High)).DefaultIfEmpty(signal.Price).Max();
            var low = future.Select(c => c.Low).Concat(ticks.Select(t => t.Low)).DefaultIfEmpty(signal.Price).Min();
            var last = ticks.OrderBy(t => t.Timestamp).Select(t => (decimal?)t.Close).LastOrDefault()
                       ?? future.OrderBy(c => c.OpenTime).Select(c => (decimal?)c.Close).LastOrDefault()
                       ?? signal.Price;
            max = max is null ? high : Math.Max(max.Value, high);
            min = min is null ? low : Math.Min(min.Value, low);
            paths[name] = new HorizonPath { Available = true, Price = last, High = high, Low = low };
        }

        var snapshot = _outcomes.Calculate(signal.Direction, signal.Price, signal.StopPrice, signal.TargetPrice, paths);
        var outcome = signal.Outcome ?? new QuantSignalOutcome { Id = Guid.NewGuid(), SignalId = signal.Id };
        outcome.Symbol = signal.Symbol;
        outcome.Direction = signal.Direction;
        outcome.SignalPrice = signal.Price;
        outcome.Return15s = snapshot.Returns.GetValueOrDefault("15s");
        outcome.Return30s = snapshot.Returns.GetValueOrDefault("30s");
        outcome.Return1m = snapshot.Returns.GetValueOrDefault("1m");
        outcome.Return5m = snapshot.Returns.GetValueOrDefault("5m");
        outcome.Return15m = snapshot.Returns.GetValueOrDefault("15m");
        outcome.Return30m = snapshot.Returns.GetValueOrDefault("30m");
        outcome.Return60m = snapshot.Returns.GetValueOrDefault("60m");
        outcome.Mfe15s = snapshot.Mfe.GetValueOrDefault("15s");
        outcome.Mae15s = snapshot.Mae.GetValueOrDefault("15s");
        outcome.Mfe30s = snapshot.Mfe.GetValueOrDefault("30s");
        outcome.Mae30s = snapshot.Mae.GetValueOrDefault("30s");
        outcome.Mfe1m = snapshot.Mfe.GetValueOrDefault("1m");
        outcome.Mae1m = snapshot.Mae.GetValueOrDefault("1m");
        outcome.Mfe5m = snapshot.Mfe.GetValueOrDefault("5m");
        outcome.Mae5m = snapshot.Mae.GetValueOrDefault("5m");
        outcome.Mfe15m = snapshot.Mfe.GetValueOrDefault("15m");
        outcome.Mae15m = snapshot.Mae.GetValueOrDefault("15m");
        outcome.Mfe30m = snapshot.Mfe.GetValueOrDefault("30m");
        outcome.Mae30m = snapshot.Mae.GetValueOrDefault("30m");
        outcome.Mfe60m = snapshot.Mfe.GetValueOrDefault("60m");
        outcome.Mae60m = snapshot.Mae.GetValueOrDefault("60m");
        outcome.MaxPrice = snapshot.MaxPrice;
        outcome.MinPrice = snapshot.MinPrice;
        outcome.TargetHit = snapshot.TargetHit;
        outcome.StopHit = snapshot.StopHit;
        outcome.Success = snapshot.Success5m;
        outcome.ReturnPoints = snapshot.ReturnPoints;
        outcome.ReturnPercent = snapshot.ReturnPercent;
        outcome.ReturnR = snapshot.ReturnR;
        outcome.OutcomeClass = snapshot.Success5m is true ? "WIN" : snapshot.Success5m is false ? "LOSS" : "PENDING";
        outcome.Complete = snapshot.Complete;
        outcome.UpdatedAt = DateTime.UtcNow;
        if (outcome.CreatedAt == default)
            outcome.CreatedAt = DateTime.UtcNow;
        await _repository.UpsertOutcomeAsync(outcome, ct);
    }
}

public interface IQuantAggregationService
{
    Task RefreshAsync(CancellationToken ct);
}

public sealed class QuantAggregationService : IQuantAggregationService
{
    private readonly NtBotDbContext _db;
    private readonly IQuantRepository _repository;
    private readonly IStatisticalEngine _stats;
    private readonly IOptions<QuantStatisticsOptions> _options;
    private readonly ILogger<QuantAggregationService> _logger;

    public QuantAggregationService(
        NtBotDbContext db,
        IQuantRepository repository,
        IStatisticalEngine stats,
        IOptions<QuantStatisticsOptions> options,
        ILogger<QuantAggregationService> logger)
    {
        _db = db;
        _repository = repository;
        _stats = stats;
        _options = options;
        _logger = logger;
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        using var activity = QuantActivity.Source.StartActivity("StatisticalAggregation");
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await (
                from signal in _db.QuantSignalEvents.AsNoTracking()
                join outcome in _db.QuantSignalOutcomes.AsNoTracking() on signal.Id equals outcome.SignalId
                where outcome.Return5m != null
                select new { signal, outcome }
            ).ToListAsync(ct);

            if (rows.Count > 0)
            {
                var featureKeys = rows.Select(r => r.signal.Symbol).Distinct().ToArray();
                var minTs = rows.Min(r => r.signal.Timestamp);
                var features = await _db.QuantMarketFeatures.AsNoTracking()
                    .Where(f => featureKeys.Contains(f.Symbol) && f.Timestamp >= minTs.AddHours(-4) && f.Timestamp <= DateTime.UtcNow)
                    .Select(f => new { f.Symbol, f.Timestamp, f.DeltaZscore, f.VolumeZscore, f.BookImbalance, f.Close, f.Vwap, f.MultiTimeframeAlignment })
                    .ToListAsync(ct);
                var featuresBySymbol = features.GroupBy(f => f.Symbol).ToDictionary(g => g.Key, g => g.OrderBy(x => x.Timestamp).ToList());

                var joined = rows.Select(r =>
                {
                    featuresBySymbol.TryGetValue(r.signal.Symbol, out var series);
                    var feature = series?
                        .Where(f => f.Timestamp <= r.signal.Timestamp)
                        .LastOrDefault();
                    if (feature is not null)
                        LookAheadGuard.EnsureNoFuture(r.signal.Timestamp, [feature.Timestamp]);
                    return new { r.signal, r.outcome, feature };
                }).ToList();

                var featureGroups = joined.GroupBy(r => new
                {
                    r.signal.Symbol,
                    r.signal.Strategy,
                    r.signal.Timeframe,
                    r.signal.Session,
                    Regime = r.signal.MarketRegime ?? "UNKNOWN",
                    r.signal.Direction,
                    DeltaBucket = BucketClassifier.Delta(r.feature?.DeltaZscore),
                    VolumeBucket = BucketClassifier.VolumeZ(r.feature?.VolumeZscore),
                    BookBucket = BucketClassifier.BookImbalance(r.feature?.BookImbalance),
                    VwapBucket = r.feature?.Vwap is null ? "UNKNOWN" : BucketClassifier.PriceVsVwap(r.feature.Close, r.feature.Vwap),
                    Alignment = r.feature?.MultiTimeframeAlignment ?? "MIXED"
                });

                foreach (var group in featureGroups)
                {
                    var summary = SummarizeGroup(group.Select(g => g.outcome).ToArray());
                    await Persist(group.Key.Symbol, group.Key.Strategy, group.Key.Timeframe, group.Key.Session, group.Key.Regime, group.Key.Direction,
                        "delta_zscore", "delta", group.Key.DeltaBucket, summary, ct);
                    await Persist(group.Key.Symbol, group.Key.Strategy, group.Key.Timeframe, group.Key.Session, group.Key.Regime, group.Key.Direction,
                        "volume_zscore", "volume", group.Key.VolumeBucket, summary, ct);
                    await Persist(group.Key.Symbol, group.Key.Strategy, group.Key.Timeframe, group.Key.Session, group.Key.Regime, group.Key.Direction,
                        "book_imbalance", "book", group.Key.BookBucket, summary, ct);
                    await Persist(group.Key.Symbol, group.Key.Strategy, group.Key.Timeframe, group.Key.Session, group.Key.Regime, group.Key.Direction,
                        "price_vs_vwap", "vwap", group.Key.VwapBucket, summary, ct);
                    await Persist(group.Key.Symbol, group.Key.Strategy, group.Key.Timeframe, group.Key.Session, group.Key.Regime, group.Key.Direction,
                        "mtf", "alignment", group.Key.Alignment, summary, ct);
                }

                foreach (var group in joined.GroupBy(r => new
                {
                    r.signal.Symbol,
                    r.signal.Strategy,
                    r.signal.Direction,
                    Hour = QuantSessionClock.ToSession(r.signal.Timestamp, _options.Value).ToString("HH:mm")
                }))
                {
                    var summary = SummarizeGroup(group.Select(g => g.outcome).ToArray());
                    await Persist(group.Key.Symbol, group.Key.Strategy, "5m", "REGULAR", "ALL", group.Key.Direction,
                        "hour", "session_hour", group.Key.Hour, summary, ct);
                }

                foreach (var group in joined.GroupBy(r => new
                {
                    r.signal.Symbol,
                    r.signal.Strategy,
                    r.signal.Direction,
                    Weekday = QuantSessionClock.ToSession(r.signal.Timestamp, _options.Value).DayOfWeek.ToString()
                }))
                {
                    var summary = SummarizeGroup(group.Select(g => g.outcome).ToArray());
                    await Persist(group.Key.Symbol, group.Key.Strategy, "5m", "REGULAR", "ALL", group.Key.Direction,
                        "weekday", "session_weekday", group.Key.Weekday, summary, ct);
                }
            }

            await RefreshCorrelationsAsync(ct);
            await RefreshMaterializedViewsAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            QuantMeters.CalculationErrors.Add(1);
            _logger.LogWarning(ex, "Statistical aggregation failed");
        }
        finally
        {
            QuantMeters.StatisticsLatency.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    private Task Persist(
        string symbol, string strategy, string timeframe, string session, string regime, string direction,
        string group, string name, string bucket, StatisticalSummary summary, CancellationToken ct)
        => _repository.UpsertObservationAsync(new QuantStatisticalObservation
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            Strategy = strategy,
            Timeframe = timeframe,
            Session = session,
            MarketRegime = regime,
            Direction = direction,
            FeatureGroup = group,
            FeatureName = name,
            FeatureBucket = bucket,
            SampleCount = summary.SampleCount,
            SuccessCount = summary.SuccessCount,
            FailureCount = summary.FailureCount,
            SampleClass = summary.SampleClass,
            SuccessProbability = summary.SuccessProbability,
            ConfidenceLow = summary.ConfidenceLow,
            ConfidenceHigh = summary.ConfidenceHigh,
            ConfidenceLevel = summary.ConfidenceLevel,
            AverageReturn = summary.AverageReturn,
            MedianReturn = summary.MedianReturn,
            StdReturn = summary.StdReturn,
            MinReturn = summary.MinReturn,
            MaxReturn = summary.MaxReturn,
            P25Return = summary.P25,
            P50Return = summary.P50,
            P75Return = summary.P75,
            P90Return = summary.P90,
            P95Return = summary.P95,
            AverageMfe = summary.AverageMfe,
            AverageMae = summary.AverageMae,
            AverageWin = summary.AverageWin,
            AverageLoss = summary.AverageLoss,
            ProfitFactor = summary.ProfitFactor,
            Expectancy = summary.Expectancy,
            ExpectancyR = summary.ExpectancyR,
            SharpeLike = summary.SharpeLike,
            SortinoLike = summary.SortinoLike,
            MaxDrawdown = summary.MaxDrawdown,
            OutcomeHorizon = "5m",
            UpdatedAt = DateTime.UtcNow
        }, ct);

    private StatisticalSummary SummarizeGroup(IReadOnlyList<QuantSignalOutcome> outcomes)
    {
        var returns = outcomes.Select(o => o.Return5m!.Value).ToArray();
        var mfe = outcomes.Select(o => o.Mfe5m).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        var mae = outcomes.Select(o => o.Mae5m).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        var rs = outcomes.Select(o => o.ReturnR).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        return _stats.Summarize(
            returns,
            mfe,
            mae,
            rs,
            _options.Value.MinimumSampleSize,
            _options.Value.LowSampleSize,
            _options.Value.MediumSampleSize,
            _options.Value.ConfidenceLevel);
    }

    private async Task RefreshCorrelationsAsync(CancellationToken ct)
    {
        var symbols = _options.Value.CorrelationSymbols.Select(CandleSymbolAliases.Canonical).Distinct().ToArray();
        var windows = new (string Label, string Storage, int Bars)[]
        {
            ("5m", "M5", 12),
            ("15m", "M15", 16),
            ("30m", "M30", 16),
            ("60m", "H1", 24),
            ("1D", "D1", 30)
        };
        var aliasSet = symbols.SelectMany(CandleSymbolAliases.Expand).Distinct().ToArray();
        var available = await _db.Candles.AsNoTracking()
            .Where(c => aliasSet.Contains(c.Symbol))
            .Select(c => c.Symbol)
            .Distinct()
            .ToListAsync(ct);
        var canonicalAvailable = available.Select(CandleSymbolAliases.Canonical).Distinct().ToArray();
        if (canonicalAvailable.Length < 2)
            return;

        foreach (var (label, storage, bars) in windows)
        {
            var series = new Dictionary<string, List<decimal>>(StringComparer.OrdinalIgnoreCase);
            DateTime? asOf = null;
            foreach (var symbol in canonicalAvailable)
            {
                var aliases = CandleSymbolAliases.Expand(symbol);
                var closes = await _db.Candles.AsNoTracking()
                    .Where(c => aliases.Contains(c.Symbol) && c.Timeframe == storage)
                    .OrderByDescending(c => c.OpenTime)
                    .Take(bars + 1)
                    .Select(c => new { c.Close, c.OpenTime })
                    .ToListAsync(ct);
                closes.Reverse();
                if (closes.Count < 4)
                    continue;
                asOf = closes[^1].OpenTime;
                var rets = new List<decimal>();
                for (var i = 1; i < closes.Count; i++)
                {
                    if (closes[i - 1].Close == 0)
                        continue;
                    rets.Add((closes[i].Close - closes[i - 1].Close) / closes[i - 1].Close);
                }
                if (rets.Count >= 3)
                    series[symbol] = rets;
            }

            var keys = series.Keys.OrderBy(k => k).ToArray();
            for (var i = 0; i < keys.Length; i++)
            for (var j = i + 1; j < keys.Length; j++)
            {
                var a = series[keys[i]];
                var b = series[keys[j]];
                var n = Math.Min(a.Count, b.Count);
                var xa = a.TakeLast(n).ToArray();
                var yb = b.TakeLast(n).ToArray();
                var pearson = PearsonCorrelation.Compute(xa, yb);
                var volA = DescriptiveStats.StdDev(xa);
                var volB = DescriptiveStats.StdDev(yb);
                decimal? beta = volA is null or 0 || pearson is null ? null : pearson * (volB / volA);
                await _repository.UpsertCorrelationAsync(new QuantAssetCorrelation
                {
                    Id = Guid.NewGuid(),
                    Timestamp = asOf ?? DateTime.UtcNow,
                    SymbolA = keys[i],
                    SymbolB = keys[j],
                    Window = label,
                    Pearson = pearson,
                    ReturnA = DescriptiveStats.Mean(xa),
                    ReturnB = DescriptiveStats.Mean(yb),
                    VolatilityA = volA,
                    VolatilityB = volB,
                    Beta = beta,
                    SampleCount = n,
                    CreatedAt = DateTime.UtcNow
                }, ct);
            }
        }
    }

    private async Task RefreshMaterializedViewsAsync(CancellationToken ct)
    {
        if (_db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true)
            return;
        foreach (var view in new[]
        {
            "quant.mv_signal_statistics",
            "quant.mv_opening_statistics",
            "quant.mv_strategy_performance",
            "quant.mv_regime_statistics",
            "quant.mv_hourly_statistics"
        })
        {
            try
            {
                var sql = view switch
                {
                    "quant.mv_signal_statistics" => "REFRESH MATERIALIZED VIEW quant.mv_signal_statistics;",
                    "quant.mv_opening_statistics" => "REFRESH MATERIALIZED VIEW quant.mv_opening_statistics;",
                    "quant.mv_strategy_performance" => "REFRESH MATERIALIZED VIEW quant.mv_strategy_performance;",
                    "quant.mv_regime_statistics" => "REFRESH MATERIALIZED VIEW quant.mv_regime_statistics;",
                    _ => "REFRESH MATERIALIZED VIEW quant.mv_hourly_statistics;"
                };
                await _db.Database.ExecuteSqlRawAsync(sql, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Materialized view {View} not refreshed", view);
            }
        }
    }
}
