using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Core;
using NtBot.Connector.Windows.MarketData;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.Providers.Profit;

/// <summary>
/// Fonte ProfitDLL — consome MarketData.API via SignalR (/marketHub) e publica no MarketDataBus.
/// </summary>
public sealed class ProfitDllProvider : BackgroundService, IBrokerPlugin
{
    private readonly ConnectorOptions _options;
    private readonly IProfitMarketDataModeController _mode;
    private readonly IMarketDataPublisher _publisher;
    private readonly ProfitMarketDataCoordinator _coordinator;
    private readonly ILogger<ProfitDllProvider> _logger;
    private readonly ConcurrentDictionary<string, QuoteState> _quotes = new(StringComparer.OrdinalIgnoreCase);

    private HubConnection? _connection;
    private bool _connected;
    private string _statusMessage = "idle";

    public ProfitDllProvider(
        IOptions<ConnectorOptions> options,
        IProfitMarketDataModeController mode,
        IMarketDataPublisher publisher,
        ProfitMarketDataCoordinator coordinator,
        ILogger<ProfitDllProvider> logger)
    {
        _options = options.Value;
        _mode = mode;
        _publisher = publisher;
        _coordinator = coordinator;
        _logger = logger;
    }

    public BrokerSource Source => BrokerSource.Profit;
    public string Name => "ProfitDLL";
    public bool IsConnected => _connected;

    public event Action<NormalizedMarketTick>? OnTick;
    public event Action<NormalizedBrokerStatus>? OnStatusChanged;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_mode.Current != ProfitMarketDataMode.ProfitDll)
            {
                await StopHubAsync();
                SetStatus(false, "disabled", $"Modo ativo: {_mode.Current.ToDisplayName()}");
                await _mode.WaitForModeAsync(ProfitMarketDataMode.ProfitDll, stoppingToken);
                continue;
            }

            try
            {
                await EnsureConnectedAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                SetStatus(false, "error", ex.Message);
                _logger.LogWarning(ex, "ProfitDLL hub erro — retry em 5s");
                await StopHubAsync();
                await Task.Delay(5000, stoppingToken);
            }
        }

        await StopHubAsync();
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (_mode.Current != ProfitMarketDataMode.ProfitDll)
        {
            SetStatus(false, "disabled", $"Modo ativo: {_mode.Current.ToDisplayName()}");
            return;
        }

        await EnsureConnectedAsync(ct);
    }

    public Task DisconnectAsync(CancellationToken ct) => StopHubAsync();

    public Task<NormalizedAccount?> GetAccountAsync(CancellationToken ct) =>
        Task.FromResult<NormalizedAccount?>(null);

    public Task<IReadOnlyList<NormalizedOrder>> GetOpenOrdersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedOrder>>([]);

    public Task<IReadOnlyList<NormalizedOrder>> GetOrdersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedOrder>>([]);

    public Task<IReadOnlyList<NormalizedExecution>> GetRecentExecutionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedExecution>>([]);

    public Task<IReadOnlyList<NormalizedPosition>> GetPositionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedPosition>>([]);

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            SetStatus(true, "connected", _statusMessage);
            return;
        }

        var baseUrl = _options.MarketDataApiBaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            SetStatus(false, "error", "MarketDataApiBaseUrl não configurado");
            return;
        }

        await StopHubAsync();

        var hubUrl = $"{baseUrl}/marketHub";
        SetStatus(false, "connecting", $"Conectando {hubUrl}…");

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, o =>
            {
                o.HttpMessageHandlerFactory = _ => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<string, string, object>("tick", OnTickReceived);
        _connection.On<string, double>("price", OnPriceReceived);
        _connection.Reconnected += async _ =>
        {
            _connected = true;
            await ResubscribeAsync(CancellationToken.None);
            SetStatus(true, "connected", "Reconectado ao marketHub");
        };
        _connection.Closed += ex =>
        {
            _connected = false;
            SetStatus(false, "disconnected", ex?.Message ?? "marketHub fechado");
            return Task.CompletedTask;
        };

        await _connection.StartAsync(ct);
        _connected = true;
        await ResubscribeAsync(ct);
        SetStatus(true, "connected", $"ProfitDLL via {hubUrl} ({_options.ProfitDllTickers.Length} tickers)");
        _logger.LogInformation("ProfitDLL conectado em {Hub}", hubUrl);
    }

    private async Task ResubscribeAsync(CancellationToken ct)
    {
        if (_connection?.State != HubConnectionState.Connected)
            return;

        foreach (var ticker in _options.ProfitDllTickers.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            try
            {
                await _connection.InvokeAsync("Subscribe", ticker.Trim().ToUpperInvariant(), 0, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Falha ao assinar {Ticker}", ticker);
            }
        }
    }

    private void OnTickReceived(string ticker, string topic, object value)
    {
        if (_mode.Current != ProfitMarketDataMode.ProfitDll)
            return;

        if (!TryToDecimal(value, out var numeric))
            return;

        ApplyField(ticker, topic, numeric);
    }

    private void OnPriceReceived(string ticker, double price)
    {
        if (_mode.Current != ProfitMarketDataMode.ProfitDll)
            return;

        ApplyField(ticker, "ULT", (decimal)price);
    }

    private void ApplyField(string ticker, string topic, decimal value)
    {
        if (string.IsNullOrWhiteSpace(ticker) || value <= 0)
            return;

        var physical = ticker.Trim().ToUpperInvariant();
        var logical = ToLogicalSymbol(physical);
        var state = _quotes.GetOrAdd(logical, _ => new QuoteState(logical, physical));

        var topicKey = topic.Trim().ToUpperInvariant();
        switch (topicKey)
        {
            case "ULT" or "FEC" or "PRT":
                state.Last = value;
                break;
            case "QC" or "BID":
                state.Bid = value;
                break;
            case "QV" or "ASK":
                state.Ask = value;
                break;
            case "VOL" or "QTT":
                state.Volume = (long)value;
                break;
            case "ABE":
                state.Open = value;
                break;
            case "MAX":
                state.High = value;
                break;
            case "MIN":
                state.Low = value;
                break;
            default:
                return;
        }

        state.UpdatedUtc = DateTime.UtcNow;

        if (state.Last is null or <= 0)
            return;

        _coordinator.RecordDllTick();

        var tick = MarketTickNormalizer.Normalize(new MarketTick
        {
            Provider = BrokerSource.Profit,
            Symbol = logical,
            LastPrice = state.Last,
            Bid = state.Bid,
            Ask = state.Ask,
            Volume = state.Volume,
            Open = state.Open,
            High = state.High,
            Low = state.Low,
            Close = state.Last,
            TimestampUtc = DateTime.UtcNow,
            Source = $"DLL:{physical}"
        });

        _ = _publisher.PublishAsync(tick);
        OnTick?.Invoke(tick.ToNormalized());
        _statusMessage = $"{logical} ULT={state.Last:0.##} via DLL:{physical}";
    }

    private static string ToLogicalSymbol(string physical) => physical.ToUpperInvariant() switch
    {
        "WINFUT" or "WINQ26" => "WIN",
        "WDOFUT" or "DOLFUT" or "WDOQ26" => "WDO",
        _ => physical
    };

    private static bool TryToDecimal(object value, out decimal result)
    {
        result = 0;
        switch (value)
        {
            case decimal m:
                result = m;
                return m > 0;
            case double d:
                result = (decimal)d;
                return d > 0;
            case float f:
                result = (decimal)f;
                return f > 0;
            case int i:
                result = i;
                return i > 0;
            case long l:
                result = l;
                return l > 0;
            case string s:
                return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result) && result > 0
                    || decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out result) && result > 0;
            default:
                return false;
        }
    }

    private async Task StopHubAsync()
    {
        var connection = _connection;
        _connection = null;
        _connected = false;

        if (connection is null)
            return;

        try
        {
            await connection.DisposeAsync();
        }
        catch
        {
            // ignore
        }
    }

    private void SetStatus(bool connected, string status, string? message)
    {
        _connected = connected;
        _statusMessage = message ?? status;
        OnStatusChanged?.Invoke(new NormalizedBrokerStatus
        {
            Source = BrokerSource.Profit,
            IsConnected = connected,
            Status = status,
            Message = message,
            TimestampUtc = DateTime.UtcNow
        });
    }

    private sealed class QuoteState(string logical, string physical)
    {
        public string Logical { get; } = logical;
        public string Physical { get; } = physical;
        public decimal? Last { get; set; }
        public decimal? Bid { get; set; }
        public decimal? Ask { get; set; }
        public long? Volume { get; set; }
        public decimal? Open { get; set; }
        public decimal? High { get; set; }
        public decimal? Low { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }
}
