using NtBot.Analytics.Engines;
using NtBot.Analytics.Maths;
using NtBot.Analytics.Model;

namespace NtBot.UnitTests.Analytics;

public class FeatureEngineTests
{
    private readonly FeatureEngine _engine = new();

    [Fact]
    public void Delta_UsesBuyMinusSellWhenAvailable()
    {
        var bars = Bars(10, (i, bar) => bar with { BuyVolume = 80, SellVolume = 20, Volume = 100 });
        var snapshot = _engine.Compute(bars);
        Assert.Equal(60m, snapshot.Delta);
    }

    [Fact]
    public void Imbalance_IsBidOverTotal()
    {
        var bars = Bars(8, (_, bar) => bar with { BidVolume = 70, AskVolume = 30 });
        var snapshot = _engine.Compute(bars);
        Assert.Equal(0.7m, snapshot.BookImbalance);
    }

    [Fact]
    public void Vwap_IsVolumeWeightedTypicalPrice()
    {
        var bars = new List<MarketBar>
        {
            new(Utc(1), 10, 10, 10, 10, 100),
            new(Utc(2), 12, 12, 12, 12, 100)
        };
        var snapshot = _engine.Compute(bars);
        Assert.Equal(11m, snapshot.Vwap);
    }

    [Fact]
    public void ZScore_IsZeroWhenFlatWindow()
    {
        var bars = Bars(20, (_, bar) => bar);
        var snapshot = _engine.Compute(bars);
        Assert.Equal(0m, snapshot.VolumeZscore);
    }

    [Fact]
    public void LookAhead_ThrowsWhenFutureBarPresent()
    {
        var bars = new List<MarketBar>
        {
            new(Utc(2), 10, 11, 9, 10, 10),
            new(Utc(1), 10, 11, 9, 10, 10)
        };
        Assert.Throws<InvalidOperationException>(() => _engine.Compute(bars));
    }

    private static DateTime Utc(int minute) => new(2026, 8, 17, 12, minute, 0, DateTimeKind.Utc);

    private static List<MarketBar> Bars(int count, Func<int, MarketBar, MarketBar> mutate)
    {
        var list = new List<MarketBar>();
        for (var i = 1; i <= count; i++)
        {
            var bar = new MarketBar(Utc(i), 100, 101, 99, 100, 50);
            list.Add(mutate(i, bar));
        }
        return list;
    }
}

public class OutcomeEngineTests
{
    private readonly OutcomeEngine _engine = new();

    [Fact]
    public void BuyReturn_IsFutureMinusEntry()
    {
        var snapshot = _engine.Calculate("BUY", 100, 95, 110, new Dictionary<string, HorizonPath>
        {
            ["5m"] = new() { Available = true, Price = 104, High = 105, Low = 99 }
        });
        Assert.Equal(4m, snapshot.Returns["5m"]);
        Assert.Equal(5m, snapshot.Mfe["5m"]);
        Assert.Equal(-1m, snapshot.Mae["5m"]);
    }

    [Fact]
    public void SellReturn_IsEntryMinusFuture()
    {
        var snapshot = _engine.Calculate("SELL", 100, 105, 90, new Dictionary<string, HorizonPath>
        {
            ["5m"] = new() { Available = true, Price = 96, High = 101, Low = 95 }
        });
        Assert.Equal(4m, snapshot.Returns["5m"]);
        Assert.Equal(5m, snapshot.Mfe["5m"]);
        Assert.Equal(-1m, snapshot.Mae["5m"]);
    }
}

public class StatisticalEngineTests
{
    private readonly StatisticalEngine _engine = new();

    [Fact]
    public void InsufficientSample_HidesProbability()
    {
        var summary = _engine.Summarize(Enumerable.Repeat(1m, 10).ToArray());
        Assert.Equal("INSUFFICIENT_SAMPLE", summary.SampleClass);
        Assert.Null(summary.SuccessProbability);
    }

    [Fact]
    public void WinRateAndExpectancy()
    {
        var returns = Enumerable.Repeat(2m, 20).Concat(Enumerable.Repeat(-1m, 20)).ToArray();
        var summary = _engine.Summarize(returns, minimumSampleSize: 30);
        Assert.Equal(0.5m, summary.SuccessProbability);
        Assert.Equal(0.5m, summary.Expectancy);
        Assert.Equal(2m, summary.ProfitFactor);
        Assert.Equal(0.5m, summary.MedianReturn);
    }

    [Fact]
    public void WilsonInterval_IsInsideUnitRange()
    {
        var (low, high) = WilsonScore.Interval(73, 100);
        Assert.True(low > 0.6m && low < 0.73m);
        Assert.True(high > 0.73m && high < 0.85m);
    }
}

public class AuctionScoreTests
{
    [Fact]
    public void StrongPositiveInputs_ClassifyBuy()
    {
        var moderate = AuctionScoreCalculator.Score(
            new AuctionScoreInput(2.5m, 2m, 0.8m, 0.4m, -0.5m, 40m, 50m, 0.4m),
            FeatureEngine.DefaultAuctionWeights);
        Assert.Equal("BUY", BucketClassifier.Auction(moderate));
        Assert.InRange(moderate, 20, 59);

        var strong = AuctionScoreCalculator.Score(
            new AuctionScoreInput(3m, 3m, 1m, 1.5m, -2m, 100m, 100m, 2m),
            FeatureEngine.DefaultAuctionWeights);
        Assert.Equal("STRONG_BUY", BucketClassifier.Auction(strong));
        Assert.InRange(strong, 60, 100);
    }
}

public class DataIntegrityTests
{
    [Fact]
    public void QualityRejectsNonPositivePrice()
    {
        var errors = DataQuality.ValidateBar("WIN", DateTime.UtcNow, 0, 1, 1, 1, 10, 1, 1);
        Assert.Contains("price_invalid", errors);
    }

    [Fact]
    public void Pearson_PerfectCorrelation()
    {
        var x = new decimal[] { 1, 2, 3, 4, 5 };
        Assert.Equal(1m, PearsonCorrelation.Compute(x, x));
    }

    [Fact]
    public void Outcome_DoesNotUseFutureForMaeSignOnBuy()
    {
        var snapshot = new OutcomeEngine().Calculate("BUY", 100, null, null, new Dictionary<string, HorizonPath>
        {
            ["1m"] = new() { Available = true, Price = 101, High = 102, Low = 98 }
        });
        Assert.True(snapshot.Mae["1m"] <= 0);
        Assert.True(snapshot.Mfe["1m"] >= 0);
    }
}

public class PercentileAndDrawdownTests
{
    [Fact]
    public void Percentile_P50_MatchesMedianOnOddSet()
    {
        var values = new decimal[] { 1, 2, 3, 4, 5 };
        Assert.Equal(3m, DescriptiveStats.Percentile(values, 50));
        Assert.Equal(3m, DescriptiveStats.Median(values));
    }

    [Fact]
    public void MaxDrawdown_TracksPeakToTrough()
    {
        var returns = new decimal[] { 1, 1, -3, 1 };
        Assert.Equal(-3m, ExpectancyMath.MaxDrawdown(returns));
    }

    [Fact]
    public void VolumeBuckets_CoverConfiguredRanges()
    {
        Assert.Equal("GT_3", BucketClassifier.VolumeZ(3.1m));
        Assert.Equal("EXTREME_BUY", BucketClassifier.Delta(2.4m));
        Assert.Equal("0.70-0.80", BucketClassifier.BookImbalance(0.71m));
        Assert.Equal("VERY_HIGH", BucketClassifier.Volatility(90m));
    }
}
