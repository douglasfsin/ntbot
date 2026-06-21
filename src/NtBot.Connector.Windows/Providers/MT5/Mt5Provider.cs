using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Core;
using NtBot.Connector.Windows.Services;
using NtBot.Shared.Normalized;
using System.Text.Json;

namespace NtBot.Connector.Windows.Providers.MT5;

/// <summary>
/// Integração MT5 via Python (Flask + MetaTrader5) em background.
/// Consome SSE e publica ticks normalizados no ingest do Connector.
/// </summary>
public sealed class Mt5Provider : BackgroundService, IBrokerPlugin
{
    private readonly ConnectorOptions _options;
    private readonly IServiceProvider _services;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Mt5Provider> _logger;
    private readonly Mt5PythonHost _pythonHost;

    private Mt5Config _mt5Config = new();
    private CancellationTokenSource? _streamCts;
    private NormalizedAccount? _account;
    private DateTime _lastTickUtc = DateTime.MinValue;
    private bool _connected;
    private string? _statusMessage;

    public Mt5Provider(
        IOptions<ConnectorOptions> options,
        IServiceProvider services,
        IHttpClientFactory httpClientFactory,
        Mt5PythonHost pythonHost,
        ILogger<Mt5Provider> logger)
    {
        _options = options.Value;
        _services = services;
        _httpClientFactory = httpClientFactory;
        _pythonHost = pythonHost;
        _logger = logger;
    }

    public BrokerSource Source => BrokerSource.MT5;
    public string Name => "MetaTrader 5";
    public bool IsConnected => _connected && _pythonHost.IsRunning
        && _lastTickUtc != DateTime.MinValue
        && DateTime.UtcNow - _lastTickUtc < TimeSpan.FromSeconds(60);

    public event Action<NormalizedMarketTick>? OnTick;
    public event Action<NormalizedBrokerStatus>? OnStatusChanged;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableMt5)
        {
            SetStatus(false, "disabled", "MT5 desabilitado em appsettings");
            return;
        }

        _mt5Config = Mt5ConfigLoader.Load(
            AppContext.BaseDirectory,
            _options.Mt5ConfigPath,
            _options);

        if (_mt5Config.Symbols.Count == 0)
        {
            SetStatus(false, "error", "Nenhum símbolo em mt5_config.json");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartPipelineAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                SetStatus(false, "error", ex.Message);
                _logger.LogWarning(ex, "MT5 pipeline falhou — retry em 10s");
                await StopPipelineAsync();
                try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        await StopPipelineAsync();
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (!_options.EnableMt5)
            return;

        if (IsConnected)
            return;

        _logger.LogInformation("Reconectando MT5 Python");
        await StopPipelineAsync();
        await StartPipelineAsync(ct);
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        await StopPipelineAsync();
        SetStatus(false, "stopped", "Desconectado manualmente");
    }

    public Task<IReadOnlyList<NormalizedOrder>> GetOpenOrdersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedOrder>>([]);

    public Task<IReadOnlyList<NormalizedOrder>> GetOrdersAsync(CancellationToken ct) =>
        GetOpenOrdersAsync(ct);

    public Task<IReadOnlyList<NormalizedExecution>> GetRecentExecutionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedExecution>>([]);

    public Task<IReadOnlyList<NormalizedPosition>> GetPositionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedPosition>>([]);

    public Task<NormalizedAccount?> GetAccountAsync(CancellationToken ct) =>
        Task.FromResult(_account);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopPipelineAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task StartPipelineAsync(CancellationToken ct)
    {
        SetStatus(false, "starting", "Iniciando serviço Python MT5…");
        await _pythonHost.StartAsync(_mt5Config, ct);
        await RefreshAccountAsync(ct);

        _streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var streamToken = _streamCts.Token;

        var tasks = _mt5Config.Symbols
            .Select(symbol => Task.Run(() => StreamSymbolAsync(symbol, streamToken), streamToken))
            .ToArray();

        SetStatus(true, "connected", $"MT5 via {_pythonHost.BaseUrl} ({_mt5Config.Symbols.Count} símbolos)");

        await Task.WhenAll(tasks);
    }

    private async Task StopPipelineAsync()
    {
        try { _streamCts?.Cancel(); }
        catch (ObjectDisposedException) { /* ignore */ }

        _streamCts?.Dispose();
        _streamCts = null;
        await _pythonHost.StopAsync();
        _connected = false;
    }

    private async Task StreamSymbolAsync(string symbol, CancellationToken ct)
    {
        var url = $"{_pythonHost.BaseUrl.TrimEnd('/')}/api/stream/tick/{symbol}";
        var attempt = 0;

        while (!ct.IsCancellationRequested)
        {
            attempt++;
            try
            {
                _logger.LogInformation("MT5 SSE conectando {Symbol} (#{Attempt}): {Url}", symbol, attempt, url);
                var client = _httpClientFactory.CreateClient(nameof(Mt5Provider));
                client.Timeout = Timeout.InfiniteTimeSpan;

                await Mt5SseReader.ReadStreamAsync(
                    client,
                    url,
                    async (eventType, json, token) =>
                    {
                        if (!eventType.Equals("tick", StringComparison.OrdinalIgnoreCase))
                            return;

                        var pyTick = Mt5SseReader.ParseTick(json);
                        if (pyTick == null)
                            return;

                        PublishTick(MapTick(pyTick));
                        await Task.CompletedTask;
                    },
                    ct);

                _logger.LogWarning("MT5 SSE {Symbol} encerrado pelo servidor", symbol);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                var delay = Math.Min(30, attempt * 2);
                _logger.LogWarning(ex, "MT5 SSE {Symbol} erro — retry em {Delay}s", symbol, delay);
                try { await Task.Delay(TimeSpan.FromSeconds(delay), ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private void PublishTick(NormalizedMarketTick tick)
    {
        _lastTickUtc = DateTime.UtcNow;
        _services.GetRequiredService<ProviderOrchestrator>().PushTick(tick);
        OnTick?.Invoke(tick);
    }

    private static NormalizedMarketTick MapTick(Mt5PyTick py)
    {
        var last = py.Last > 0 ? (decimal?)py.Last : null;
        var bid = py.Bid > 0 ? (decimal?)py.Bid : null;
        var ask = py.Ask > 0 ? (decimal?)py.Ask : null;

        if (last is null or 0 && bid > 0 && ask > 0)
            last = (bid + ask) / 2m;

        return new NormalizedMarketTick
        {
            Symbol = py.Symbol.ToUpperInvariant(),
            Source = BrokerSource.MT5,
            Last = last,
            Bid = bid,
            Ask = ask,
            Volume = py.Volume > 0 ? py.Volume : null,
            TimestampUtc = DateTime.UtcNow
        };
    }

    private async Task RefreshAccountAsync(CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(Mt5Provider));
            var json = await client.GetStringAsync($"{_pythonHost.BaseUrl}/api/status", ct);
            var status = JsonSerializer.Deserialize<Mt5PyStatus>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (status?.Account == null)
                return;

            _account = new NormalizedAccount
            {
                AccountId = status.Account.Login?.ToString() ?? "mt5",
                Source = BrokerSource.MT5,
                Balance = (decimal)(status.Account.Balance ?? 0),
                Equity = (decimal)(status.Account.Balance ?? 0),
                Currency = status.Account.Currency ?? "USD",
                TimestampUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao obter conta MT5");
        }
    }

    private void SetStatus(bool connected, string status, string? message = null)
    {
        _connected = connected;
        _statusMessage = message;
        OnStatusChanged?.Invoke(new NormalizedBrokerStatus
        {
            Source = BrokerSource.MT5,
            IsConnected = connected,
            Status = status,
            Message = message,
            TimestampUtc = DateTime.UtcNow
        });
    }
}
