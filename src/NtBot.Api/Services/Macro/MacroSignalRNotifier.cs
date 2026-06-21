using Microsoft.AspNetCore.SignalR;
using NtBot.Api.Hubs;
using NtBot.Macro.DTO;
using NtBot.Macro.Services;

namespace NtBot.Api.Services.Macro;

public sealed class MacroSignalRNotifier : IMacroUpdateNotifier
{
    private readonly IHubContext<MacroHub> _hub;

    public MacroSignalRNotifier(IHubContext<MacroHub> hub)
    {
        _hub = hub;
    }

    public async Task NotifySnapshotUpdatedAsync(MacroSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _hub.Clients.Group("macro_all").SendAsync("MacroSnapshotUpdated", snapshot, cancellationToken);
        await _hub.Clients.Group("macro_all").SendAsync("MacroScoreUpdated", new
        {
            snapshot.MacroScore,
            snapshot.Confidence,
            snapshot.Timestamp
        }, cancellationToken);
        await _hub.Clients.Group("macro_all").SendAsync("MacroRecommendationsUpdated", snapshot.Recommendations, cancellationToken);
        if (snapshot.UpcomingEvents.Count > 0)
        {
            await _hub.Clients.Group("macro_all").SendAsync("MacroEventsUpdated", snapshot.UpcomingEvents, cancellationToken);
        }
    }

    public async Task NotifyProvidersUpdatedAsync(IReadOnlyList<MacroProviderStatusDto> providers, CancellationToken cancellationToken = default)
    {
        await _hub.Clients.Group("macro_all").SendAsync("MacroProvidersUpdated", providers, cancellationToken);
    }
}
