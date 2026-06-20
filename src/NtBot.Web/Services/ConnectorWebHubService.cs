using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace NtBot.Web.Services;

public class ConnectorWebHubService : IAsyncDisposable
{
    private readonly AuthSession _session;
    private readonly AuthSignInService _signIn;
    private readonly string _hubUrl;
    private readonly ILogger<ConnectorWebHubService> _logger;
    private HubConnection? _connection;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action<string, decimal?>? TickReceived;

    public ConnectorWebHubService(
        IConfiguration configuration,
        IHostEnvironment environment,
        AuthSession session,
        AuthSignInService signIn,
        ILogger<ConnectorWebHubService> logger)
    {
        _session = session;
        _signIn = signIn;
        var apiBase = ApiUrlResolver.Resolve(configuration, environment);
        _hubUrl = $"{apiBase}/hubs/connector-web";
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _signIn.HydrateSession(_session);

        if (string.IsNullOrEmpty(_session.Token))
            return;

        if (_connection?.State == HubConnectionState.Connected)
            return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(_session.Token);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<ConnectorTickMessage>("ConnectorTick", tick =>
        {
            if (tick.Last.HasValue)
                TickReceived?.Invoke(tick.Symbol, tick.Last);
        });

        await _connection.StartAsync(ct);
        _logger.LogInformation("ConnectorWebHub connected at {HubUrl}", _hubUrl);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    private sealed class ConnectorTickMessage
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal? Last { get; set; }
    }
}
