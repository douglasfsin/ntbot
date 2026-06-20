using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace NtBot.Api.Hubs;

public class NotificationHub : Hub
{
    public async Task SubscribeToNotifications(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"notifications_{tenantId}");
        await Clients.Caller.SendAsync("NotificationsSubscribed", new { tenantId, group = $"notifications_{tenantId}" });
    }

    public async Task UnsubscribeFromNotifications(string tenantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"notifications_{tenantId}");
        await Clients.Caller.SendAsync("NotificationsUnsubscribed", new { tenantId, group = $"notifications_{tenantId}" });
    }

    public async Task SubscribeToAlerts(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"alerts_{tenantId}");
        await Clients.Caller.SendAsync("AlertsSubscribed", new { tenantId, group = $"alerts_{tenantId}" });
    }

    public async Task UnsubscribeFromAlerts(string tenantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"alerts_{tenantId}");
        await Clients.Caller.SendAsync("AlertsUnsubscribed", new { tenantId, group = $"alerts_{tenantId}" });
    }

    public async Task MarkNotificationAsRead(string notificationId)
    {
        await Clients.Caller.SendAsync("NotificationMarkedAsRead", new
        {
            notificationId,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task GetNotificationHistory(string tenantId, int page = 1, int pageSize = 50)
    {
        await Clients.Caller.SendAsync("NotificationHistoryRequested", new
        {
            tenantId,
            page,
            pageSize,
            status = "processing",
            timestamp = DateTime.UtcNow
        });
    }

    public async Task SendTestNotification(string tenantId, string message)
    {
        await Clients.Group($"notifications_{tenantId}").SendAsync("TestNotification", new
        {
            message,
            sentBy = Context.ConnectionId,
            timestamp = DateTime.UtcNow
        });
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await Clients.Caller.SendAsync("NotificationConnected", new
        {
            connectionId = Context.ConnectionId,
            timestamp = DateTime.UtcNow,
            notificationTypes = new[] { "info", "warning", "error", "success", "trade", "risk", "system" }
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}