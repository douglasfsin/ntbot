using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Api.Services.MarketData;

/// <summary>
/// Flushes queued M1/TF candles to PostgreSQL every ~15 minutes in batches.
/// Chart requests only touch Redis; this worker owns the slow path.
/// </summary>
public sealed class CandlePostgresFlushWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private const int BatchSize = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICandlePersistQueue _queue;
    private readonly ILogger<CandlePostgresFlushWorker> _logger;

    public CandlePostgresFlushWorker(
        IServiceScopeFactory scopeFactory,
        ICandlePersistQueue queue,
        ILogger<CandlePostgresFlushWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FlushAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Candle Postgres flush cycle failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Best-effort drain on shutdown.
        try
        {
            await FlushAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Final candle flush skipped");
        }
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        var pending = _queue.Drain();
        if (pending.Count == 0)
            return;

        _logger.LogInformation("Flushing {Count} queued candles to Postgres", pending.Count);

        using var scope = _scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<NtBotDbContext>>();

        for (var offset = 0; offset < pending.Count; offset += BatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var slice = pending.Skip(offset).Take(BatchSize).ToList();
            try
            {
                await UpsertBatchAsync(dbFactory, slice, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Soft-fail: re-queue and continue so one bad batch does not drop everything.
                _queue.Enqueue(slice);
                _logger.LogWarning(ex, "Candle Postgres batch upsert failed ({Count} bars); re-queued", slice.Count);
            }
        }
    }

    private static async Task UpsertBatchAsync(
        IDbContextFactory<NtBotDbContext> dbFactory,
        IReadOnlyList<Candle> batch,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));

        foreach (var group in batch.GroupBy(c => (c.Symbol, c.Timeframe)))
        {
            timeout.Token.ThrowIfCancellationRequested();

            var openTimes = group.Select(c => c.OpenTime).Distinct().ToList();
            var existingRows = await db.Candles
                .Where(c => c.Symbol == group.Key.Symbol
                            && c.Timeframe == group.Key.Timeframe
                            && openTimes.Contains(c.OpenTime))
                .ToListAsync(timeout.Token);

            var existingByOpen = existingRows.ToDictionary(c => c.OpenTime);

            foreach (var candle in group)
            {
                if (existingByOpen.TryGetValue(candle.OpenTime, out var existing))
                {
                    existing.Open = candle.Open;
                    existing.High = candle.High;
                    existing.Low = candle.Low;
                    existing.Close = candle.Close;
                    existing.Volume = candle.Volume;
                    existing.CloseTime = candle.CloseTime;
                }
                else
                {
                    candle.Id = candle.Id == Guid.Empty ? Guid.NewGuid() : candle.Id;
                    candle.CreatedAt = DateTime.UtcNow;
                    db.Candles.Add(candle);
                    existingByOpen[candle.OpenTime] = candle;
                }
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(timeout.Token);
    }
}
