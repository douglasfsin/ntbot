using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Core;
using NtBot.Connector.Windows.Services;
using NtBot.Connector.Windows.SignalR;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.Workers;

/// <summary>
/// Supervisiona plugins habilitados e reconecta automaticamente com backoff exponencial.
/// </summary>
public sealed class BrokerSupervisorWorker : BackgroundService
{
    private readonly IEnumerable<IBrokerPlugin> _plugins;
    private readonly IPlatformStatusRegistry _registry;
    private readonly INtBotApiClient _api;
    private readonly IOptionsMonitor<ConnectorOptions> _options;
    private readonly IProfitMarketDataModeController _profitMode;
    private readonly ILogger<BrokerSupervisorWorker> _logger;
    private readonly Dictionary<string, int> _attempts = new(StringComparer.OrdinalIgnoreCase);

    public BrokerSupervisorWorker(
        IEnumerable<IBrokerPlugin> plugins,
        IPlatformStatusRegistry registry,
        INtBotApiClient api,
        IOptionsMonitor<ConnectorOptions> options,
        IProfitMarketDataModeController profitMode,
        ILogger<BrokerSupervisorWorker> logger)
    {
        _plugins = plugins;
        _registry = registry;
        _api = api;
        _options = options;
        _profitMode = profitMode;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RegisterHandlers();

        while (!stoppingToken.IsCancellationRequested)
        {
            await SuperviseApiAsync(stoppingToken);

            foreach (var plugin in _plugins)
            {
                if (!IsPluginEnabled(plugin) || stoppingToken.IsCancellationRequested)
                    continue;

                // Profit gerencia conexão no próprio worker RTD
                if (plugin.Source is BrokerSource.Profit or BrokerSource.MT5)
                    continue;

                if (!plugin.IsConnected)
                    await TryReconnectPluginAsync(plugin, stoppingToken);
            }

            await Task.Delay(3000, stoppingToken);
        }
    }

    private void RegisterHandlers()
    {
        foreach (var plugin in _plugins)
        {
            var enabled = IsPluginEnabled(plugin);
            plugin.OnStatusChanged += status =>
            {
                var attempts = _attempts.GetValueOrDefault(plugin.Name);
                _registry.UpdatePlatform(plugin.Name, plugin.Source, IsPluginEnabled(plugin), status, attempts);
            };

            _registry.UpdatePlatform(
                plugin.Name,
                plugin.Source,
                enabled,
                new NormalizedBrokerStatus
                {
                    Source = plugin.Source,
                    IsConnected = plugin.IsConnected,
                    Status = enabled ? "starting" : "disabled",
                    Message = enabled ? null : "Desabilitado em appsettings",
                    TimestampUtc = DateTime.UtcNow
                });
        }
    }

    private async Task SuperviseApiAsync(CancellationToken ct)
    {
        if (!_api.IsConfigured)
        {
            _registry.UpdateApi(false, "offline", "ApiKey não configurada");
            return;
        }

        try
        {
            await _api.EnsureSessionAsync(ct);
            _registry.UpdateApi(_api.IsOnline, _api.IsOnline ? "connected" : "connecting");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _registry.UpdateApi(false, "error", ex.Message);
        }
    }

    private async Task TryReconnectPluginAsync(IBrokerPlugin plugin, CancellationToken ct)
    {
        _attempts.TryGetValue(plugin.Name, out var attempt);
        attempt++;

        var delay = Math.Min(
            _options.CurrentValue.ReconnectBaseDelayMs * (int)Math.Pow(2, Math.Min(attempt - 1, 6)),
            _options.CurrentValue.MaxReconnectDelayMs);

        _logger.LogInformation(
            "Reconectando {Plugin} (tentativa {Attempt}, delay {Delay}ms)",
            plugin.Name, attempt, delay);

        _registry.UpdatePlatform(
            plugin.Name,
            plugin.Source,
            true,
            new NormalizedBrokerStatus
            {
                Source = plugin.Source,
                IsConnected = false,
                Status = "reconnecting",
                Message = $"Tentativa {attempt} em {delay}ms",
                TimestampUtc = DateTime.UtcNow
            },
            attempt);

        await Task.Delay(delay, ct);

        try
        {
            await plugin.ConnectAsync(ct);
            if (plugin.IsConnected)
            {
                _attempts[plugin.Name] = 0;
                _logger.LogInformation("{Plugin} reconectado", plugin.Name);
            }
            else
            {
                _attempts[plugin.Name] = attempt;
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _attempts[plugin.Name] = attempt;
            _logger.LogWarning(ex, "Falha ao reconectar {Plugin}", plugin.Name);
            _registry.UpdatePlatform(
                plugin.Name,
                plugin.Source,
                true,
                new NormalizedBrokerStatus
                {
                    Source = plugin.Source,
                    IsConnected = false,
                    Status = "error",
                    Message = ex.Message,
                    TimestampUtc = DateTime.UtcNow
                },
                attempt);
        }
    }

    private bool IsPluginEnabled(IBrokerPlugin plugin)
    {
        var mode = _profitMode.Current;
        return plugin.Source switch
        {
            BrokerSource.Profit when plugin.Name.Contains("DDE", StringComparison.OrdinalIgnoreCase) =>
                mode == ProfitMarketDataMode.Dde,
            BrokerSource.Profit when plugin.Name.Contains("DLL", StringComparison.OrdinalIgnoreCase) =>
                mode == ProfitMarketDataMode.ProfitDll,
            BrokerSource.Profit =>
                mode == ProfitMarketDataMode.Rtd
                || (mode == ProfitMarketDataMode.Dde && _options.CurrentValue.AllowRtdFallbackWhenDdeStale),
            BrokerSource.MT5 => _options.CurrentValue.EnableMt5,
            BrokerSource.NinjaTrader => _options.CurrentValue.EnableNinjaTrader,
            BrokerSource.TradingView => _options.CurrentValue.EnableTradingView,
            _ => false
        };
    }
}
