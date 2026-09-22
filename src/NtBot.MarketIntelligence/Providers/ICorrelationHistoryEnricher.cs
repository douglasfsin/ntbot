using NtBot.MarketIntelligence.Models;

namespace NtBot.MarketIntelligence.Providers;

public interface ICorrelationHistoryEnricher
{
    Task EnrichHistoryAsync(
        IDictionary<string, IReadOnlyList<PriceHistoryPoint>> history,
        CancellationToken cancellationToken = default);
}
