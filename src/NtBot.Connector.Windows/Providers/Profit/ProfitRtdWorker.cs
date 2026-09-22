using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Core;
using NtBot.Connector.Windows.MarketData;
using NtBot.Connector.Windows.Services;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.Providers.Profit;

public class ProfitRtdWorker : BackgroundService, IBrokerPlugin
{
    private readonly IOptionsMonitor<ConnectorOptions> _options;
    private readonly IProfitMarketDataModeController _mode;
    private readonly IServiceProvider _services;
    private readonly IMarketDataPublisher _publisher;
    private readonly ProfitMarketDataCoordinator _coordinator;
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
        IOptionsMonitor<ConnectorOptions> options,
        IProfitMarketDataModeController mode,
        IServiceProvider services,
        IMarketDataPublisher publisher,
        ProfitMarketDataCoordinator coordinator,
        ILogger<ProfitRtdWorker> logger)
    {
        _options = options;
        _mode = mode;
        _services = services;
        _publisher = publisher;
        _coordinator = coordinator;
        _logger = logger;
    }

    public BrokerSource Source => BrokerSource.Profit;
    public string Name => "ProfitChart RTD";
    public bool IsConnected => _connected;

    public event Action<NormalizedMarketTick>? OnTick;
    public event Action<NormalizedBrokerStatus>? OnStatusChanged;

    private int _rtdRestartAttempt;
    private DateTime _rtdNextRetryUtc = DateTime.MinValue;
    private DateTime _rtdLastWarnUtc = DateTime.MinValue;

    private async Task RunRtdFallbackLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && _mode.Current == ProfitMarketDataMode.Dde)
        {
            try
            {
                if (!IsRtdThreadRunning())
                {
                    if (DateTime.UtcNow < _rtdNextRetryUtc)
                    {
                        await Task.Delay(500, stoppingToken);
                        continue;
                    }

                    try
                    {
                        await StartRtdThreadAsync(stoppingToken);
                        _rtdRestartAttempt = 0;
                    }
                    catch (Exception ex)
                    {
                        _rtdRestartAttempt++;
                        var delay = Math.Min(60, (int)Math.Pow(2, Math.Min(_rtdRestartAttempt, 5)));
                        _rtdNextRetryUtc = DateTime.UtcNow.AddSeconds(delay);

                        if (DateTime.UtcNow - _rtdLastWarnUtc > TimeSpan.FromSeconds(30))
                        {
                            _logger.LogWarning(
                                ex,
                                "RTD fallback indisponível (Profit aberto?) — retry em {Delay}s",
                                delay);
                            _rtdLastWarnUtc = DateTime.UtcNow;
                        }

                        SetStatus(false, "waiting", "Aguardando Profit RTD…");
                        await Task.Delay(500, stoppingToken);
                        continue;
                    }
                }

                if (_coordinator.ShouldPublishRtdFallback())
                    PublishTicks();
                else if (_coordinator.ReplayModeActive
                         && DateTime.UtcNow - _rtdLastWarnUtc > TimeSpan.FromSeconds(60))
                {
                    _logger.LogInformation("RTD fallback bloqueado — ReplayMode DDE ativo");
                    _rtdLastWarnUtc = DateTime.UtcNow;
                }

                UpdateConnectionState();
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                if (DateTime.UtcNow - _rtdLastWarnUtc > TimeSpan.FromSeconds(30))
                {
                    SetStatus(false, "error", ex.Message);
                    _logger.LogWarning(ex, "Erro no ciclo RTD fallback");
                    _rtdLastWarnUtc = DateTime.UtcNow;
                }
            }

            await Task.Delay(250, stoppingToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _hostStoppingToken = stoppingToken;

        while (!stoppingToken.IsCancellationRequested)
        {
            var mode = _mode.Current;

            if (mode == ProfitMarketDataMode.ProfitDll)
            {
                StopRtdThread();
                _coordinator.AllowRtdFallback = false;
                SetStatus(false, "disabled", "Modo ProfitDLL — RTD em espera");
                await _mode.WaitForChangeAsync(stoppingToken);
                continue;
            }

            if (mode == ProfitMarketDataMode.Dde)
            {
                var allowFallback = _options.CurrentValue.AllowRtdFallbackWhenDdeStale;
                _coordinator.AllowRtdFallback = allowFallback;
                if (!allowFallback)
                {
                    StopRtdThread();
                    SetStatus(false, "standby", "Modo DDE — RTD fallback desligado");
                    await _mode.WaitForChangeAsync(stoppingToken);
                    continue;
                }

                _logger.LogInformation("Profit RTD fallback ativo — ticks via DDE, RTD quando DDE sem cotações");
                SetStatus(false, "fallback", "RTD pronto como fallback do DDE");
                await RunRtdFallbackLoopAsync(stoppingToken);
                continue;
            }

            // mode == Rtd
            _coordinator.AllowRtdFallback = false;
            _coordinator.ReplayModeActive = false; // RTD primário não compete com DDE replay
            _logger.LogInformation("Profit RTD worker iniciando (thread STA COM) — modo primário");
            SetStatus(false, "starting", "Iniciando RTD COM…");

            try
            {
                await StartRtdThreadAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                SetStatus(false, "error", ex.Message);
                _logger.LogError(ex, "Falha fatal no Profit RTD");
                await Task.Delay(5000, stoppingToken);
                continue;
            }

            while (!stoppingToken.IsCancellationRequested && _mode.Current == ProfitMarketDataMode.Rtd)
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

            if (_mode.Current != ProfitMarketDataMode.Rtd)
                StopRtdThread();
        }

        StopRtdThread();
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (_mode.Current is not (ProfitMarketDataMode.Rtd or ProfitMarketDataMode.Dde))
        {
            SetStatus(false, "disabled", $"Modo ativo: {_mode.Current.ToDisplayName()}");
            return;
        }

        if (_mode.Current == ProfitMarketDataMode.Dde && !_options.CurrentValue.AllowRtdFallbackWhenDdeStale)
        {
            SetStatus(false, "standby", "RTD fallback desligado no modo DDE");
            return;
        }

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
        var configPath = Path.Combine(AppContext.BaseDirectory, _options.CurrentValue.ProfitRtdConfigPath);

        var thread = new Thread(() =>
        {
            ProfitRtdComClient? client = null;
            try
            {
                client = new ProfitRtdComClient(configPath, _logger);
                if (!client.TryStart(out var startError))
                    throw new InvalidOperationException(startError ?? "RTD indisponível");

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
                _logger.LogDebug(ex, "Thread STA Profit RTD encerrada");
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

        // Em modo DDE+fallback, só publica quando DDE está stale e replay não bloqueia.
        if (_mode.Current == ProfitMarketDataMode.Dde && !_coordinator.ShouldPublishRtdFallback())
            return;

        // Modo Rtd primário: nunca publicar se ReplayMode DDE ainda estiver marcado.
        if (_mode.Current == ProfitMarketDataMode.Rtd && _coordinator.ReplayModeActive)
            return;

        var published = false;
        foreach (var (ticker, quote) in client.Quotes)
        {
            if (quote.Last is null or <= 0)
                continue;

            var tick = MarketTickNormalizer.FromProvider(
                BrokerSource.Profit,
                ticker,
                quote.Last,
                quote.Bid,
                quote.Ask,
                quote.Volume);

            // Garante Source rastreável no ingest
            tick = tick with { Source = $"RTD:{ticker}" };

            _ = _publisher.PublishAsync(tick);
            OnTick?.Invoke(tick.ToNormalized());
            published = true;

            if (_positions.TryGetValue(ticker, out var pos))
            {
                _positions[ticker] = pos with
                {
                    UnrealizedPnL = (quote.Last.Value - pos.AveragePrice) * pos.Quantity,
                    TimestampUtc = DateTime.UtcNow
                };
            }
        }

        if (!published)
        {
            foreach (var (ticker, last) in client.LastPrices)
            {
                var tick = MarketTickNormalizer.FromProvider(
                    BrokerSource.Profit,
                    ticker,
                    last,
                    bid: null,
                    ask: null);

                tick = tick with { Source = $"RTD:{ticker}" };

                _ = _publisher.PublishAsync(tick);
                OnTick?.Invoke(tick.ToNormalized());
                published = true;
            }
        }

        if (published)
            _coordinator.RecordRtdTick();

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

        var hasQuotes = client.LastPrices.Count > 0 || client.Quotes.Any(q => q.Value.Last is > 0);
        if (hasQuotes)
        {
            var age = client.LastDataUtc.HasValue
                ? DateTime.UtcNow - client.LastDataUtc.Value
                : TimeSpan.MaxValue;
            var ddePrimary = _mode.Current == ProfitMarketDataMode.Dde;
            var usingFallback = ddePrimary && _coordinator.ShouldPublishRtdFallback();

            // Em modo RTD primário mantém connected com último preço conhecido
            // (fora do pregão o Profit para de enviar RefreshData).
            var live = age < TimeSpan.FromSeconds(30);
            var connected = ddePrimary
                ? live && (usingFallback || !_coordinator.IsDdeActive())
                : true;

            var price = client.LastPrices.GetValueOrDefault("WIN", client.LastPrices.Values.FirstOrDefault());
            var mode = ddePrimary
                ? (_coordinator.IsDdeActive() ? "DDE ativo" : "fallback RTD")
                : (live ? "RTD" : "RTD snapshot");
            SetStatus(
                connected,
                connected ? "connected" : "stale",
                connected
                    ? $"WIN @ {price:N0} ({mode}, {client.DataCount} ticks)"
                    : $"Sem dados há {age.TotalSeconds:N0}s ({mode})");
            return;
        }

        if (client.IsConnected)
        {
            var mode = _mode.Current == ProfitMarketDataMode.Dde ? "fallback RTD" : "RTD";
            SetStatus(false, "waiting", client.LastError ?? $"Aguardando dados do Profit ({mode})…");
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
