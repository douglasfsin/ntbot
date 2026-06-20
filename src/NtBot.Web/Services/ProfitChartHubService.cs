using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace NtBot.Web.Services;

public class ProfitChartHubService : IAsyncDisposable
{
    private readonly string _hubUrl;
    private readonly ILogger<ProfitChartHubService> _logger;
    private HubConnection? _connection;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public event Action<string, string, object?, DateTime>? TickUpdated;

    public ProfitChartHubService(IConfiguration configuration, IHostEnvironment environment, ILogger<ProfitChartHubService> logger)
    {
        var apiBase = ApiUrlResolver.Resolve(configuration, environment);
        _hubUrl = $"{apiBase}/hubs/profitchart";
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_connection?.State == HubConnectionState.Connected)
            return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<string, string, object, string>("TickUpdate", (ticker, topic, value, timestamp) =>
        {
            if (DateTime.TryParse(timestamp, out var ts))
                TickUpdated?.Invoke(ticker, topic, value, ts);
        });

        await _connection.StartAsync(ct);
        _logger.LogInformation("ProfitChart hub connected at {HubUrl}", _hubUrl);
    }

    public async Task SubscribeAllAsync()
    {
        if (_connection?.State != HubConnectionState.Connected) return;
        await _connection.InvokeAsync("SubscribeAll");
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
