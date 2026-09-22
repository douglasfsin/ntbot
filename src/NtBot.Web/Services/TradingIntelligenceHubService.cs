using Microsoft.AspNetCore.SignalR.Client;
using NtBot.Web.Models;

namespace NtBot.Web.Services;

public sealed class TradingIntelligenceHubService : IAsyncDisposable
{
    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    ];

    private readonly AuthSession _session;
    private readonly AuthSignInService _signIn;
    private readonly string _hubUrl;
    private readonly ILogger<TradingIntelligenceHubService> _logger;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private HubConnection? _connection;
    private string? _chartSymbol;
    private string? _chartTimeframe;
    private string? _priceSymbol;
    private string? _subscribedAsset;
    private int _disposed;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public event Action<TradingIntelligenceSnapshotModel>? SnapshotUpdated;
    public event Action<ChartCandlesPushModel>? ChartCandlesUpdated;
    public event Action<ChartPriceModel>? ChartPriceUpdated;

    public TradingIntelligenceHubService(
        IConfiguration configuration,
        IHostEnvironment environment,
        AuthSession session,
        AuthSignInService signIn,
        ILogger<TradingIntelligenceHubService> logger)
    {
        _session = session;
        _signIn = signIn;
        var apiBase = ApiUrlResolver.Resolve(configuration, environment);
        _hubUrl = $"{apiBase}/hubs/trading-intelligence";
        _logger = logger;
    }

    /// <summary>
    /// Starts the shared hub connection. Caller <paramref name="ct"/> only gates waiting for the
    /// connect lock — it is intentionally NOT linked to <see cref="HubConnection.StartAsync"/>,
    /// so one subscriber cancel (timeframe change / component dispose) cannot abort a shared start.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _signIn.HydrateSession(_session);
        if (string.IsNullOrEmpty(_session.Token)) return;
        if (IsDisposed) return;

        if (!await TryEnterConnectLockAsync(ct).ConfigureAwait(false))
            return;

        try
        {
            if (IsDisposed) return;
            if (_connection?.State == HubConnectionState.Connected) return;

            await EnsureConnectionStartedAsync().ConfigureAwait(false);
        }
        finally
        {
            ReleaseConnectLock();
        }
    }

    public async Task SubscribeAssetAsync(string symbol, CancellationToken ct = default)
    {
        _subscribedAsset = symbol;

        // Connect is connection-owned; do not forward subscriber cancel into StartAsync.
        if (_connection?.State != HubConnectionState.Connected)
            await ConnectAsync(CancellationToken.None).ConfigureAwait(false);

        if (_connection?.State != HubConnectionState.Connected)
            return;

        try
        {
            await _connection.InvokeAsync("SubscribeAsset", symbol, ct).ConfigureAwait(false);
        }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) when (ct.IsCancellationRequested || IsDisposed) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SubscribeAsset failed for {Symbol}", symbol);
        }
    }

    public async Task SubscribeChartAsync(string symbol, string timeframe, CancellationToken ct = default)
    {
        _chartSymbol = symbol;
        _chartTimeframe = timeframe;

        if (_connection?.State != HubConnectionState.Connected)
            await ConnectAsync(CancellationToken.None).ConfigureAwait(false);

        if (_connection?.State != HubConnectionState.Connected)
            return;

        try
        {
            await _connection.InvokeAsync("SubscribeChart", symbol, timeframe, 80, ct).ConfigureAwait(false);
        }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) when (ct.IsCancellationRequested || IsDisposed) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SubscribeChart failed for {Symbol} {Timeframe}", symbol, timeframe);
        }
    }

    public async Task UnsubscribeChartAsync(string symbol, string timeframe, CancellationToken ct = default)
    {
        if (_connection?.State != HubConnectionState.Connected)
            return;

        try
        {
            await _connection.InvokeAsync("UnsubscribeChart", symbol, timeframe, ct).ConfigureAwait(false);
        }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) when (ct.IsCancellationRequested || IsDisposed) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "UnsubscribeChart failed for {Symbol} {Timeframe}", symbol, timeframe);
        }
    }

    public async Task SubscribePriceAsync(string symbol, CancellationToken ct = default)
    {
        _priceSymbol = symbol;

        if (_connection?.State != HubConnectionState.Connected)
            await ConnectAsync(CancellationToken.None).ConfigureAwait(false);

        if (_connection?.State != HubConnectionState.Connected)
            return;

        try
        {
            await _connection.InvokeAsync("SubscribePrice", symbol, ct).ConfigureAwait(false);
        }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) when (ct.IsCancellationRequested || IsDisposed) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SubscribePrice failed for {Symbol}", symbol);
        }
    }

    public async Task UnsubscribePriceAsync(string symbol, CancellationToken ct = default)
    {
        if (_connection?.State != HubConnectionState.Connected)
            return;

        try
        {
            await _connection.InvokeAsync("UnsubscribePrice", symbol, ct).ConfigureAwait(false);
        }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) when (ct.IsCancellationRequested || IsDisposed) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "UnsubscribePrice failed for {Symbol}", symbol);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try { _lifetimeCts.Cancel(); }
        catch (ObjectDisposedException) { }

        // Circuit-scoped: only drop the hub connection. Keep _connectLock alive so an
        // early page Dispose cannot throw ObjectDisposedException on later ConnectAsync.
        if (!await TryEnterConnectLockAsync(CancellationToken.None).ConfigureAwait(false))
        {
            await SwapAndDisposeConnectionAsync().ConfigureAwait(false);
            DisposeLifetimeCts();
            return;
        }

        try
        {
            await SwapAndDisposeConnectionAsync().ConfigureAwait(false);
        }
        finally
        {
            ReleaseConnectLock();
            DisposeLifetimeCts();
        }
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    private async Task EnsureConnectionStartedAsync()
    {
        await DisposeConnectionAsync(_connection).ConfigureAwait(false);
        _connection = null;

        if (IsDisposed) return;

        var connection = BuildConnection();
        _connection = connection;

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (IsDisposed || !ReferenceEquals(_connection, connection))
                return;

            try
            {
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
                connectCts.CancelAfter(TimeSpan.FromSeconds(20));
                await connection.StartAsync(connectCts.Token).ConfigureAwait(false);
                _logger.LogInformation("Trading intelligence hub connected");
                return;
            }
            catch (Exception ex) when (IsLifetimeCancellation(ex))
            {
                _logger.LogDebug("Trading intelligence hub connect aborted (circuit disposing)");
                await ClearConnectionIfCurrentAsync(connection).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                if (attempt >= maxAttempts || IsDisposed)
                {
                    _logger.LogWarning(
                        "Trading intelligence hub connection failed after {Attempts} attempt(s): {Message}",
                        attempt,
                        GetUsefulMessage(ex));
                    _logger.LogDebug(ex, "Trading intelligence hub connection failure details");
                    await ClearConnectionIfCurrentAsync(connection).ConfigureAwait(false);
                    return;
                }

                var delay = ReconnectDelays[Math.Min(attempt, ReconnectDelays.Length - 1)];
                _logger.LogDebug(
                    ex,
                    "Trading intelligence hub connect attempt {Attempt}/{Max} failed; retrying in {Delay}",
                    attempt,
                    maxAttempts,
                    delay);

                try
                {
                    await Task.Delay(delay, _lifetimeCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    await ClearConnectionIfCurrentAsync(connection).ConfigureAwait(false);
                    return;
                }
            }
        }
    }

    private HubConnection BuildConnection()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(_session.Token);
                options.HttpMessageHandlerFactory = _ => new SocketsHttpHandler
                {
                    ConnectTimeout = TimeSpan.FromSeconds(10)
                };
            })
            .WithAutomaticReconnect(ReconnectDelays)
            .Build();

        connection.ServerTimeout = TimeSpan.FromSeconds(30);
        connection.HandshakeTimeout = TimeSpan.FromSeconds(20);

        connection.On<TradingIntelligenceSnapshotModel>("TradingIntelligenceSnapshotUpdated",
            s => SnapshotUpdated?.Invoke(s));

        connection.On<ChartCandlesPushModel>("ChartCandlesUpdated",
            payload => ChartCandlesUpdated?.Invoke(payload));

        connection.On<ChartPriceModel>("ChartPriceUpdated",
            payload => ChartPriceUpdated?.Invoke(payload));

        connection.Reconnected += async _ =>
        {
            try
            {
                await ResubscribeAfterReconnectAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (IsDisposed) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Trading intelligence hub resubscribe after reconnect failed");
            }
        };

        return connection;
    }

    private async Task ResubscribeAfterReconnectAsync()
    {
        var ct = _lifetimeCts.Token;
        if (!string.IsNullOrWhiteSpace(_subscribedAsset))
            await SubscribeAssetAsync(_subscribedAsset, ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(_chartSymbol) && !string.IsNullOrWhiteSpace(_chartTimeframe))
            await SubscribeChartAsync(_chartSymbol, _chartTimeframe, ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(_priceSymbol))
            await SubscribePriceAsync(_priceSymbol, ct).ConfigureAwait(false);
    }

    private bool IsLifetimeCancellation(Exception ex)
    {
        if (IsDisposed || _lifetimeCts.IsCancellationRequested)
            return true;

        return false;
    }

    private static string GetUsefulMessage(Exception ex)
    {
        if (ex is AggregateException agg)
        {
            var inner = agg.Flatten().InnerExceptions.FirstOrDefault();
            if (inner is not null)
                return inner.Message;
        }

        return ex.InnerException?.Message ?? ex.Message;
    }

    private async Task ClearConnectionIfCurrentAsync(HubConnection connection)
    {
        if (!ReferenceEquals(_connection, connection))
            return;

        _connection = null;
        await DisposeConnectionAsync(connection).ConfigureAwait(false);
    }

    private async Task SwapAndDisposeConnectionAsync()
    {
        var orphan = Interlocked.Exchange(ref _connection, null);
        await DisposeConnectionAsync(orphan).ConfigureAwait(false);
    }

    private static async Task DisposeConnectionAsync(HubConnection? connection)
    {
        if (connection is null) return;
        try { await connection.DisposeAsync().ConfigureAwait(false); }
        catch { /* ignore */ }
    }

    private void DisposeLifetimeCts()
    {
        try { _lifetimeCts.Dispose(); }
        catch (ObjectDisposedException) { }
    }

    private async Task<bool> TryEnterConnectLockAsync(CancellationToken ct)
    {
        try
        {
            await _connectLock.WaitAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void ReleaseConnectLock()
    {
        try { _connectLock.Release(); }
        catch (ObjectDisposedException) { }
        catch (SemaphoreFullException) { }
    }
}
