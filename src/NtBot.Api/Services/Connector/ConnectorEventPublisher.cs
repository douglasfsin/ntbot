using Microsoft.AspNetCore.SignalR;
using NtBot.Api.Hubs;
using NtBot.Connector.Services;
using NtBot.Shared.Normalized;

namespace NtBot.Api.Services.Connector;

public class ConnectorEventPublisher : IConnectorEventPublisher
{
    private readonly IHubContext<MarketHub> _marketHub;
    private readonly IHubContext<ConnectorHub> _connectorHub;
    private readonly IHubContext<ConnectorWebHub> _connectorWebHub;
    private readonly IHubContext<ProfitChartHub> _profitChartHub;
    private readonly IConnectorLiveState _liveState;

    public ConnectorEventPublisher(
        IHubContext<MarketHub> marketHub,
        IHubContext<ConnectorHub> connectorHub,
        IHubContext<ConnectorWebHub> connectorWebHub,
        IHubContext<ProfitChartHub> profitChartHub,
        IConnectorLiveState liveState)
    {
        _marketHub = marketHub;
        _connectorHub = connectorHub;
        _connectorWebHub = connectorWebHub;
        _profitChartHub = profitChartHub;
        _liveState = liveState;
    }

    public async Task PublishBatchAsync(Guid tenantId, NormalizedIngestBatch batch, CancellationToken ct = default)
    {
        _liveState.ApplyBatch(tenantId, batch);

        var tenantGroup = $"tenant_{tenantId}";

        if (batch.Ticks?.Count > 0)
        {
            foreach (var tick in batch.Ticks)
            {
                var ts = tick.TimestampUtc.ToString("O");

                await _marketHub.Clients.Group($"ticks_{tick.Symbol}").SendAsync("TickUpdate", tick, ct);
                await _marketHub.Clients.Group($"market_{tick.Source}").SendAsync("MarketTick", tick, ct);
                await _marketHub.Clients.Group("market_ProfitChart").SendAsync("MarketTick", tick, ct);

                await _profitChartHub.Clients.All.SendAsync(
                    "TickUpdate", tick.Symbol, "ULT", tick.Last ?? 0m, ts, ct);

                await _connectorWebHub.Clients.Group(tenantGroup).SendAsync("ConnectorTick", new
                {
                    tick.Symbol,
                    Source = tick.Source.ToString(),
                    tick.Last,
                    tick.Bid,
                    tick.Ask,
                    tick.Volume,
                    tick.TimestampUtc
                }, ct);
            }
        }

        await _connectorHub.Clients.Group(tenantGroup).SendAsync("ConnectorBatch", batch, ct);
        await _connectorWebHub.Clients.Group(tenantGroup).SendAsync("ConnectorBatch", batch, ct);
    }
}
