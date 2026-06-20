using Microsoft.AspNetCore.SignalR;
using NtBot.Api.Hubs;
using NtBot.Connector.Services;
using NtBot.Shared.Normalized;

namespace NtBot.Api.Services.Connector;

public class ConnectorEventPublisher : IConnectorEventPublisher
{
    private readonly IHubContext<MarketHub> _marketHub;
    private readonly IHubContext<ConnectorHub> _connectorHub;

    public ConnectorEventPublisher(IHubContext<MarketHub> marketHub, IHubContext<ConnectorHub> connectorHub)
    {
        _marketHub = marketHub;
        _connectorHub = connectorHub;
    }

    public async Task PublishBatchAsync(Guid tenantId, NormalizedIngestBatch batch, CancellationToken ct = default)
    {
        var tenantGroup = $"tenant_{tenantId}";

        if (batch.Ticks?.Count > 0)
        {
            foreach (var tick in batch.Ticks)
            {
                await _marketHub.Clients.Group($"ticks_{tick.Symbol}").SendAsync("TickUpdate", tick, ct);
                await _marketHub.Clients.Group($"market_{tick.Source}").SendAsync("MarketTick", tick, ct);
            }
        }

        await _connectorHub.Clients.Group(tenantGroup).SendAsync("ConnectorBatch", batch, ct);
    }
}
