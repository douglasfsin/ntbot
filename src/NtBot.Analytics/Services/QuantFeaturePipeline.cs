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

public interface IQuantFeaturePipeline
{
    Task ProcessSymbolAsync(string symbol, string timeframe, CancellationToken ct);
    Task ProcessLiveBarsAsync(CancellationToken ct);
    Task ProcessAuctionsAsync(string symbol, CancellationToken ct);
}

public sealed class QuantFeaturePipeline : IQuantFeaturePipeline
{
    private readonly NtBotDbContext _db;
    private readonly IQuantRepository _repository;
    private readonly IFeatureEngine _features;
    private readonly IQuantTickAggregator _ticks;
    private readonly IOptions<QuantStatisticsOptions> _options;
    private readonly ILogger<QuantFeaturePipeline> _logger;

    public QuantFeaturePipeline(
        NtBotDbContext db,
        IQuantRepository repository,
        IFeatureEngine features,
        IQuantTickAggregator ticks,
        IOptions<QuantStatisticsOptions> options,
        ILogger<QuantFeaturePipeline> logger)
    {
        _db = db;
        _repository = repository;
        _features = features;
        _ticks = ticks;
        _options = options;
        _logger = logger;
    }

    public async Task ProcessSymbolAsync(string symbol, string timeframe, CancellationToken ct)
    {
        using var activity = QuantActivity.Source.StartActivity("FeatureCalculation");
        activity?.SetTag("symbol", symbol);
        activity?.SetTag("timeframe", timeframe);
        var sw = Stopwatch.StartNew();
        try
        {
            var canonical = CandleSymbolAliases.Canonical(symbol);
            var storageTf = CandleBarMapper.StorageTimeframe(timeframe);
            var aliases = CandleSymbolAliases.Expand(canonical);
            var candles = await _db.Candles
                .AsNoTracking()
                .Where(c => aliases.Contains(c.Symbol) && c.Timeframe == storageTf)
                .OrderByDescending(c => c.OpenTime)
                .Take(_options.Value.FeatureLookback)
                .ToListAsync(ct);
            candles.Reverse();
            if (candles.Count < 5)
                return;

            var bars = candles.Select(CandleBarMapper.ToBar).ToList();
            var quality = DataQuality.ValidateBar(canonical, bars[^1].Timestamp, bars[^1].Open, bars[^1].High, bars[^1].Low, bars[^1].Close, bars[^1].Volume, bars[^1].BuyVolume, bars[^1].SellVolume);
            if (quality.Count > 0)
            {
                _logger.LogWarning("Quant data quality {Symbol} {Errors}", canonical, string.Join(",", quality));
                QuantMeters.CalculationErrors.Add(1);
                return;
            }

            var trends = await LoadTrendsAsync(canonical, bars[^1].Timestamp, ct);
            var snapshot = _features.Compute(bars, trends, _options.Value.QuantScoreWeights, _options.Value.ZScoreWindow);
            await _repository.UpsertFeatureAsync(ToEntity(canonical, CandleBarMapper.DisplayTimeframe(storageTf), snapshot), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            QuantMeters.CalculationErrors.Add(1);
            _logger.LogWarning(ex, "Feature pipeline failed for {Symbol} {Timeframe}", symbol, timeframe);
        }
        finally
        {
            QuantMeters.FeatureLatency.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task ProcessLiveBarsAsync(CancellationToken ct)
    {
        foreach (var bar in _ticks.DrainCompleted())
        {
            var history = new List<MarketBar> { bar };
            var snapshot = _features.Compute(history, zScoreWindow: 5);
            await _repository.UpsertFeatureAsync(
                ToEntity(CandleSymbolAliases.Canonical(bar.Symbol), $"{_options.Value.LiveBarSeconds}s", snapshot),
                ct);
        }
    }

    public async Task ProcessAuctionsAsync(string symbol, CancellationToken ct)
    {
        var options = _options.Value;
        var canonical = CandleSymbolAliases.Canonical(symbol);
        var aliases = CandleSymbolAliases.Expand(canonical);
        var sessionDate = DateOnly.FromDateTime(QuantSessionClock.ToSession(DateTime.UtcNow, options).DateTime);
        var openUtc = QuantSessionClock.SessionOpenUtc(sessionDate, options);
        var previous = await _db.Candles.AsNoTracking()
            .Where(c => aliases.Contains(c.Symbol) && c.Timeframe == "D1" && c.OpenTime < openUtc)
            .OrderByDescending(c => c.OpenTime)
            .FirstOrDefaultAsync(ct);
        var opening = await _db.Candles.AsNoTracking()
            .Where(c => aliases.Contains(c.Symbol) && c.Timeframe == "M5" && c.OpenTime >= openUtc)
            .OrderBy(c => c.OpenTime)
            .FirstOrDefaultAsync(ct);
        if (opening is null)
            return;

        var gap = previous is null ? (decimal?)null : opening.Open - previous.Close;
        var historyCandles = await _db.Candles.AsNoTracking()
            .Where(c => aliases.Contains(c.Symbol) && c.Timeframe == "M5" && c.OpenTime <= opening.OpenTime)
            .OrderByDescending(c => c.OpenTime)
            .Take(Math.Max(8, options.FeatureLookback))
            .ToListAsync(ct);
        historyCandles.Reverse();
        var history = historyCandles.Select(CandleBarMapper.ToBar).ToList();
        if (history.Count == 0)
            return;
        LookAheadGuard.EnsureNoFuture(opening.OpenTime, history.Select(b => b.Timestamp));
        var snapshot = _features.Compute(history, scoreWeights: options.QuantScoreWeights, zScoreWindow: Math.Min(20, history.Count));
        var score = AuctionScoreCalculator.Score(new AuctionScoreInput(
            snapshot.DeltaZscore,
            snapshot.VolumeZscore,
            snapshot.BookImbalance,
            previous is { Close: > 0 } && gap is not null ? gap / previous.Close * 100m : null,
            snapshot.DistanceVwapAtr,
            snapshot.TrendStrength,
            snapshot.VolatilityPercentile,
            snapshot.RangeAtrRatio), options.AuctionScoreWeights);

        await _repository.UpsertAuctionAsync(new QuantOpeningAuction
        {
            Id = Guid.NewGuid(),
            Date = sessionDate,
            Symbol = canonical,
            AuctionStart = QuantSessionClock.AuctionStartUtc(sessionDate, options),
            AuctionEnd = QuantSessionClock.AuctionEndUtc(sessionDate, options),
            PreviousClose = previous?.Close,
            IndicativePrice = null,
            EquilibriumPrice = null,
            OpeningPrice = opening.Open,
            GapPoints = gap,
            GapPercent = previous is { Close: > 0 } && gap is not null ? gap / previous.Close * 100m : null,
            Volume = opening.Volume,
            BuyVolume = opening.BuyVolume,
            SellVolume = opening.SellVolume,
            Delta = snapshot.Delta,
            DeltaRatio = snapshot.DeltaRatio,
            BookImbalance = snapshot.BookImbalance,
            Spread = snapshot.Spread,
            Vwap = snapshot.Vwap,
            AuctionScore = score,
            AuctionClassification = BucketClassifier.Auction(score),
            IndicativeDataAvailable = false,
            CreatedAt = DateTime.UtcNow
        }, ct);

        _logger.LogInformation(
            "AuctionEnded {Symbol} {Score} {Class} {IndicativeAvailable}",
            canonical, score, BucketClassifier.Auction(score), false);

        foreach (var minutes in options.OpeningRangeWindowsMinutes)
        {
            var end = openUtc.AddMinutes(minutes);
            var rangeBars = await _db.Candles.AsNoTracking()
                .Where(c => aliases.Contains(c.Symbol) && c.Timeframe == "M5" && c.OpenTime >= openUtc && c.OpenTime < end)
                .OrderBy(c => c.OpenTime)
                .ToListAsync(ct);
            if (rangeBars.Count == 0)
                continue;
            var high = rangeBars.Max(c => c.High);
            var low = rangeBars.Min(c => c.Low);
            var later = await _db.Candles.AsNoTracking()
                .Where(c => aliases.Contains(c.Symbol) && c.Timeframe == "M5" && c.OpenTime >= end)
                .OrderBy(c => c.OpenTime)
                .FirstOrDefaultAsync(ct);
            var breakoutUp = later is not null && later.Close > high;
            var breakoutDown = later is not null && later.Close < low;
            await _repository.UpsertOpeningRangeAsync(new QuantOpeningRange
            {
                Id = Guid.NewGuid(),
                Date = sessionDate,
                Symbol = canonical,
                RangeWindow = $"{minutes}m",
                RangeStart = openUtc,
                RangeEnd = end,
                OpeningPrice = opening.Open,
                High = high,
                Low = low,
                RangePoints = high - low,
                RangePercent = opening.Open == 0 ? null : (high - low) / opening.Open * 100m,
                Volume = rangeBars.Sum(c => c.Volume),
                Delta = rangeBars.Sum(c => (c.BuyVolume ?? 0) - (c.SellVolume ?? 0)),
                BuyVolume = rangeBars.Sum(c => c.BuyVolume ?? 0),
                SellVolume = rangeBars.Sum(c => c.SellVolume ?? 0),
                Vwap = snapshot.Vwap,
                BreakoutUp = breakoutUp,
                BreakoutDown = breakoutDown,
                BreakoutTime = later?.OpenTime,
                OpeningDrive = breakoutUp || breakoutDown,
                CreatedAt = DateTime.UtcNow
            }, ct);
            if (breakoutUp || breakoutDown)
                _logger.LogInformation("OpeningDriveDetected {Symbol} {Window} {Direction}", canonical, minutes, breakoutUp ? "UP" : "DOWN");
        }
    }

    private async Task<Dictionary<string, string>> LoadTrendsAsync(string symbol, DateTime asOf, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (display, storage) in new[] { ("1m", "M1"), ("5m", "M5"), ("15m", "M15"), ("30m", "M30"), ("60m", "H1") })
        {
            var aliases = CandleSymbolAliases.Expand(symbol);
            var closes = await _db.Candles.AsNoTracking()
                .Where(c => aliases.Contains(c.Symbol) && c.Timeframe == storage && c.OpenTime <= asOf)
                .OrderByDescending(c => c.OpenTime)
                .Take(8)
                .Select(c => c.Close)
                .ToListAsync(ct);
            closes.Reverse();
            result[display] = MultiTimeframeAlignment.TrendFromBars(closes);
        }
        return result;
    }

    private QuantMarketFeature ToEntity(string symbol, string timeframe, FeatureSnapshot snapshot)
    {
        var bar = snapshot.Bar;
        return new QuantMarketFeature
        {
            Id = Guid.NewGuid(),
            Timestamp = bar.Timestamp,
            Symbol = CandleSymbolAliases.Canonical(symbol),
            Market = InferMarket(symbol),
            Timeframe = timeframe,
            Open = bar.Open,
            High = bar.High,
            Low = bar.Low,
            Close = bar.Close,
            Volume = bar.Volume,
            TradeCount = bar.TradeCount,
            BuyVolume = bar.BuyVolume,
            SellVolume = bar.SellVolume,
            AggressiveBuyVolume = bar.AggressiveBuyVolume,
            AggressiveSellVolume = bar.AggressiveSellVolume,
            Delta = snapshot.Delta,
            CumulativeDelta = snapshot.CumulativeDelta,
            DeltaRatio = snapshot.DeltaRatio,
            BidVolume = bar.BidVolume,
            AskVolume = bar.AskVolume,
            BookImbalance = snapshot.BookImbalance,
            Spread = snapshot.Spread,
            Vwap = snapshot.Vwap,
            DistanceVwap = snapshot.DistanceVwap,
            DistanceVwapAtr = snapshot.DistanceVwapAtr,
            Atr = snapshot.Atr,
            Range = snapshot.Range,
            RangeAtrRatio = snapshot.RangeAtrRatio,
            Volatility = snapshot.Volatility,
            AverageTradeSize = snapshot.AverageTradeSize,
            LargeTradeCount = snapshot.LargeTradeCount,
            TradeSizePercentile = snapshot.TradeSizePercentile,
            LargeTradeClass = snapshot.LargeTradeClass,
            VolumeZscore = snapshot.VolumeZscore,
            DeltaZscore = snapshot.DeltaZscore,
            VolumePercentile = snapshot.VolumePercentile,
            DeltaPercentile = snapshot.DeltaPercentile,
            MarketRegime = snapshot.MarketRegime,
            TrendDirection = snapshot.TrendDirection,
            TrendStrength = snapshot.TrendStrength,
            MultiTimeframeAlignment = snapshot.MultiTimeframeAlignment,
            Trend1m = snapshot.TimeframeTrends.GetValueOrDefault("1m"),
            Trend5m = snapshot.TimeframeTrends.GetValueOrDefault("5m"),
            Trend15m = snapshot.TimeframeTrends.GetValueOrDefault("15m"),
            Trend30m = snapshot.TimeframeTrends.GetValueOrDefault("30m"),
            Trend60m = snapshot.TimeframeTrends.GetValueOrDefault("60m"),
            Absorption = snapshot.Absorption,
            AbsorptionStrength = snapshot.AbsorptionStrength,
            QuantScore = snapshot.Score.Total,
            TrendScore = snapshot.Score.Trend,
            VolumeScore = snapshot.Score.Volume,
            DeltaScore = snapshot.Score.Delta,
            AggressionScore = snapshot.Score.Aggression,
            BookScore = snapshot.Score.Book,
            VwapScore = snapshot.Score.Vwap,
            VolatilityScore = snapshot.Score.Volatility,
            AuctionScore = snapshot.Score.Auction,
            MtfScore = snapshot.Score.MultiTimeframe,
            CrossMarketScore = snapshot.Score.CrossMarket,
            Session = QuantSessionClock.SessionLabel(bar.Timestamp, _options.Value),
            Source = timeframe.EndsWith('s') ? "live-ticks" : "candles",
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string InferMarket(string symbol) => CandleSymbolAliases.Canonical(symbol) switch
    {
        "WIN" => "B3_IDX",
        "WDO" => "B3_FX",
        "NQ" or "MNQ" or "ES" => "US_IDX",
        "XAUUSD" => "METAL",
        _ => "OTHER"
    };
}
