using Microsoft.AspNetCore.SignalR.Client;
using NtBot.Web.Models;

namespace NtBot.Web.Services;

public sealed class MarketHubService : IAsyncDisposable
{
    private readonly AuthSession _session;
    private readonly AuthSignInService _signIn;
    private readonly string _hubUrl;
    private readonly ILogger<MarketHubService> _logger;
    private HubConnection? _connection;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action<MarketOverviewModel>? OverviewUpdated;
    public event Action<CorrelationResultModel>? CorrelationUpdated;
    public event Action<QuantScoreModel>? QuantScoreUpdated;
    public event Action<List<MarketProviderStatusModel>>? ProvidersUpdated;

    public MarketHubService(
        IConfiguration configuration,
        IHostEnvironment environment,
        AuthSession session,
        AuthSignInService signIn,
        ILogger<MarketHubService> logger)
    {
        _session = session;
        _signIn = signIn;
        var apiBase = ApiUrlResolver.Resolve(configuration, environment);
        _hubUrl = $"{apiBase}/hubs/market-intelligence";
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

        _connection.On<MarketOverviewModel>("MarketOverviewUpdated", o => OverviewUpdated?.Invoke(o));
        _connection.On<CorrelationResultModel>("MarketCorrelationUpdated", c => CorrelationUpdated?.Invoke(c));
        _connection.On<QuantScoreModel>("MarketQuantScoreUpdated", s => QuantScoreUpdated?.Invoke(s));
        _connection.On<List<MarketProviderStatusModel>>("MarketProvidersUpdated", p => ProvidersUpdated?.Invoke(p));

        try
        {
            await _connection.StartAsync(ct);
            await _connection.InvokeAsync("SubscribeMarket", ct);
            _logger.LogInformation("Market intelligence hub connected");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Market intelligence hub connection failed");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
