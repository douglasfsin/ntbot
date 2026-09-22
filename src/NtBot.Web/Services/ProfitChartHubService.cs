using Microsoft.AspNetCore.SignalR.Client;

namespace NtBot.Web.Services;

public sealed class ProfitChartHubService : IAsyncDisposable
{
    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    ];

    private readonly string _hubUrl;
    private readonly ILogger<ProfitChartHubService> _logger;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private HubConnection? _connection;
    private int _disposed;
    private bool _subscribedAll;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public event Action<string, string, object?, DateTime>? TickUpdated;

    public ProfitChartHubService(IConfiguration configuration, IHostEnvironment environment, ILogger<ProfitChartHubService> logger)
    {
        var apiBase = ApiUrlResolver.Resolve(configuration, environment);
        _hubUrl = $"{apiBase}/hubs/profitchart";
        _logger = logger;
    }

    /// <summary>
    /// Starts the shared hub connection. Caller <paramref name="ct"/> only gates waiting for the
    /// connect lock — it is intentionally NOT linked to <see cref="HubConnection.StartAsync"/>.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
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

    public async Task SubscribeAllAsync(CancellationToken ct = default)
    {
        _subscribedAll = true;

        if (_connection?.State != HubConnectionState.Connected)
            await ConnectAsync(CancellationToken.None).ConfigureAwait(false);

        if (_connection?.State != HubConnectionState.Connected)
            return;

        try
        {
            await _connection.InvokeAsync("SubscribeAll", ct).ConfigureAwait(false);
        }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) when (ct.IsCancellationRequested || IsDisposed) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SubscribeAll failed");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try { _lifetimeCts.Cancel(); }
        catch (ObjectDisposedException) { }

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
                _logger.LogInformation("ProfitChart hub connected at {HubUrl}", _hubUrl);
                return;
            }
            catch (Exception ex) when (IsLifetimeCancellation(ex))
            {
                _logger.LogDebug("ProfitChart hub connect aborted (circuit disposing)");
                await ClearConnectionIfCurrentAsync(connection).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                if (attempt >= maxAttempts || IsDisposed)
                {
                    _logger.LogWarning(
                        "ProfitChart hub connection failed after {Attempts} attempt(s): {Message}",
                        attempt,
                        GetUsefulMessage(ex));
                    _logger.LogDebug(ex, "ProfitChart hub connection failure details");
                    await ClearConnectionIfCurrentAsync(connection).ConfigureAwait(false);
                    return;
                }

                var delay = ReconnectDelays[Math.Min(attempt, ReconnectDelays.Length - 1)];
                _logger.LogDebug(
                    ex,
                    "ProfitChart hub connect attempt {Attempt}/{Max} failed; retrying in {Delay}",
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
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect(ReconnectDelays)
            .Build();

        connection.ServerTimeout = TimeSpan.FromSeconds(30);
        connection.HandshakeTimeout = TimeSpan.FromSeconds(20);

        connection.On<string, string, object, string>("TickUpdate", (ticker, topic, value, timestamp) =>
        {
            if (DateTime.TryParse(timestamp, out var ts))
                TickUpdated?.Invoke(ticker, topic, value, ts);
        });

        connection.Reconnected += async _ =>
        {
            try
            {
                if (_subscribedAll && _connection?.State == HubConnectionState.Connected)
                    await _connection.InvokeAsync("SubscribeAll", _lifetimeCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (IsDisposed) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ProfitChart hub resubscribe after reconnect failed");
            }
        };

        return connection;
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
