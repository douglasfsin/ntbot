using Microsoft.AspNetCore.SignalR.Client;
using NtBot.Web.Models;

namespace NtBot.Web.Services;

public sealed class MarketDriversHubService : IAsyncDisposable
{
    private readonly AuthSession _session;
    private readonly AuthSignInService _signIn;
    private readonly string _hubUrl;
    private readonly ILogger<MarketDriversHubService> _logger;
    private HubConnection? _connection;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action<MarketDriversSnapshotModel>? SnapshotUpdated;
    public event Action<List<MarketDriversDashboardItemModel>>? DashboardUpdated;

    public MarketDriversHubService(
        IConfiguration configuration,
        IHostEnvironment environment,
        AuthSession session,
        AuthSignInService signIn,
        ILogger<MarketDriversHubService> logger)
    {
        _session = session;
        _signIn = signIn;
        var apiBase = ApiUrlResolver.Resolve(configuration, environment);
        _hubUrl = $"{apiBase}/hubs/market-drivers";
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _signIn.HydrateSession(_session);
        if (string.IsNullOrEmpty(_session.Token)) return;
        if (_connection?.State == HubConnectionState.Connected) return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options => options.AccessTokenProvider = () => Task.FromResult<string?>(_session.Token))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<MarketDriversSnapshotModel>("MarketDriversSnapshotUpdated", s => SnapshotUpdated?.Invoke(s));
        _connection.On<List<MarketDriversDashboardItemModel>>("MarketDriversDashboardUpdated", d => DashboardUpdated?.Invoke(d));

        try
        {
            await _connection.StartAsync(ct);
            await _connection.InvokeAsync("SubscribeDrivers", ct);
            _logger.LogInformation("Market drivers hub connected");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Market drivers hub connection failed");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
