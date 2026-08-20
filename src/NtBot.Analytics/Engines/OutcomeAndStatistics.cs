using NtBot.Analytics.Maths;
using NtBot.Analytics.Model;

namespace NtBot.Analytics.Engines;

public interface IOutcomeEngine
{
    OutcomeSnapshot Calculate(
        string direction,
        decimal entry,
        decimal? stopPrice,
        decimal? targetPrice,
        IReadOnlyDictionary<string, HorizonPath> horizons);
}

public sealed class OutcomeEngine : IOutcomeEngine
{
    public static readonly string[] Horizons = ["15s", "30s", "1m", "5m", "15m", "30m", "60m"];

    public OutcomeSnapshot Calculate(
        string direction,
        decimal entry,
        decimal? stopPrice,
        decimal? targetPrice,
        IReadOnlyDictionary<string, HorizonPath> horizons)
    {
        var returns = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
        var mfe = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
        var mae = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);

        decimal? maxAll = null;
        decimal? minAll = null;
        foreach (var key in Horizons)
        {
            if (!horizons.TryGetValue(key, out var path) || !path.Available || path.Price is null)
            {
                returns[key] = null;
                mfe[key] = null;
                mae[key] = null;
                continue;
            }

            var high = path.High ?? path.Price.Value;
            var low = path.Low ?? path.Price.Value;
            returns[key] = OutcomeMath.DirectionalReturn(direction, entry, path.Price.Value);
            mfe[key] = OutcomeMath.Mfe(direction, entry, high, low);
            mae[key] = OutcomeMath.Mae(direction, entry, high, low);
            maxAll = maxAll is null ? high : Math.Max(maxAll.Value, high);
            minAll = minAll is null ? low : Math.Min(minAll.Value, low);
        }

        var stopDistance = stopPrice is null ? (decimal?)null : Math.Abs(entry - stopPrice.Value);
        var ret5 = returns.GetValueOrDefault("5m");
        var complete = Horizons.All(h => !horizons.TryGetValue(h, out var p) || p.Available);

        bool? targetHit = null;
        bool? stopHit = null;
        if (maxAll is not null && minAll is not null && targetPrice is not null)
        {
            targetHit = OutcomeMath.IsBuy(direction)
                ? maxAll >= targetPrice
                : minAll <= targetPrice;
        }
        if (maxAll is not null && minAll is not null && stopPrice is not null)
        {
            stopHit = OutcomeMath.IsBuy(direction)
                ? minAll <= stopPrice
                : maxAll >= stopPrice;
        }

        return new OutcomeSnapshot
        {
            Direction = direction,
            Entry = entry,
            StopDistance = stopDistance,
            Returns = returns,
            Mfe = mfe,
            Mae = mae,
            MaxPrice = maxAll,
            MinPrice = minAll,
            TargetHit = targetHit,
            StopHit = stopHit,
            Success5m = ret5 is null ? null : ret5 > 0,
            ReturnPoints = ret5,
            ReturnPercent = ret5 is null || entry == 0 ? null : ret5 / entry * 100m,
            ReturnR = OutcomeMath.ReturnR(ret5 ?? 0, stopDistance),
            Complete = complete && ret5 is not null
        };
    }
}

public interface IStatisticalEngine
{
    StatisticalSummary Summarize(
        IReadOnlyList<decimal> returns,
        IReadOnlyList<decimal>? mfe = null,
        IReadOnlyList<decimal>? mae = null,
        IReadOnlyList<decimal>? returnsR = null,
        int minimumSampleSize = 30,
        int lowSampleSize = 100,
        int mediumSampleSize = 500,
        decimal confidenceLevel = 0.95m);
}

public sealed class StatisticalEngine : IStatisticalEngine
{
    public StatisticalSummary Summarize(
        IReadOnlyList<decimal> returns,
        IReadOnlyList<decimal>? mfe = null,
        IReadOnlyList<decimal>? mae = null,
        IReadOnlyList<decimal>? returnsR = null,
        int minimumSampleSize = 30,
        int lowSampleSize = 100,
        int mediumSampleSize = 500,
        decimal confidenceLevel = 0.95m)
    {
        var n = returns.Count;
        var wins = returns.Count(r => r > 0);
        var losses = returns.Count(r => r < 0);
        var sampleClass = BucketClassifier.SampleSize(n, minimumSampleSize, lowSampleSize, mediumSampleSize);
        var reliable = n >= minimumSampleSize;
        var (low, high) = reliable ? WilsonScore.Interval(wins, n, confidenceLevel) : (0m, 0m);
        var winReturns = returns.Where(r => r > 0).ToArray();
        var lossReturns = returns.Where(r => r < 0).ToArray();
        var pWin = n == 0 ? 0 : (decimal)wins / n;
        var pLoss = n == 0 ? 0 : (decimal)losses / n;
        var avgWin = DescriptiveStats.Mean(winReturns) ?? 0;
        var avgLoss = DescriptiveStats.Mean(lossReturns) ?? 0;

        return new StatisticalSummary
        {
            SampleCount = n,
            SuccessCount = wins,
            FailureCount = losses,
            SampleClass = sampleClass,
            SuccessProbability = reliable ? (n == 0 ? 0 : (decimal)wins / n) : null,
            ConfidenceLow = reliable ? low : null,
            ConfidenceHigh = reliable ? high : null,
            ConfidenceLevel = confidenceLevel,
            AverageReturn = DescriptiveStats.Mean(returns),
            MedianReturn = DescriptiveStats.Median(returns),
            StdReturn = DescriptiveStats.StdDev(returns),
            MinReturn = n == 0 ? null : returns.Min(),
            MaxReturn = n == 0 ? null : returns.Max(),
            P25 = DescriptiveStats.Percentile(returns, 25),
            P50 = DescriptiveStats.Percentile(returns, 50),
            P75 = DescriptiveStats.Percentile(returns, 75),
            P90 = DescriptiveStats.Percentile(returns, 90),
            P95 = DescriptiveStats.Percentile(returns, 95),
            AverageMfe = mfe is null ? null : DescriptiveStats.Mean(mfe),
            AverageMae = mae is null ? null : DescriptiveStats.Mean(mae),
            AverageWin = winReturns.Length == 0 ? null : avgWin,
            AverageLoss = lossReturns.Length == 0 ? null : avgLoss,
            ProfitFactor = ExpectancyMath.ProfitFactor(returns),
            Expectancy = n == 0 ? null : ExpectancyMath.Expectancy(pWin, avgWin, pLoss, avgLoss),
            ExpectancyR = returnsR is { Count: > 0 } ? DescriptiveStats.Mean(returnsR) : null,
            SharpeLike = ExpectancyMath.SharpeLike(returns),
            SortinoLike = ExpectancyMath.SortinoLike(returns),
            MaxDrawdown = ExpectancyMath.MaxDrawdown(returns)
        };
    }
}

public interface IBacktestEngine
{
    Task<BacktestSkeletonResult> PrepareAsync(CancellationToken cancellationToken = default);
}

public sealed class BacktestSkeletonResult
{
    public bool Implemented { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Placeholder so historical replay can be plugged in later without changing trading rules.
/// </summary>
public sealed class BacktestEngineStub : IBacktestEngine
{
    public Task<BacktestSkeletonResult> PrepareAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new BacktestSkeletonResult
        {
            Implemented = false,
            Message = "Historical replay uses FeatureEngine + OutcomeEngine + StatisticalEngine. A full simulator is not in this release."
        });
}
