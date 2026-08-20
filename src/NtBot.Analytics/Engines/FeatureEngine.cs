using NtBot.Analytics.Maths;
using NtBot.Analytics.Model;

namespace NtBot.Analytics.Engines;

public interface IFeatureEngine
{
    FeatureSnapshot Compute(
        IReadOnlyList<MarketBar> historyInclusive,
        IReadOnlyDictionary<string, string>? timeframeTrends = null,
        IReadOnlyDictionary<string, decimal>? scoreWeights = null,
        int zScoreWindow = 20,
        bool openingDrive = false,
        bool breakout = false,
        int auctionScore = 0,
        int crossMarketScore = 0);
}

public sealed class FeatureEngine : IFeatureEngine
{
    public FeatureSnapshot Compute(
        IReadOnlyList<MarketBar> historyInclusive,
        IReadOnlyDictionary<string, string>? timeframeTrends = null,
        IReadOnlyDictionary<string, decimal>? scoreWeights = null,
        int zScoreWindow = 20,
        bool openingDrive = false,
        bool breakout = false,
        int auctionScore = 0,
        int crossMarketScore = 0)
    {
        if (historyInclusive.Count == 0)
            throw new ArgumentException("History is required.", nameof(historyInclusive));

        var asOf = historyInclusive[^1].Timestamp;
        LookAheadGuard.EnsureNoFuture(asOf, historyInclusive.Select(b => b.Timestamp));

        var bar = historyInclusive[^1];
        var window = Math.Max(5, zScoreWindow);
        var recent = historyInclusive.TakeLast(window).ToArray();

        var deltas = recent.Select(DeltaOf).ToArray();
        var volumes = recent.Select(b => (decimal)b.Volume).ToArray();
        var ranges = recent.Select(b => b.High - b.Low).ToArray();
        var atr = AverageTrueRange(historyInclusive, 14);
        var vwap = SessionVwap(historyInclusive);
        var range = bar.High - bar.Low;
        var delta = DeltaOf(bar);
        var cumDelta = historyInclusive.Sum(DeltaOf);
        var totalSide = (bar.BuyVolume ?? 0) + (bar.SellVolume ?? 0);
        var deltaRatio = totalSide > 0 ? (decimal?)(delta / totalSide) : null;
        var bookImbalance = BookImbalance(bar);
        var spread = bar.Bid is not null && bar.Ask is not null
            ? (decimal?)(bar.Ask.Value - bar.Bid.Value)
            : null;
        var avgTrade = bar.TradeCount > 0 ? (decimal?)bar.Volume / bar.TradeCount : null;
        var sizes = recent.Where(b => b.TradeCount > 0).Select(b => (decimal)b.Volume / b.TradeCount).ToArray();
        var sizePct = avgTrade is null ? null : DescriptiveStats.PercentileRank(avgTrade.Value, sizes.Length == 0 ? volumes : sizes);
        var volumeZ = DescriptiveStats.ZScore(bar.Volume, volumes);
        var deltaZ = DescriptiveStats.ZScore(delta, deltas);
        var volPct = DescriptiveStats.PercentileRank(bar.Volume, volumes);
        var deltaPct = DescriptiveStats.PercentileRank(delta, deltas);
        var volaPct = DescriptiveStats.PercentileRank(range, ranges);
        var aggression = AggressionZ(bar, recent);
            var (absorption, absorptionStrength) = AbsorptionDetector.Detect(
                aggression,
                volumeZ,
                atr is not null && atr > 0 ? range / atr : null);
        var trendDirection = MultiTimeframeAlignment.TrendFromBars(recent.Select(b => b.Close).ToArray());
        var trendStrength = TrendStrength(recent);
        var regime = RegimeClassifier.Classify(trendDirection, trendStrength, volaPct, openingDrive, breakout);
        var trends = timeframeTrends is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(timeframeTrends, StringComparer.OrdinalIgnoreCase);
        var alignment = MultiTimeframeAlignment.Classify(trends);

        var weights = scoreWeights ?? DefaultScoreWeights;
        var components = new QuantScoreInput(
            Trend: QuantMarketScoreCalculator.ComponentFromZ(trendDirection == "UP" ? trendStrength / 50m : trendDirection == "DOWN" ? -trendStrength / 50m : 0, 1m),
            Volume: QuantMarketScoreCalculator.ComponentFromZ(volumeZ),
            Delta: QuantMarketScoreCalculator.ComponentFromZ(deltaZ),
            Aggression: QuantMarketScoreCalculator.ComponentFromZ(aggression),
            Book: QuantMarketScoreCalculator.ComponentFromZ(
                bookImbalance is null ? null : (decimal?)((bookImbalance.Value - 0.5m) * 4m), 1m),
            Vwap: QuantMarketScoreCalculator.ComponentFromZ(
                vwap is null ? null : (decimal?)((bar.Close - vwap.Value) / (atr is null or 0 ? 1m : atr.Value))),
            Volatility: QuantMarketScoreCalculator.ComponentFromZ(((volaPct ?? 50) - 50m) / 25m, 1m),
            Auction: auctionScore,
            MultiTimeframe: AlignmentScore(alignment),
            CrossMarket: crossMarketScore);
        var (total, _) = QuantMarketScoreCalculator.Score(components, weights);

        return new FeatureSnapshot
        {
            Bar = bar,
            Delta = delta,
            CumulativeDelta = cumDelta,
            DeltaRatio = deltaRatio,
            Vwap = vwap,
            DistanceVwap = vwap is null ? null : bar.Close - vwap,
            DistanceVwapAtr = vwap is null || atr is null or 0 ? null : (bar.Close - vwap) / atr,
            Atr = atr,
            Range = range,
            RangeAtrRatio = atr is null or 0 ? null : range / atr,
            Volatility = volaPct,
            VolumeZscore = volumeZ,
            DeltaZscore = deltaZ,
            VolumePercentile = volPct,
            DeltaPercentile = deltaPct,
            VolatilityPercentile = volaPct,
            AverageTradeSize = avgTrade,
            TradeSizePercentile = sizePct,
            LargeTradeClass = BucketClassifier.LargeTrade(sizePct),
            LargeTradeCount = sizePct >= 90m ? 1 : 0,
            Absorption = absorption,
            AbsorptionStrength = absorptionStrength,
            BookImbalance = bookImbalance,
            Spread = spread,
            TrendDirection = trendDirection,
            TrendStrength = trendStrength,
            MarketRegime = regime,
            TimeframeTrends = trends,
            MultiTimeframeAlignment = alignment,
            Score = new QuantScoreBreakdown
            {
                Total = total,
                Trend = components.Trend,
                Volume = components.Volume,
                Delta = components.Delta,
                Aggression = components.Aggression,
                Book = components.Book,
                Vwap = components.Vwap,
                Volatility = components.Volatility,
                Auction = components.Auction,
                MultiTimeframe = components.MultiTimeframe,
                CrossMarket = components.CrossMarket
            }
        };
    }

    public static readonly Dictionary<string, decimal> DefaultScoreWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Trend"] = 0.10m,
        ["Volume"] = 0.10m,
        ["Delta"] = 0.15m,
        ["Aggression"] = 0.10m,
        ["Book"] = 0.10m,
        ["VWAP"] = 0.10m,
        ["Volatility"] = 0.10m,
        ["Auction"] = 0.10m,
        ["MultiTimeframe"] = 0.10m,
        ["CrossMarket"] = 0.05m
    };

    public static readonly Dictionary<string, decimal> DefaultAuctionWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Delta"] = 0.20m,
        ["Volume"] = 0.15m,
        ["Book"] = 0.15m,
        ["Gap"] = 0.10m,
        ["VWAP"] = 0.10m,
        ["Trend"] = 0.10m,
        ["Volatility"] = 0.10m,
        ["OpeningRange"] = 0.10m
    };

    private static decimal DeltaOf(MarketBar bar)
    {
        if (bar.BuyVolume is not null || bar.SellVolume is not null)
            return (bar.BuyVolume ?? 0) - (bar.SellVolume ?? 0);
        if (bar.AggressiveBuyVolume is not null || bar.AggressiveSellVolume is not null)
            return (bar.AggressiveBuyVolume ?? 0) - (bar.AggressiveSellVolume ?? 0);
        var body = bar.Close - bar.Open;
        if (body == 0 || bar.Volume == 0)
            return 0;
        return body > 0 ? bar.Volume : -bar.Volume;
    }

    private static decimal? SessionVwap(IReadOnlyList<MarketBar> history)
    {
        decimal pv = 0;
        decimal vol = 0;
        foreach (var bar in history)
        {
            var typical = (bar.High + bar.Low + bar.Close) / 3m;
            pv += typical * bar.Volume;
            vol += bar.Volume;
        }
        if (vol == 0)
            return history[^1].Close;
        return pv / vol;
    }

    private static decimal? AverageTrueRange(IReadOnlyList<MarketBar> history, int period)
    {
        if (history.Count < 2)
            return history[0].High - history[0].Low;
        var trs = new List<decimal>();
        for (var i = 1; i < history.Count; i++)
        {
            var cur = history[i];
            var prev = history[i - 1];
            var tr = Math.Max(cur.High - cur.Low, Math.Max(Math.Abs(cur.High - prev.Close), Math.Abs(cur.Low - prev.Close)));
            trs.Add(tr);
        }
        var take = trs.TakeLast(period).ToArray();
        return take.Length == 0 ? null : take.Average();
    }

    private static decimal? BookImbalance(MarketBar bar)
    {
        if (bar.BidVolume is null && bar.AskVolume is null)
            return null;
        var bid = bar.BidVolume ?? 0;
        var ask = bar.AskVolume ?? 0;
        var sum = bid + ask;
        if (sum == 0)
            return 0.5m;
        return (decimal)bid / sum;
    }

    private static decimal? AggressionZ(MarketBar bar, IReadOnlyList<MarketBar> recent)
    {
        var current = (bar.AggressiveBuyVolume ?? 0) - (bar.AggressiveSellVolume ?? 0);
        if (recent.All(b => b.AggressiveBuyVolume is null && b.AggressiveSellVolume is null))
            return DescriptiveStats.ZScore(DeltaOf(bar), recent.Select(DeltaOf).ToArray());
        var series = recent.Select(b => (decimal)((b.AggressiveBuyVolume ?? 0) - (b.AggressiveSellVolume ?? 0))).ToArray();
        return DescriptiveStats.ZScore(current, series);
    }

    private static decimal TrendStrength(IReadOnlyList<MarketBar> recent)
    {
        if (recent.Count < 3)
            return 0;
        var first = recent[0].Close;
        var last = recent[^1].Close;
        if (first == 0)
            return 0;
        return Math.Abs((last - first) / first) * 1000m;
    }

    private static int AlignmentScore(string alignment) => alignment switch
    {
        "FULL_BULLISH_ALIGNMENT" => 100,
        "BULLISH_ALIGNMENT" => 60,
        "FULL_BEARISH_ALIGNMENT" => -100,
        "BEARISH_ALIGNMENT" => -60,
        _ => 0
    };
}
