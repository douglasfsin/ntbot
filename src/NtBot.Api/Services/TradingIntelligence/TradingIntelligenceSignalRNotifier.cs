using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NtBot.Api.Hubs;
using NtBot.TradingIntelligence.Models;
using NtBot.TradingIntelligence.Services;

namespace NtBot.Api.Services.TradingIntelligence;

public sealed class TradingIntelligenceSignalRNotifier : ITradingIntelligenceUpdateNotifier
{
    private readonly IHubContext<TradingIntelligenceHub> _hub;
    private readonly ILogger<TradingIntelligenceSignalRNotifier> _logger;

    public TradingIntelligenceSignalRNotifier(
        IHubContext<TradingIntelligenceHub> hub,
        ILogger<TradingIntelligenceSignalRNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifySnapshotUpdatedAsync(
        TradingIntelligenceSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        // Do not link hub writes to the caller's token: HTTP client abort must not
        // fail a refresh after the snapshot was already built.
        try
        {
            await _hub.Clients.Group("trading_intelligence_all")
                .SendAsync("TradingIntelligenceSnapshotUpdated", snapshot, CancellationToken.None);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex,
                "SignalR broadcast canceled for Trading Intelligence snapshot {Asset}",
                snapshot.Asset);
        }
    }
}
