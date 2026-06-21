using NtBot.MarketIntelligence.Models;

namespace NtBot.MarketIntelligence.Services;

public interface IMarketUpdateNotifier
{
    Task NotifyOverviewUpdatedAsync(MarketOverview overview, CancellationToken cancellationToken = default);
    Task NotifyCorrelationUpdatedAsync(CorrelationResult correlation, CancellationToken cancellationToken = default);
    Task NotifyQuantScoreUpdatedAsync(QuantScore score, CancellationToken cancellationToken = default);
    Task NotifyProvidersUpdatedAsync(IReadOnlyList<MarketProviderStatusDto> providers, CancellationToken cancellationToken = default);
}
