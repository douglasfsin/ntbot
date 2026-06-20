using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NtBot.Api.Hubs;

/// <summary>
/// Hub SignalR para o dashboard Blazor (JWT). Recebe batches do Connector Windows.
/// </summary>
[Authorize]
public class ConnectorWebHub : Hub
{
    private readonly ILogger<ConnectorWebHub> _logger;

    public ConnectorWebHub(ILogger<ConnectorWebHub> logger) => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty)
        {
            _logger.LogWarning("ConnectorWebHub rejected {ConnectionId} — tenant ausente", Context.ConnectionId);
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
        _logger.LogInformation("ConnectorWebHub connected {ConnectionId} tenant={TenantId}", Context.ConnectionId, tenantId);
        await Clients.Caller.SendAsync("Connected", new { tenantId, timestamp = DateTime.UtcNow });
        await base.OnConnectedAsync();
    }

    private Guid GetTenantId()
    {
        var claim = Context.User?.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claim, out var tenantId) ? tenantId : Guid.Empty;
    }
}
