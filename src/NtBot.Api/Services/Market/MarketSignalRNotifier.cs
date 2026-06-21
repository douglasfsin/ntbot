using Microsoft.AspNetCore.SignalR;
using NtBot.Api.Hubs;
using NtBot.MarketIntelligence.Models;
using NtBot.MarketIntelligence.Services;

namespace NtBot.Api.Services.Market;

public sealed class MarketSignalRNotifier : IMarketUpdateNotifier
{
    private readonly IHubContext<MarketIntelligenceHub> _hub;

    public MarketSignalRNotifier(IHubContext<MarketIntelligenceHub> hub) => _hub = hub;

    public Task NotifyOverviewUpdatedAsync(MarketOverview overview, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group("market_all").SendAsync("MarketOverviewUpdated", overview, cancellationToken);

    public Task NotifyCorrelationUpdatedAsync(CorrelationResult correlation, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group("market_all").SendAsync("MarketCorrelationUpdated", correlation, cancellationToken);

    public Task NotifyQuantScoreUpdatedAsync(QuantScore score, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group("market_all").SendAsync("MarketQuantScoreUpdated", score, cancellationToken);

    public Task NotifyProvidersUpdatedAsync(IReadOnlyList<MarketProviderStatusDto> providers, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group("market_all").SendAsync("MarketProvidersUpdated", providers, cancellationToken);
}
