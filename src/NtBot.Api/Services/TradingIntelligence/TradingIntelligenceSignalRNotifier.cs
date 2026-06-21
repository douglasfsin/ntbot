using Microsoft.AspNetCore.SignalR;
using NtBot.Api.Hubs;
using NtBot.TradingIntelligence.Models;
using NtBot.TradingIntelligence.Services;

namespace NtBot.Api.Services.TradingIntelligence;

public sealed class TradingIntelligenceSignalRNotifier : ITradingIntelligenceUpdateNotifier
{
    private readonly IHubContext<TradingIntelligenceHub> _hub;

    public TradingIntelligenceSignalRNotifier(IHubContext<TradingIntelligenceHub> hub) => _hub = hub;

    public Task NotifySnapshotUpdatedAsync(TradingIntelligenceSnapshot snapshot, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group("trading_intelligence_all")
            .SendAsync("TradingIntelligenceSnapshotUpdated", snapshot, cancellationToken);
}
