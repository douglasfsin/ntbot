using Microsoft.AspNetCore.SignalR;
using NtBot.Connector.Services;

namespace NtBot.Api.Hubs;

/// <summary>
/// Hub para sessões do NtBot.Connector.Windows (ApiKey via query string).
/// </summary>
public class ConnectorHub : Hub
{
    private readonly IConnectorService _connector;
    private readonly ILogger<ConnectorHub> _logger;

    public ConnectorHub(IConnectorService connector, ILogger<ConnectorHub> logger)
    {
        _connector = connector;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var apiKey = http?.Request.Query["apiKey"].FirstOrDefault()
            ?? http?.Request.Headers["X-Connector-Api-Key"].FirstOrDefault();
        var ip = http?.Connection.RemoteIpAddress?.ToString();

        var auth = await _connector.ValidateApiKeyAsync(apiKey ?? string.Empty, ip);
        if (!auth.Success || !auth.LicenseActive)
        {
            _logger.LogWarning("Connector hub rejected connection {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        Context.Items["TenantId"] = auth.TenantId.ToString();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{auth.TenantId}");

        _logger.LogInformation("Connector hub connected {ConnectionId} tenant={TenantId}", Context.ConnectionId, auth.TenantId);
        await base.OnConnectedAsync();
    }

    public Task Ping() => Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
}
