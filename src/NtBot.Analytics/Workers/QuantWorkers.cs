using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.Analytics.Configuration;

namespace NtBot.Analytics.Workers;

public sealed class QuantFeatureWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<QuantStatisticsOptions> _options;
    private readonly ILogger<QuantFeatureWorker> _logger;

    public QuantFeatureWorker(IServiceScopeFactory scopes, IOptions<QuantStatisticsOptions> options, ILogger<QuantFeatureWorker> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
            return;
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var pipeline = scope.ServiceProvider.GetRequiredService<Services.IQuantFeaturePipeline>();
                await pipeline.ProcessLiveBarsAsync(stoppingToken);
                foreach (var symbol in _options.Value.Symbols)
                {
                    await pipeline.ProcessSymbolAsync(symbol, _options.Value.PrimaryFeatureTimeframe, stoppingToken);
                    await pipeline.ProcessSymbolAsync(symbol, "15m", stoppingToken);
                    await pipeline.ProcessAuctionsAsync(symbol, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Quant feature worker cycle failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(15, _options.Value.FeatureRefreshSeconds)), stoppingToken);
        }
    }
}

public sealed class QuantOutcomeWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<QuantStatisticsOptions> _options;
    private readonly ILogger<QuantOutcomeWorker> _logger;

    public QuantOutcomeWorker(IServiceScopeFactory scopes, IOptions<QuantStatisticsOptions> options, ILogger<QuantOutcomeWorker> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
            return;
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<Services.IQuantOutcomeProcessor>().ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Quant outcome worker cycle failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(15, _options.Value.OutcomeRefreshSeconds)), stoppingToken);
        }
    }
}

public sealed class QuantAggregationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<QuantStatisticsOptions> _options;
    private readonly ILogger<QuantAggregationWorker> _logger;

    public QuantAggregationWorker(IServiceScopeFactory scopes, IOptions<QuantStatisticsOptions> options, ILogger<QuantAggregationWorker> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
            return;
        await Task.Delay(TimeSpan.FromSeconds(40), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<Services.IQuantAggregationService>().RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Quant aggregation worker cycle failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(60, _options.Value.StatisticsRefreshSeconds)), stoppingToken);
        }
    }
}
