using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace NtBot.Web.Services;

public sealed class ConnectorTickUpdate
{
    public string Symbol { get; set; } = string.Empty;
    public decimal? Last { get; set; }
    public decimal? Bid { get; set; }
    public decimal? Ask { get; set; }
}

public class ConnectorWebHubService : IAsyncDisposable
{
    private readonly AuthSession _session;
    private readonly AuthSignInService _signIn;
    private readonly string _hubUrl;
    private readonly ILogger<ConnectorWebHubService> _logger;
    private HubConnection? _connection;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action<ConnectorTickUpdate>? TickReceived;

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

        _connection.On<ConnectorTickUpdate>("ConnectorTick", tick =>
        {
            TickReceived?.Invoke(tick);
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
}
