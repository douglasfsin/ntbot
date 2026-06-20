using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Core;
using NtBot.Connector.Windows.Services;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.Providers.Profit;

public class ProfitRtdWorker : BackgroundService, IBrokerPlugin
{
    private readonly ConnectorOptions _options;
    private readonly IServiceProvider _services;
    private readonly ILogger<ProfitRtdWorker> _logger;
    private readonly ConcurrentDictionary<string, NormalizedPosition> _positions = new();
    private readonly object _rtdLock = new();

    private Thread? _rtdThread;
    private ProfitRtdComClient? _rtdClient;
    private CancellationTokenSource? _rtdLoopCts;
    private TaskCompletionSource<bool>? _rtdReady;
    private CancellationToken _hostStoppingToken;
    private decimal _balance;
    private decimal _pnl;
    private bool _connected;
    private string? _statusMessage;

    public ProfitRtdWorker(
        IOptions<ConnectorOptions> options,
        IServiceProvider services,
        ILogger<ProfitRtdWorker> logger)
    {
        _options = options.Value;
        _services = services;
        _logger = logger;
    }

    public BrokerSource Source => BrokerSource.Profit;
    public string Name => "ProfitChart RTD";
    public bool IsConnected => _connected;

    public event Action<NormalizedMarketTick>? OnTick;
    public event Action<NormalizedBrokerStatus>? OnStatusChanged;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _hostStoppingToken = stoppingToken;

        if (!_options.EnableProfit)
        {
            _logger.LogInformation("Profit RTD desabilitado via configuração");
            SetStatus(false, "disabled", "Desabilitado em appsettings");
            return;
        }

        _logger.LogInformation("Profit RTD worker iniciando (thread STA COM)");
        SetStatus(false, "starting", "Iniciando RTD COM…");

        try
        {
            await StartRtdThreadAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            SetStatus(false, "error", ex.Message);
            _logger.LogError(ex, "Falha fatal no Profit RTD");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                PublishTicks();
                UpdateConnectionState();
            }
            catch (Exception ex)
            {
                SetStatus(false, "error", ex.Message);
                _logger.LogWarning(ex, "Erro no ciclo Profit RTD");
            }

            await Task.Delay(250, stoppingToken);
        }
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (!_options.EnableProfit)
            return;

        if (IsRtdThreadRunning())
        {
            SetStatus(_connected, _connected ? "connected" : "waiting", _statusMessage);
            return;
        }

        _logger.LogInformation("Reconectando Profit RTD COM");
        SetStatus(false, "reconnecting", "Reconectando RTD COM…");

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_hostStoppingToken, ct);
            await StartRtdThreadAsync(linked.Token);
        }
        catch (Exception ex)
        {
            SetStatus(false, "error", ex.Message);
            _logger.LogWarning(ex, "Falha ao reconectar Profit RTD");
            throw;
        }
    }

    public Task DisconnectAsync(CancellationToken ct)
    {
        _logger.LogInformation("Desconectando Profit RTD COM");
        StopRtdThread();
        SetStatus(false, "stopped", "Desconectado manualmente");
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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        StopRtdThread();
        await base.StopAsync(cancellationToken);
    }

    private bool IsRtdThreadRunning()
    {
        lock (_rtdLock)
            return _rtdThread?.IsAlive == true;
    }

    private ProfitRtdComClient? GetRtdClient()
    {
        lock (_rtdLock)
            return _rtdClient;
    }

    private Task StartRtdThreadAsync(CancellationToken stoppingToken)
    {
        lock (_rtdLock)
        {
            StopRtdThreadInternal();
            _rtdLoopCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        }

        var loopToken = _rtdLoopCts!.Token;
        _rtdReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var configPath = Path.Combine(AppContext.BaseDirectory, _options.ProfitRtdConfigPath);

        var thread = new Thread(() =>
        {
            ProfitRtdComClient? client = null;
            try
            {
                client = new ProfitRtdComClient(configPath, _logger);
                client.Start();

                lock (_rtdLock)
                    _rtdClient = client;

                _rtdReady.TrySetResult(true);

                while (!loopToken.IsCancellationRequested)
                {
                    client.Poll();
                    Thread.Sleep(50);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Thread STA Profit RTD falhou");
                _rtdReady.TrySetException(ex);
            }
            finally
            {
                try
                {
                    client?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Erro ao dispose RTD client");
                }

                lock (_rtdLock)
                {
                    if (ReferenceEquals(_rtdClient, client))
                        _rtdClient = null;
                }
            }
        })
        {
            IsBackground = true,
            Name = "ProfitRtdSta"
        };

        thread.SetApartmentState(ApartmentState.STA);

        lock (_rtdLock)
            _rtdThread = thread;

        thread.Start();

        return _rtdReady.Task.WaitAsync(TimeSpan.FromSeconds(30), stoppingToken);
    }

    private void StopRtdThread()
    {
        lock (_rtdLock)
            StopRtdThreadInternal();
    }

    private void StopRtdThreadInternal()
    {
        try
        {
            _rtdLoopCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // ignore
        }

        var thread = _rtdThread;
        if (thread?.IsAlive == true && !thread.Join(TimeSpan.FromSeconds(5)))
            _logger.LogWarning("Thread Profit RTD não encerrou em 5s");

        _rtdLoopCts?.Dispose();
        _rtdLoopCts = null;
        _rtdThread = null;
    }

    private void PublishTicks()
    {
        var client = GetRtdClient();
        if (client == null)
            return;

        foreach (var (logical, last) in client.LastPrices)
        {
            var tick = new NormalizedMarketTick
            {
                Symbol = logical,
                Source = BrokerSource.Profit,
                Last = last,
                Bid = last - 5m,
                Ask = last + 5m,
                Volume = 0,
                TimestampUtc = DateTime.UtcNow
            };

            _services.GetRequiredService<ProviderOrchestrator>().PushTick(tick);
            OnTick?.Invoke(tick);

            if (_positions.TryGetValue(logical, out var pos))
            {
                _positions[logical] = pos with
                {
                    UnrealizedPnL = (last - pos.AveragePrice) * pos.Quantity,
                    TimestampUtc = DateTime.UtcNow
                };
            }
        }

        _pnl = _positions.Values.Sum(p => p.UnrealizedPnL);
    }

    private void UpdateConnectionState()
    {
        var client = GetRtdClient();
        if (client == null)
        {
            if (!IsRtdThreadRunning())
                SetStatus(false, "disconnected", "Cliente RTD indisponível");
            return;
        }

        if (client.DataCount > 0 && client.LastDataUtc.HasValue)
        {
            var age = DateTime.UtcNow - client.LastDataUtc.Value;
            var connected = age < TimeSpan.FromSeconds(30);
            var price = client.LastPrices.GetValueOrDefault("WIN", client.LastPrices.Values.FirstOrDefault());
            SetStatus(
                connected,
                connected ? "connected" : "stale",
                connected
                    ? $"WIN @ {price:N0} ({client.DataCount} ticks)"
                    : $"Sem dados há {age.TotalSeconds:N0}s");
            return;
        }

        if (client.IsConnected)
        {
            SetStatus(false, "waiting", client.LastError ?? "Aguardando dados do Profit…");
            return;
        }

        SetStatus(false, "disconnected", client.LastError ?? "RTD não conectado");
    }

    private void SetStatus(bool connected, string status, string? message)
    {
        _connected = connected;
        _statusMessage = message;
        OnStatusChanged?.Invoke(new NormalizedBrokerStatus
        {
            Source = BrokerSource.Profit,
            IsConnected = connected,
            Status = status,
            Message = message,
            TimestampUtc = DateTime.UtcNow
        });
    }
}
