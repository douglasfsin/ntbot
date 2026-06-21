using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using NtBot.Web.Models;

namespace NtBot.Web.Services;

public sealed class MacroHubService : IAsyncDisposable
{
    private readonly AuthSession _session;
    private readonly AuthSignInService _signIn;
    private readonly string _hubUrl;
    private readonly ILogger<MacroHubService> _logger;
    private HubConnection? _connection;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action<MacroSnapshotModel>? SnapshotUpdated;
    public event Action<List<MacroProviderStatusModel>>? ProvidersUpdated;

    public MacroHubService(
        IConfiguration configuration,
        IHostEnvironment environment,
        AuthSession session,
        AuthSignInService signIn,
        ILogger<MacroHubService> logger)
    {
        _session = session;
        _signIn = signIn;
        var apiBase = ApiUrlResolver.Resolve(configuration, environment);
        _hubUrl = $"{apiBase}/hubs/macro";
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

        _connection.On<MacroSnapshotModel>("MacroSnapshotUpdated", s => SnapshotUpdated?.Invoke(s));
        _connection.On<List<MacroProviderStatusModel>>("MacroProvidersUpdated", p => ProvidersUpdated?.Invoke(p));

        try
        {
            await _connection.StartAsync(ct);
            await _connection.InvokeAsync("SubscribeMacro", ct);
            _logger.LogInformation("Macro hub connected at {HubUrl}", _hubUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Macro hub connection failed");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
