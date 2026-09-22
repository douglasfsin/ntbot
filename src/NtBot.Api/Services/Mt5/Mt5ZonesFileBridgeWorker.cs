using Microsoft.Extensions.Options;
using NtBot.Api.Configuration;
using NtBot.Api.Services.MarketData;
using NtBot.Shared.MarketData;
using NtBot.TradingIntelligence.Cache;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Services;

namespace NtBot.Api.Services.Mt5;

/// <summary>
/// Periodically dumps zone delim files so MT5 indicators can draw without WebRequest.
/// </summary>
public sealed class Mt5ZonesFileBridgeWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<Mt5ZonesFileBridgeOptions> _options;
    private readonly ILogger<Mt5ZonesFileBridgeWorker> _logger;

    public Mt5ZonesFileBridgeWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<Mt5ZonesFileBridgeOptions> options,
        ILogger<Mt5ZonesFileBridgeWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue;
            if (opts.Enabled)
            {
                try
                {
                    await TickAsync(opts, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "MT5 zones file bridge tick failed");
                }
            }

            var delay = Math.Clamp(opts.RefreshSeconds, 10, 600);
            await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);
        }
    }

    private async Task TickAsync(Mt5ZonesFileBridgeOptions opts, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var tiCache = scope.ServiceProvider.GetRequiredService<ITradingIntelligenceCacheService>();
        var ti = scope.ServiceProvider.GetRequiredService<ITradingIntelligenceService>();
        var prices = scope.ServiceProvider.GetRequiredService<IChartPriceService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IMt5ZonesFileBridge>();

        var tf = ChartTimeframe.ToChartKey(opts.Timeframe);
        var max = opts.MaxZones > 0 ? Math.Clamp(opts.MaxZones, 1, 12) : 0;

        foreach (var raw in opts.Symbols)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var symbol = CandleSymbolAliases.Canonical(raw);
            try
            {
                var snapshot = await tiCache.GetSnapshotAsync(symbol, cancellationToken: ct)
                               ?? tiCache.GetLastKnownSnapshot(symbol);
                if (snapshot is null)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(15));
                    try
                    {
                        snapshot = await ti.GetSnapshotAsync(symbol, cancellationToken: cts.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        _logger.LogDebug("MT5 zones bridge: TI timeout for {Symbol}", symbol);
                    }
                }

                if (snapshot is null)
                    continue;

                decimal? lastPrice = null;
                try
                {
                    var px = await prices.GetPriceAsync(symbol, tenantId: null, ct);
                    if (px?.Price > 0)
                        lastPrice = px.Price;
                }
                catch
                {
                    // optional
                }

                var zones = Mt5ZoneMarkupBuilder.Build(
                    symbol,
                    snapshot,
                    preferredTimeframe: tf,
                    lastPrice: lastPrice,
                    maxZones: max > 0 ? max : null);

                bridge.WriteZonesFile(symbol, tf, zones, snapshot.Timestamp);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "MT5 zones bridge skip {Symbol}", symbol);
            }
        }
    }
}
