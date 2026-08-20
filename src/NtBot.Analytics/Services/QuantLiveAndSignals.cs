using System.Collections.Concurrent;
using NtBot.Analytics.Configuration;
using NtBot.Analytics.Engines;
using NtBot.Analytics.Maths;
using NtBot.Analytics.Model;
using NtBot.Domain.Entities;
using NtBot.Domain.Entities.Quant;
using NtBot.Infrastructure.Persistence;
using NtBot.Shared.MarketData;
using NtBot.Shared.Normalized;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NtBot.Analytics.Services;

public interface IQuantTickAggregator
{
    void Observe(NormalizedMarketTick tick);
    IReadOnlyList<MarketBar> DrainCompleted();
}

/// <summary>
/// Builds 15s bars from live ticks. Tick-rule classifies aggression when bid/ask exist.
/// Profit ticks often have Volume=0; those bars still record OHLC.
/// </summary>
public sealed class QuantTickBarAggregator : IQuantTickAggregator
{
    private readonly ConcurrentDictionary<string, LiveBar> _bars = new(StringComparer.OrdinalIgnoreCase);
    private readonly IOptions<QuantStatisticsOptions> _options;

    public QuantTickBarAggregator(IOptions<QuantStatisticsOptions> options) => _options = options;

    public void Observe(NormalizedMarketTick tick)
    {
        var price = tick.Last ?? tick.Bid ?? tick.Ask;
        if (price is null or <= 0 || string.IsNullOrWhiteSpace(tick.Symbol))
            return;

        var seconds = Math.Max(1, _options.Value.LiveBarSeconds);
        var bucket = new DateTime(
            tick.TimestampUtc.Year, tick.TimestampUtc.Month, tick.TimestampUtc.Day,
            tick.TimestampUtc.Hour, tick.TimestampUtc.Minute,
            tick.TimestampUtc.Second / seconds * seconds, DateTimeKind.Utc);

        var symbol = CandleSymbolAliases.Canonical(tick.Symbol);
        var key = $"{symbol}|{bucket:o}";
        _bars.AddOrUpdate(key, _ => new LiveBar(symbol, bucket, price.Value, tick), (_, current) =>
        {
            current.Apply(price.Value, tick);
            return current;
        });
    }

    public IReadOnlyList<MarketBar> DrainCompleted()
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-Math.Max(1, _options.Value.LiveBarSeconds));
        var completed = _bars.Where(kv => kv.Value.Start < cutoff).Select(kv => kv.Key).ToArray();
        var bars = new List<MarketBar>();
        foreach (var key in completed)
        {
            if (_bars.TryRemove(key, out var live))
                bars.Add(live.ToBar());
        }
        return bars;
    }

    private sealed class LiveBar
    {
        public LiveBar(string symbol, DateTime start, decimal price, NormalizedMarketTick tick)
        {
            Symbol = symbol;
            Start = start;
            Open = High = Low = Close = price;
            Apply(price, tick);
        }

        public string Symbol { get; }
        public DateTime Start { get; }
        public decimal Open { get; }
        public decimal High { get; private set; }
        public decimal Low { get; private set; }
        public decimal Close { get; private set; }
        public long Volume { get; private set; }
        public int Trades { get; private set; }
        public long Buy { get; private set; }
        public long Sell { get; private set; }
        public decimal? LastBid { get; private set; }
        public decimal? LastAsk { get; private set; }

        public void Apply(decimal price, NormalizedMarketTick tick)
        {
            High = Math.Max(High, price);
            Low = Math.Min(Low, price);
            Close = price;
            Trades++;
            var vol = tick.Volume ?? 1;
            Volume += vol;
            LastBid = tick.Bid ?? LastBid;
            LastAsk = tick.Ask ?? LastAsk;
            var mid = tick.Bid is not null && tick.Ask is not null ? (tick.Bid + tick.Ask) / 2m : null;
            if (mid is not null)
            {
                if (price >= mid)
                    Buy += vol;
                else
                    Sell += vol;
            }
        }

        public MarketBar ToBar() => new(
            Start, Open, High, Low, Close, Volume, Trades, Buy, Sell, Buy, Sell,
            LastBid, LastAsk, null, null, Symbol);
    }
}

public static class CandleBarMapper
{
    public static MarketBar ToBar(Candle candle) => new(
        Timestamp: candle.OpenTime,
        Open: candle.Open,
        High: candle.High,
        Low: candle.Low,
        Close: candle.Close,
        Volume: candle.Volume,
        TradeCount: 0,
        BuyVolume: candle.BuyVolume,
        SellVolume: candle.SellVolume,
        AggressiveBuyVolume: candle.BuyVolume,
        AggressiveSellVolume: candle.SellVolume);

    public static string DisplayTimeframe(string stored) => stored switch
    {
        "M1" => "1m",
        "M5" => "5m",
        "M15" => "15m",
        "M30" => "30m",
        "H1" => "60m",
        "D1" => "1d",
        _ => stored
    };

    public static string StorageTimeframe(string display) => ChartTimeframe.Normalize(display);
}

public interface IQuantSignalRecorder
{
    Task RecordAsync(
        string symbol,
        string strategy,
        string timeframe,
        string direction,
        decimal price,
        decimal? stop,
        decimal? target,
        decimal? score,
        decimal? confidence,
        FeatureSnapshot? features,
        string? traceId,
        CancellationToken ct);
}

public sealed class QuantSignalRecorder : IQuantSignalRecorder
{
    private readonly IQuantRepository _repository;
    private readonly IOptions<QuantStatisticsOptions> _options;
    private readonly ILogger<QuantSignalRecorder> _logger;

    public QuantSignalRecorder(
        IQuantRepository repository,
        IOptions<QuantStatisticsOptions> options,
        ILogger<QuantSignalRecorder> logger)
    {
        _repository = repository;
        _options = options;
        _logger = logger;
    }

    public async Task RecordAsync(
        string symbol,
        string strategy,
        string timeframe,
        string direction,
        decimal price,
        decimal? stop,
        decimal? target,
        decimal? score,
        decimal? confidence,
        FeatureSnapshot? features,
        string? traceId,
        CancellationToken ct)
    {
        if (!_options.Value.Enabled)
            return;

        var utc = features?.Bar.Timestamp ?? DateTime.UtcNow;
        var canonical = CandleSymbolAliases.Canonical(symbol);
        var dir = direction.ToUpperInvariant() switch
        {
            "LONG" => "BUY",
            "SHORT" => "SELL",
            _ => direction.ToUpperInvariant()
        };

        QuantMarketFeature? attached = null;
        if (features is null)
            attached = await _repository.LatestFeatureAsync(canonical, utc, ct);

        using var activity = QuantActivity.Source.StartActivity("SignalGeneration");
        activity?.SetTag("symbol", canonical);
        activity?.SetTag("strategy", strategy);
        activity?.SetTag("direction", dir);

        if (attached is not null)
            LookAheadGuard.EnsureNoFuture(utc, [attached.Timestamp]);

        var signal = new QuantSignalEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = utc,
            Symbol = canonical,
            Strategy = strategy,
            Timeframe = timeframe,
            Direction = dir,
            Price = price,
            StopPrice = stop,
            TargetPrice = target,
            Score = score,
            Confidence = confidence,
            TrendScore = features?.Score.Trend ?? attached?.TrendScore,
            VolumeScore = features?.Score.Volume ?? attached?.VolumeScore,
            DeltaScore = features?.Score.Delta ?? attached?.DeltaScore,
            AggressionScore = features?.Score.Aggression ?? attached?.AggressionScore,
            BookScore = features?.Score.Book ?? attached?.BookScore,
            VwapScore = features?.Score.Vwap ?? attached?.VwapScore,
            VolatilityScore = features?.Score.Volatility ?? attached?.VolatilityScore,
            AuctionScore = features?.Score.Auction ?? attached?.AuctionScore,
            MtfScore = features?.Score.MultiTimeframe ?? attached?.MtfScore,
            CorrelationScore = features?.Score.CrossMarket ?? attached?.CrossMarketScore,
            MarketRegime = features?.MarketRegime ?? attached?.MarketRegime,
            Session = QuantSessionClock.SessionLabel(utc, _options.Value),
            TraceId = traceId,
            FeatureId = attached?.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddSignalAsync(signal, ct);
        _logger.LogInformation(
            "SignalGenerated {Symbol} {Strategy} {Direction} {Score} {TraceId}",
            signal.Symbol, signal.Strategy, signal.Direction, signal.Score, signal.TraceId);
    }
}
