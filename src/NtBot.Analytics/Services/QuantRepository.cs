using System.Diagnostics;
using System.Diagnostics.Metrics;
using NtBot.Domain.Entities.Quant;
using NtBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NtBot.Analytics.Services;

public static class QuantActivity
{
    public static readonly ActivitySource Source = new("NtBot.Quant");
}

public static class QuantMeters
{
    public const string Name = "NtBot.Quant";
    private static readonly Meter Meter = new(Name);

    public static readonly Counter<long> FeaturesPersisted = Meter.CreateCounter<long>("quant.features.persisted");
    public static readonly Counter<long> SignalsPersisted = Meter.CreateCounter<long>("quant.signals.persisted");
    public static readonly Counter<long> OutcomesPersisted = Meter.CreateCounter<long>("quant.outcomes.persisted");
    public static readonly Counter<long> StatisticsCalculated = Meter.CreateCounter<long>("quant.statistics.calculation_count");
    public static readonly Counter<long> CalculationErrors = Meter.CreateCounter<long>("quant.calculation.errors");
    public static readonly Counter<long> DatabaseErrors = Meter.CreateCounter<long>("quant.database.errors");
    public static readonly Histogram<double> FeatureLatency = Meter.CreateHistogram<double>("quant.feature.processing_latency");
    public static readonly Histogram<double> OutcomeLatency = Meter.CreateHistogram<double>("quant.outcome.processing_latency");
    public static readonly Histogram<double> StatisticsLatency = Meter.CreateHistogram<double>("quant.statistics.processing_latency");
    public static readonly Histogram<double> MarketVolume = Meter.CreateHistogram<double>("market.volume");
    public static readonly Histogram<double> MarketDelta = Meter.CreateHistogram<double>("market.delta");
    public static readonly Histogram<double> MarketVwap = Meter.CreateHistogram<double>("market.vwap");
    public static readonly Histogram<double> MarketVolatility = Meter.CreateHistogram<double>("market.volatility");
    public static readonly Histogram<double> MarketBookImbalance = Meter.CreateHistogram<double>("market.book_imbalance");
    public static readonly Histogram<double> AuctionScore = Meter.CreateHistogram<double>("auction.score");
    public static readonly Histogram<double> SignalScore = Meter.CreateHistogram<double>("signal.score");
    public static readonly Histogram<double> SignalCount = Meter.CreateHistogram<double>("signal.count");
}

public interface IQuantRepository
{
    Task UpsertFeatureAsync(QuantMarketFeature feature, CancellationToken ct);
    Task UpsertAuctionAsync(QuantOpeningAuction auction, CancellationToken ct);
    Task UpsertOpeningRangeAsync(QuantOpeningRange range, CancellationToken ct);
    Task<QuantSignalEvent> AddSignalAsync(QuantSignalEvent signal, CancellationToken ct);
    Task UpsertOutcomeAsync(QuantSignalOutcome outcome, CancellationToken ct);
    Task UpsertObservationAsync(QuantStatisticalObservation observation, CancellationToken ct);
    Task UpsertCorrelationAsync(QuantAssetCorrelation correlation, CancellationToken ct);
    Task<IReadOnlyList<QuantSignalEvent>> IncompleteSignalsAsync(int take, CancellationToken ct);
    Task<QuantMarketFeature?> LatestFeatureAsync(string symbol, DateTime asOf, CancellationToken ct);
}

public sealed class QuantRepository : IQuantRepository
{
    private readonly NtBotDbContext _db;
    private readonly ILogger<QuantRepository> _logger;

    public QuantRepository(NtBotDbContext db, ILogger<QuantRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task UpsertFeatureAsync(QuantMarketFeature feature, CancellationToken ct)
    {
        var existing = await _db.QuantMarketFeatures.FirstOrDefaultAsync(
            x => x.Symbol == feature.Symbol && x.Timeframe == feature.Timeframe && x.Timestamp == feature.Timestamp, ct);
        if (existing is null)
        {
            _db.QuantMarketFeatures.Add(feature);
        }
        else
        {
            feature.Id = existing.Id;
            _db.Entry(existing).CurrentValues.SetValues(feature);
            existing.CreatedAt = existing.CreatedAt;
        }
        await SaveAsync(ct);
        QuantMeters.FeaturesPersisted.Add(1);
        var tags = new TagList
        {
            { "symbol", feature.Symbol },
            { "timeframe", feature.Timeframe }
        };
        QuantMeters.MarketVolume.Record(feature.Volume, tags);
        if (feature.Delta is not null)
            QuantMeters.MarketDelta.Record((double)feature.Delta.Value, tags);
        if (feature.Vwap is not null)
            QuantMeters.MarketVwap.Record((double)feature.Vwap.Value, tags);
        if (feature.Volatility is not null)
            QuantMeters.MarketVolatility.Record((double)feature.Volatility.Value, tags);
        if (feature.BookImbalance is not null)
            QuantMeters.MarketBookImbalance.Record((double)feature.BookImbalance.Value, tags);
    }

    public async Task UpsertAuctionAsync(QuantOpeningAuction auction, CancellationToken ct)
    {
        var existing = await _db.QuantOpeningAuctions.FirstOrDefaultAsync(
            x => x.Symbol == auction.Symbol && x.Date == auction.Date, ct);
        if (existing is null)
            _db.QuantOpeningAuctions.Add(auction);
        else
        {
            auction.Id = existing.Id;
            _db.Entry(existing).CurrentValues.SetValues(auction);
        }
        await SaveAsync(ct);
        QuantMeters.AuctionScore.Record(auction.AuctionScore, new TagList { { "symbol", auction.Symbol } });
    }

    public async Task UpsertOpeningRangeAsync(QuantOpeningRange range, CancellationToken ct)
    {
        var existing = await _db.QuantOpeningRanges.FirstOrDefaultAsync(
            x => x.Symbol == range.Symbol && x.Date == range.Date && x.RangeWindow == range.RangeWindow, ct);
        if (existing is null)
            _db.QuantOpeningRanges.Add(range);
        else
        {
            range.Id = existing.Id;
            _db.Entry(existing).CurrentValues.SetValues(range);
        }
        await SaveAsync(ct);
    }

    public async Task<QuantSignalEvent> AddSignalAsync(QuantSignalEvent signal, CancellationToken ct)
    {
        var duplicate = await _db.QuantSignalEvents.AnyAsync(
            x => x.Symbol == signal.Symbol
                 && x.Strategy == signal.Strategy
                 && x.Timestamp == signal.Timestamp
                 && x.Direction == signal.Direction, ct);
        if (duplicate)
            return signal;

        _db.QuantSignalEvents.Add(signal);
        _db.QuantSignalOutcomes.Add(new QuantSignalOutcome
        {
            Id = Guid.NewGuid(),
            SignalId = signal.Id,
            Symbol = signal.Symbol,
            Direction = signal.Direction,
            SignalPrice = signal.Price,
            CreatedAt = DateTime.UtcNow
        });
        await SaveAsync(ct);
        QuantMeters.SignalsPersisted.Add(1);
        QuantMeters.SignalCount.Record(1, new TagList { { "symbol", signal.Symbol }, { "strategy", signal.Strategy } });
        if (signal.Score is not null)
            QuantMeters.SignalScore.Record((double)signal.Score.Value, new TagList { { "symbol", signal.Symbol } });
        return signal;
    }

    public async Task UpsertOutcomeAsync(QuantSignalOutcome outcome, CancellationToken ct)
    {
        var existing = await _db.QuantSignalOutcomes.FirstOrDefaultAsync(x => x.SignalId == outcome.SignalId, ct);
        if (existing is null)
            _db.QuantSignalOutcomes.Add(outcome);
        else
        {
            outcome.Id = existing.Id;
            _db.Entry(existing).CurrentValues.SetValues(outcome);
        }
        await SaveAsync(ct);
        QuantMeters.OutcomesPersisted.Add(1);
    }

    public async Task UpsertObservationAsync(QuantStatisticalObservation observation, CancellationToken ct)
    {
        var existing = await _db.QuantStatisticalObservations.FirstOrDefaultAsync(x =>
            x.Symbol == observation.Symbol
            && x.Strategy == observation.Strategy
            && x.Timeframe == observation.Timeframe
            && x.Session == observation.Session
            && x.MarketRegime == observation.MarketRegime
            && x.Direction == observation.Direction
            && x.FeatureGroup == observation.FeatureGroup
            && x.FeatureName == observation.FeatureName
            && x.FeatureBucket == observation.FeatureBucket
            && x.OutcomeHorizon == observation.OutcomeHorizon, ct);
        if (existing is null)
            _db.QuantStatisticalObservations.Add(observation);
        else
        {
            observation.Id = existing.Id;
            _db.Entry(existing).CurrentValues.SetValues(observation);
        }
        await SaveAsync(ct);
        QuantMeters.StatisticsCalculated.Add(1);
    }

    public async Task UpsertCorrelationAsync(QuantAssetCorrelation correlation, CancellationToken ct)
    {
        var existing = await _db.QuantAssetCorrelations.FirstOrDefaultAsync(x =>
            x.SymbolA == correlation.SymbolA
            && x.SymbolB == correlation.SymbolB
            && x.Window == correlation.Window
            && x.Timestamp == correlation.Timestamp, ct);
        if (existing is null)
            _db.QuantAssetCorrelations.Add(correlation);
        else
        {
            correlation.Id = existing.Id;
            _db.Entry(existing).CurrentValues.SetValues(correlation);
        }
        await SaveAsync(ct);
    }

    public async Task<QuantMarketFeature?> LatestFeatureAsync(string symbol, DateTime asOf, CancellationToken ct)
        => await _db.QuantMarketFeatures.AsNoTracking()
            .Where(f => f.Symbol == symbol && f.Timestamp <= asOf)
            .OrderByDescending(f => f.Timestamp)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<QuantSignalEvent>> IncompleteSignalsAsync(int take, CancellationToken ct)
    {
        var rows = await _db.QuantSignalEvents
            .Include(s => s.Outcome)
            .Where(s => s.Outcome == null || !s.Outcome.Complete)
            .OrderBy(s => s.Timestamp)
            .Take(take)
            .ToListAsync(ct);
        return rows;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            QuantMeters.DatabaseErrors.Add(1);
            _logger.LogError(ex, "Quant persistence failed");
            throw;
        }
    }
}
