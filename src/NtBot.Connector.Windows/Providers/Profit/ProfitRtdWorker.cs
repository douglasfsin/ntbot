using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Core;
using NtBot.Connector.Windows.Services;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.Providers.Profit;

public class ProfitRtdWorker : BackgroundService, IBrokerPlugin
{
    private readonly ConnectorOptions _options;
    private readonly ProviderOrchestrator _orchestrator;
    private readonly ILogger<ProfitRtdWorker> _logger;
    private readonly ConcurrentDictionary<string, NormalizedMarketTick> _ticks = new();
    private readonly ConcurrentDictionary<string, NormalizedPosition> _positions = new();

    private decimal _balance;
    private decimal _pnl;
    private bool _connected;

    public ProfitRtdWorker(
        IOptions<ConnectorOptions> options,
        ProviderOrchestrator orchestrator,
        ILogger<ProfitRtdWorker> logger)
    {
        _options = options.Value;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public BrokerSource Source => BrokerSource.Profit;
    public string Name => "ProfitChart RTD";
    public bool IsConnected => _connected;

    public event Action<NormalizedMarketTick>? OnTick;
    public event Action<NormalizedBrokerStatus>? OnStatusChanged;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableProfit)
        {
            _logger.LogInformation("Profit RTD desabilitado via configuração");
            return;
        }

        _logger.LogInformation("Profit RTD worker iniciado");
        SetStatus(true, "polling");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollRtdAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                SetStatus(false, ex.Message);
                _logger.LogWarning(ex, "Erro no poll RTD Profit");
            }

            await Task.Delay(500, stoppingToken);
        }
    }

    public Task ConnectAsync(CancellationToken ct)
    {
        SetStatus(true, "started");
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken ct)
    {
        SetStatus(false, "stopped");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NormalizedOrder>> GetOpenOrdersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedOrder>>([]);

    public Task<IReadOnlyList<NormalizedOrder>> GetOrdersAsync(CancellationToken ct) =>
        GetOpenOrdersAsync(ct);

    public Task<IReadOnlyList<NormalizedExecution>> GetRecentExecutionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedExecution>>([]);

    public Task<IReadOnlyList<NormalizedPosition>> GetPositionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedPosition>>(_positions.Values.ToList());

    public Task<NormalizedAccount?> GetAccountAsync(CancellationToken ct) =>
        Task.FromResult<NormalizedAccount?>(new NormalizedAccount
        {
            AccountId = "profit-default",
            Source = BrokerSource.Profit,
            Balance = _balance,
            Equity = _balance + _pnl,
            Margin = 0,
            FreeMargin = _balance + _pnl,
            Currency = "BRL",
            TimestampUtc = DateTime.UtcNow
        });

    private async Task PollRtdAsync(CancellationToken ct)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, _options.ProfitRtdConfigPath);
        if (!File.Exists(configPath))
        {
            _logger.LogDebug("rtd_config.json não encontrado em {Path}", configPath);
            return;
        }

        await using var stream = File.OpenRead(configPath);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("tickers", out var tickers)) return;

        foreach (var ticker in tickers.EnumerateArray())
        {
            var logical = ticker.GetProperty("logical").GetString() ?? "UNKNOWN";
            var simulatedLast = SimulatePrice(logical);

            var tick = new NormalizedMarketTick
            {
                Symbol = logical,
                Source = BrokerSource.Profit,
                Last = simulatedLast,
                Bid = simulatedLast - 0.25m,
                Ask = simulatedLast + 0.25m,
                Volume = Random.Shared.NextInt64(100, 5000),
                TimestampUtc = DateTime.UtcNow
            };

            _ticks[logical] = tick;
            _orchestrator.PushTick(tick);
            OnTick?.Invoke(tick);

            if (_positions.TryGetValue(logical, out var pos))
            {
                _positions[logical] = pos with
                {
                    UnrealizedPnL = (simulatedLast - pos.AveragePrice) * pos.Quantity,
                    TimestampUtc = DateTime.UtcNow
                };
            }
        }

        _balance = 100_000m;
        _pnl = _positions.Values.Sum(p => p.UnrealizedPnL);
        SetStatus(true, "connected");
    }

    private void SetStatus(bool connected, string message)
    {
        _connected = connected;
        OnStatusChanged?.Invoke(new NormalizedBrokerStatus
        {
            Source = BrokerSource.Profit,
            IsConnected = connected,
            Status = connected ? "connected" : "disconnected",
            Message = message,
            TimestampUtc = DateTime.UtcNow
        });
    }

    private static decimal SimulatePrice(string symbol) =>
        symbol switch
        {
            "MNQ" => 18500m + Random.Shared.Next(-50, 50),
            "MES" => 5200m + Random.Shared.Next(-20, 20),
            _ => 100m + Random.Shared.Next(-5, 5)
        };
}
