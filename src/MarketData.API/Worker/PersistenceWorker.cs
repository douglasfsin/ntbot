using Marketdata.Database.Client;
using Marketdata.Database.Models;
using MarketData.API.Worker;

namespace MarketData.API.Worker;

public class PersistenceWorker : BackgroundService
{
    private readonly IQuestDbClient _questDbClient;
    private readonly ILogger<PersistenceWorker> _logger;
    private readonly int _batchSize = 1000;
    private readonly TimeSpan _maxWaitTime = TimeSpan.FromSeconds(1);

    public PersistenceWorker(IQuestDbClient questDbClient, ILogger<PersistenceWorker> logger)
    {
        _questDbClient = questDbClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PersistenceWorker started.");
        
        var batch = new List<TradeRecord>(_batchSize);

        try
        {
            var channelReader = ChannelProvider.Channel.Reader;

            while (!stoppingToken.IsCancellationRequested)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(_maxWaitTime);

                try
                {
                    // Wait for the next item or timeout
                    var item = await channelReader.ReadAsync(cts.Token);
                    batch.Add(item);

                    // Drain the channel up to batch size
                    while (batch.Count < _batchSize && channelReader.TryRead(out var additionalItem))
                    {
                        batch.Add(additionalItem);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Timeout reached, flush what we have
                }

                if (batch.Count > 0)
                {
                    try
                    {
                        await _questDbClient.PublishTradesAsync(batch);
                        batch.Clear();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to publish trades to QuestDB.");
                        // Optional: we can clear the batch or keep it depending on retry policy.
                        // Here we clear it to avoid memory leak if QuestDB is down indefinitely.
                        batch.Clear();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "PersistenceWorker crashed.");
        }
        
        _logger.LogInformation("PersistenceWorker stopped.");
    }
}
