using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Core;
using NtBot.Connector.Windows.MarketData;
using NtBot.Connector.Windows.Providers.Profit;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.Workers;

public sealed class ProviderWatchdogWorker : BackgroundService
{
    private readonly IEnumerable<IBrokerPlugin> _plugins;
    private readonly IProviderHealth _health;
    private readonly IOptionsMonitor<ConnectorOptions> _connectorOptions;
    private readonly IProfitMarketDataModeController _profitMode;
    private readonly MarketDataGatewayOptions _gatewayOptions;
    private readonly ILogger<ProviderWatchdogWorker> _logger;
    private readonly Dictionary<string, DateTime> _lastActionUtc = new(StringComparer.OrdinalIgnoreCase);

    public ProviderWatchdogWorker(
        IEnumerable<IBrokerPlugin> plugins,
        IProviderHealth health,
        IOptionsMonitor<ConnectorOptions> connectorOptions,
        IProfitMarketDataModeController profitMode,
        IOptions<MarketDataGatewayOptions> gatewayOptions,
        ILogger<ProviderWatchdogWorker> logger)
    {
        _plugins = plugins;
        _health = health;
        _connectorOptions = connectorOptions;
        _profitMode = profitMode;
        _gatewayOptions = gatewayOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var warning = TimeSpan.FromMilliseconds(_gatewayOptions.WarningStaleMs);
        var reconnect = TimeSpan.FromMilliseconds(_gatewayOptions.ReconnectStaleMs);
        var restart = TimeSpan.FromMilliseconds(_gatewayOptions.RestartStaleMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var plugin in _plugins)
            {
                if (!IsWatchdogTarget(plugin))
                    continue;

                var snapshot = _health.GetSnapshot(plugin.Source, ProviderHealthLevel.Healthy, reconnect);
                if (!snapshot.LastTickUtc.HasValue)
                    continue;

                var age = DateTime.UtcNow - snapshot.LastTickUtc.Value;
                if (age < warning)
                    continue;

                if (age >= restart)
                    await TryRestartAsync(plugin, stoppingToken);
                else if (age >= reconnect)
                    await TryReconnectAsync(plugin, stoppingToken);
                else if (age >= warning)
                    _logger.LogDebug("{Provider} sem tick há {Seconds:N0}s (warning)", plugin.Name, age.TotalSeconds);
            }

            await Task.Delay(_gatewayOptions.WatchdogIntervalMs, stoppingToken);
        }
    }

    private async Task TryReconnectAsync(IBrokerPlugin plugin, CancellationToken ct)
    {
        if (!CanAct(plugin.Name, TimeSpan.FromSeconds(10)))
            return;

        _logger.LogWarning("{Provider} stale — reconectando", plugin.Name);
        _health.RecordReconnect(plugin.Source);

        try
        {
            await plugin.ConnectAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _health.RecordFailure(plugin.Source, ex.Message);
            _logger.LogWarning(ex, "Falha ao reconectar {Provider}", plugin.Name);
        }
    }

    private async Task TryRestartAsync(IBrokerPlugin plugin, CancellationToken ct)
    {
        if (!CanAct(plugin.Name, TimeSpan.FromSeconds(30)))
            return;

        _logger.LogWarning("{Provider} stale crítico — reinicializando", plugin.Name);
        _health.RecordReconnect(plugin.Source);

        try
        {
            await plugin.DisconnectAsync(ct);
            await Task.Delay(500, ct);
            await plugin.ConnectAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _health.RecordFailure(plugin.Source, ex.Message);
            _logger.LogWarning(ex, "Falha ao reinicializar {Provider}", plugin.Name);
        }
    }

    private bool CanAct(string pluginName, TimeSpan cooldown)
    {
        if (_lastActionUtc.TryGetValue(pluginName, out var last) && DateTime.UtcNow - last < cooldown)
            return false;

        _lastActionUtc[pluginName] = DateTime.UtcNow;
        return true;
    }

    private bool IsWatchdogTarget(IBrokerPlugin plugin)
    {
        var mode = _profitMode.Current;

        if (plugin is ProfitRtdWorker)
            return mode == ProfitMarketDataMode.Rtd
                   || (mode == ProfitMarketDataMode.Dde && _connectorOptions.CurrentValue.AllowRtdFallbackWhenDdeStale);

        if (plugin is ProfitDdeProvider)
            return mode == ProfitMarketDataMode.Dde;

        if (plugin is ProfitDllProvider)
            return mode == ProfitMarketDataMode.ProfitDll;

        return plugin.Source switch
        {
            BrokerSource.Profit => true,
            BrokerSource.MT5 => _connectorOptions.CurrentValue.EnableMt5,
            BrokerSource.NinjaTrader => _connectorOptions.CurrentValue.EnableNinjaTrader,
            BrokerSource.TradingView => _connectorOptions.CurrentValue.EnableTradingView,
            _ => false
        };
    }
}
