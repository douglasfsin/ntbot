using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.MarketData;
using NtBot.Connector.Windows.Services;

namespace NtBot.Connector.Windows.Workers;

/// <summary>
/// Consome o MarketDataBus, normaliza ticks, atualiza cache e repassa ao ingest em lotes.
/// </summary>
public sealed class MarketDataBatchPublisherWorker : BackgroundService
{
    private readonly IMarketDataBus _bus;
    private readonly IMarketDataCache _cache;
    private readonly ProviderOrchestrator _orchestrator;
    private readonly ProviderMonitor _monitor;
    private readonly IProviderHealth _health;
    private readonly MarketDataGatewayOptions _options;
    private readonly ILogger<MarketDataBatchPublisherWorker> _logger;

    public MarketDataBatchPublisherWorker(
        IMarketDataBus bus,
        IMarketDataCache cache,
        ProviderOrchestrator orchestrator,
        ProviderMonitor monitor,
        IProviderHealth health,
        IOptions<MarketDataGatewayOptions> options,
        ILogger<MarketDataBatchPublisherWorker> logger)
    {
        _bus = bus;
        _cache = cache;
        _orchestrator = orchestrator;
        _monitor = monitor;
        _health = health;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Market Data batch publisher ativo (intervalo {Ms}ms)", _options.BatchIntervalMs);
        var reader = _bus.Reader;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batchDeadline = DateTime.UtcNow.AddMilliseconds(_options.BatchIntervalMs);
                var processed = 0;

                while (DateTime.UtcNow < batchDeadline && !stoppingToken.IsCancellationRequested)
                {
                    if (!reader.TryRead(out var raw))
                    {
                        if (processed == 0)
                            await reader.WaitToReadAsync(stoppingToken);
                        else
                            break;

                        continue;
                    }

                    var tick = MarketTickNormalizer.Normalize(raw);
                    _cache.Upsert(tick);
                    _orchestrator.PushTick(tick.ToNormalized());
                    _health.RecordTick(tick.Provider, tick.Symbol, tick.TimestampUtc);
                    _monitor.RecordProcessedTick(tick);
                    processed++;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Erro no batch publisher");
                await Task.Delay(100, stoppingToken);
            }
        }
    }
}
