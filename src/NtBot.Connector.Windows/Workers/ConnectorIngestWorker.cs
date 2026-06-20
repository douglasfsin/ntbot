using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Services;
using NtBot.Connector.Windows.SignalR;

namespace NtBot.Connector.Windows.Workers;

public class ConnectorIngestWorker : BackgroundService
{
    private readonly ProviderOrchestrator _orchestrator;
    private readonly INtBotApiClient _api;
    private readonly ConnectorOptions _options;
    private readonly ILogger<ConnectorIngestWorker> _logger;

    public ConnectorIngestWorker(
        ProviderOrchestrator orchestrator,
        INtBotApiClient api,
        IOptions<ConnectorOptions> options,
        ILogger<ConnectorIngestWorker> logger)
    {
        _orchestrator = orchestrator;
        _api = api;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _api.EnsureSessionAsync(stoppingToken);
        _logger.LogInformation("Connector ingest worker ativo");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batch = await _orchestrator.CollectAsync(stoppingToken);
                if (HasPayload(batch))
                    await _api.SendIngestAsync(batch, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Falha no ciclo de ingest");
            }

            await Task.Delay(_options.IngestIntervalMs, stoppingToken);
        }
    }

    private static bool HasPayload(NtBot.Shared.Normalized.NormalizedIngestBatch batch) =>
        batch is { IsDelta: false }
        || batch.Ticks?.Count > 0
        || batch.Positions?.Count > 0
        || batch.Orders?.Count > 0
        || batch.Executions?.Count > 0
        || batch.Signals?.Count > 0
        || batch.Account != null
        || batch.BrokerStatuses?.Count > 0;
}
