using NtBot.MarketData.Services;
using NtBot.MarketIntelligence.Models;
using NtBot.MarketIntelligence.Providers;

namespace NtBot.Api.Services.MarketData;

/// <summary>
/// Enriquece histórico de correlação com candles D1 do Profit para ativos B3.
/// </summary>
public sealed class B3ProfitCorrelationHistoryEnricher : ICorrelationHistoryEnricher
{
    private readonly IMarketCandleService _candles;
    private readonly ILogger<B3ProfitCorrelationHistoryEnricher> _logger;

    public B3ProfitCorrelationHistoryEnricher(
        IMarketCandleService candles,
        ILogger<B3ProfitCorrelationHistoryEnricher> logger)
    {
        _candles = candles;
        _logger = logger;
    }

    public async Task EnrichHistoryAsync(
        IDictionary<string, IReadOnlyList<PriceHistoryPoint>> history,
        CancellationToken cancellationToken = default)
    {
        foreach (var (symbol, _) in B3EquityCatalog.Symbols)
        {
            if (history.TryGetValue(symbol, out var existing) && existing.Count >= 60)
                continue;

            try
            {
                var result = await _candles.GetCandlesAsync(symbol, 130, "D1", cancellationToken);
                if (result.Candles.Count < 20)
                {
                    _logger.LogDebug("Histórico D1 insuficiente para {Symbol} ({Count} candles)", symbol, result.Candles.Count);
                    continue;
                }

                var points = result.Candles
                    .GroupBy(c => c.OpenTime.Date)
                    .Select(g => g.OrderByDescending(c => c.OpenTime).First())
                    .OrderBy(c => c.OpenTime)
                    .Select(c => new PriceHistoryPoint { Date = c.OpenTime, Close = c.Close })
                    .ToList();

                if (points.Count >= 20)
                {
                    history[symbol] = points;
                    _logger.LogDebug("Correlação: {Symbol} enriquecido com {Count} pontos D1 ({Source})", symbol, points.Count, result.Source);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Falha ao obter histórico D1 Profit para {Symbol}", symbol);
            }
        }
    }
}
