using NtBot.Domain.Entities;

namespace NtBot.Api.Services.MarketData;

public sealed class CandleFetchResult
{
    public IReadOnlyList<Candle> Candles { get; init; } = [];
    public string Source { get; init; } = "unavailable";

    public bool HasSufficientData(int minimum) => Candles.Count >= minimum;
}

public interface IMarketCandleService
{
    Task<CandleFetchResult> GetCandlesAsync(
        string symbol,
        int count = 100,
        string? timeframe = null,
        CancellationToken cancellationToken = default);

    Task<int> UpsertCandlesAsync(IEnumerable<Candle> candles, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAvailableSymbolsAsync(
        int minimum = 50,
        string? timeframe = null,
        CancellationToken cancellationToken = default);
}
