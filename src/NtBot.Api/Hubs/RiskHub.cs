using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace NtBot.Api.Hubs;

public class RiskHub : Hub
{
    public async Task SubscribeToRisk(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"risk_{tenantId}");
        await Clients.Caller.SendAsync("RiskSubscribed", new { tenantId, group = $"risk_{tenantId}" });
    }

    public async Task UnsubscribeFromRisk(string tenantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"risk_{tenantId}");
        await Clients.Caller.SendAsync("RiskUnsubscribed", new { tenantId, group = $"risk_{tenantId}" });
    }

    public async Task SubscribeToDrawdownAlerts(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"drawdown_{tenantId}");
        await Clients.Caller.SendAsync("DrawdownAlertsSubscribed", new { tenantId, group = $"drawdown_{tenantId}" });
    }

    public async Task UnsubscribeFromDrawdownAlerts(string tenantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"drawdown_{tenantId}");
        await Clients.Caller.SendAsync("DrawdownAlertsUnsubscribed", new { tenantId, group = $"drawdown_{tenantId}" });
    }

    public async Task RequestRiskStatus(string tenantId)
    {
        // This would typically query risk management service
        await Clients.Caller.SendAsync("RiskStatusRequested", new
        {
            tenantId,
            status = "processing",
            timestamp = DateTime.UtcNow
        });
    }

    public async Task UpdateRiskLimits(string tenantId, object limits)
    {
        // Broadcast risk limit updates to all subscribers
        await Clients.Group($"risk_{tenantId}").SendAsync("RiskLimitsUpdated", new
        {
            tenantId,
            limits,
            updatedBy = Context.ConnectionId,
            timestamp = DateTime.UtcNow
        });
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await Clients.Caller.SendAsync("RiskConnected", new
        {
            connectionId = Context.ConnectionId,
            timestamp = DateTime.UtcNow,
            availableRiskMetrics = new[] { "drawdown", "exposure", "margin", "daily_pnl", "win_rate" }
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}