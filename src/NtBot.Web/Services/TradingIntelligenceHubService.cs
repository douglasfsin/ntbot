using Microsoft.AspNetCore.SignalR.Client;
using NtBot.Web.Models;

namespace NtBot.Web.Services;

public sealed class TradingIntelligenceHubService : IAsyncDisposable
{
    private readonly AuthSession _session;
    private readonly AuthSignInService _signIn;
    private readonly string _hubUrl;
    private readonly ILogger<TradingIntelligenceHubService> _logger;
    private HubConnection? _connection;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public event Action<TradingIntelligenceSnapshotModel>? SnapshotUpdated;

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

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _signIn.HydrateSession(_session);
        if (string.IsNullOrEmpty(_session.Token)) return;
        if (_connection?.State == HubConnectionState.Connected) return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options => options.AccessTokenProvider = () => Task.FromResult<string?>(_session.Token))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<TradingIntelligenceSnapshotModel>("TradingIntelligenceSnapshotUpdated",
            s => SnapshotUpdated?.Invoke(s));

        try
        {
            await _connection.StartAsync(ct);
            _logger.LogInformation("Trading intelligence hub connected");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Trading intelligence hub connection failed");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
