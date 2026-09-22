using NtBot.MarketIntelligence.Models;

namespace NtBot.MarketIntelligence.Providers;

public interface IMarketOverviewEnricher
{
    Task<MarketOverview> EnrichAsync(MarketOverview overview, CancellationToken cancellationToken = default);
}
