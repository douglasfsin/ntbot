using System.Collections.Concurrent;
using NtBot.Domain.Entities;
using NtBot.Shared.MarketData;

namespace NtBot.Api.Services.MarketData;

/// <summary>
/// Queues candles for background Postgres flush so chart/request paths never block on DB upserts.
/// </summary>
public interface ICandlePersistQueue
{
    void Enqueue(IEnumerable<Candle> candles);

    /// <summary>Drains a snapshot of queued candles (deduped by symbol/tf/openTime).</summary>
    IReadOnlyList<Candle> Drain();

    int PendingCount { get; }
}

public sealed class CandlePersistQueue : ICandlePersistQueue
{
    private readonly ConcurrentDictionary<string, Candle> _pending = new(StringComparer.OrdinalIgnoreCase);

    public int PendingCount => _pending.Count;

    public void Enqueue(IEnumerable<Candle> candles)
    {
        foreach (var candle in candles)
        {
            if (string.IsNullOrWhiteSpace(candle.Symbol) || candle.OpenTime == default)
                continue;

            var symbol = CandleSymbolAliases.Canonical(candle.Symbol);
            var tf = ChartTimeframe.Normalize(candle.Timeframe);
            var openUtc = candle.OpenTime.Kind == DateTimeKind.Utc
                ? candle.OpenTime
                : candle.OpenTime.ToUniversalTime();

            var key = $"{symbol}|{tf}|{openUtc.Ticks}";
            _pending[key] = new Candle
            {
                Id = candle.Id == Guid.Empty ? Guid.NewGuid() : candle.Id,
                Symbol = symbol,
                Timeframe = tf,
                OpenTime = openUtc,
                CloseTime = candle.CloseTime == default ? openUtc : candle.CloseTime.ToUniversalTime(),
                Open = candle.Open,
                High = candle.High,
                Low = candle.Low,
                Close = candle.Close,
                Volume = candle.Volume,
                CreatedAt = candle.CreatedAt == default ? DateTime.UtcNow : candle.CreatedAt
            };
        }
    }

    public IReadOnlyList<Candle> Drain()
    {
        if (_pending.IsEmpty)
            return [];

        var drained = new List<Candle>(_pending.Count);
        foreach (var key in _pending.Keys.ToArray())
        {
            if (_pending.TryRemove(key, out var candle))
                drained.Add(candle);
        }

        return drained;
    }
}
