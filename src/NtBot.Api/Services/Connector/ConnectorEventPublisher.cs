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
    private readonly ILogger<ConnectorEventPublisher> _logger;

    public ConnectorEventPublisher(
        IHubContext<MarketHub> marketHub,
        IHubContext<ConnectorHub> connectorHub,
        IHubContext<ConnectorWebHub> connectorWebHub,
        IHubContext<ProfitChartHub> profitChartHub,
        IConnectorLiveState liveState,
        ILogger<ConnectorEventPublisher> logger)
    {
        _marketHub = marketHub;
        _connectorHub = connectorHub;
        _connectorWebHub = connectorWebHub;
        _profitChartHub = profitChartHub;
        _liveState = liveState;
        _logger = logger;
    }

    public async Task PublishBatchAsync(Guid tenantId, NormalizedIngestBatch batch, string? clientIp = null, CancellationToken ct = default)
    {
        ConnectorIngestDiagnostics.LogBatch(_logger, tenantId, batch, clientIp);

        _liveState.ApplyBatch(tenantId, batch);
        ConnectorIngestDiagnostics.LogLiveState(_logger, tenantId, _liveState.GetSnapshot(tenantId));

        var tenantGroup = $"tenant_{tenantId}";

        if (batch.Ticks?.Count > 0)
        {
            _logger.LogDebug(
                "Connector SignalR publish tenant={TenantId} tickCount={TickCount}",
                tenantId,
                batch.Ticks.Count);

            foreach (var tick in batch.Ticks)
            {
                foreach (var symbol in ConnectorSymbolAliases.Expand(tick.Symbol))
                {
                    var outbound = symbol.Equals(tick.Symbol, StringComparison.OrdinalIgnoreCase)
                        ? tick
                        : tick with { Symbol = symbol };

                    await PublishTickAsync(tenantGroup, outbound, ct);
                }
            }
        }

        await _connectorHub.Clients.Group(tenantGroup).SendAsync("ConnectorBatch", batch, ct);
        await _connectorWebHub.Clients.Group(tenantGroup).SendAsync("ConnectorBatch", batch, ct);
    }

    private async Task PublishTickAsync(string tenantGroup, NormalizedMarketTick tick, CancellationToken ct)
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
