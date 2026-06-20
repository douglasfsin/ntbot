using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;

namespace NtBot.Connector.Windows.SignalR;

public class NtBotHubClient : BackgroundService
{
    private readonly ConnectorOptions _options;
    private readonly ILogger<NtBotHubClient> _logger;
    private HubConnection? _connection;
    private int _retryAttempt;

    public NtBotHubClient(IOptions<ConnectorOptions> options, ILogger<NtBotHubClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_connection == null || _connection.State == HubConnectionState.Disconnected)
                    await ConnectAsync(stoppingToken);

                if (_connection?.State == HubConnectionState.Connected)
                    await _connection.InvokeAsync("Ping", stoppingToken);

                _retryAttempt = 0;
                await Task.Delay(_options.HeartbeatIntervalMs, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _retryAttempt++;
                var delay = Math.Min(
                    _options.ReconnectBaseDelayMs * (int)Math.Pow(2, _retryAttempt - 1),
                    _options.MaxReconnectDelayMs);
                _logger.LogWarning(ex, "SignalR reconnect em {Delay}ms", delay);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        var hubUrl = $"{_options.ApiBaseUrl.TrimEnd('/')}/hubs/connector?apiKey={Uri.EscapeDataString(_options.ApiKey)}";

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        _connection.On<DateTime>("Pong", ts =>
            _logger.LogDebug("Connector hub pong {Timestamp}", ts));

        await _connection.StartAsync(ct);
        _logger.LogInformation("SignalR conectado em {Url}", hubUrl);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection != null)
            await _connection.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
