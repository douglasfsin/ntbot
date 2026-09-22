using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NtBot.MarketData.Configuration;

namespace NtBot.Api.Services.Profit;

/// <summary>
/// Watchdog — reconexão automática e re-assinatura de tickers stale.
/// </summary>
public sealed class ProfitWatchdogWorker : BackgroundService
{
    private readonly ProfitService _profit;
    private readonly MarketDataApiOptions _options;
    private readonly ILogger<ProfitWatchdogWorker> _logger;

    public ProfitWatchdogWorker(
        ProfitService profit,
        IOptions<MarketDataApiOptions> options,
        ILogger<ProfitWatchdogWorker> logger)
    {
        _profit = profit;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _profit.EnsureHealthyAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Profit watchdog cycle failed");
            }

            // Poll frequently; ProfitService applies its own exponential backoff when offline.
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
