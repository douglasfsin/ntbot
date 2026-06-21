using Microsoft.AspNetCore.SignalR;
using NtBot.Api.Hubs;
using NtBot.MarketDrivers.Models;
using NtBot.MarketDrivers.Services;

namespace NtBot.Api.Services.MarketDrivers;

public sealed class MarketDriversSignalRNotifier : IMarketDriversUpdateNotifier
{
    private readonly IHubContext<MarketDriversHub> _hub;

    public MarketDriversSignalRNotifier(IHubContext<MarketDriversHub> hub) => _hub = hub;

    public Task NotifySnapshotUpdatedAsync(MarketDriversSnapshot snapshot, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group("market_drivers_all").SendAsync("MarketDriversSnapshotUpdated", snapshot, cancellationToken);

    public Task NotifyDashboardUpdatedAsync(IReadOnlyList<MarketDriversDashboardItem> items, CancellationToken cancellationToken = default) =>
        _hub.Clients.Group("market_drivers_all").SendAsync("MarketDriversDashboardUpdated", items, cancellationToken);
}
