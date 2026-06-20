using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace NtBot.Api.Hubs;

public class ExecutionHub : Hub
{
    public async Task SubscribeToExecutions(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"executions_{tenantId}");
        await Clients.Caller.SendAsync("ExecutionsSubscribed", new { tenantId, group = $"executions_{tenantId}" });
    }

    public async Task UnsubscribeFromExecutions(string tenantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"executions_{tenantId}");
        await Clients.Caller.SendAsync("ExecutionsUnsubscribed", new { tenantId, group = $"executions_{tenantId}" });
    }

    public async Task SubscribeToOrderUpdates(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"orders_{tenantId}");
        await Clients.Caller.SendAsync("OrderUpdatesSubscribed", new { tenantId, group = $"orders_{tenantId}" });
    }

    public async Task UnsubscribeFromOrderUpdates(string tenantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"orders_{tenantId}");
        await Clients.Caller.SendAsync("OrderUpdatesUnsubscribed", new { tenantId, group = $"orders_{tenantId}" });
    }

    public async Task SendExecutionCommand(string command, object parameters)
    {
        // Broadcast execution commands to trading engines
        await Clients.Group("trading_engines").SendAsync("ExecutionCommand", new
        {
            command,
            parameters,
            timestamp = DateTime.UtcNow,
            from = Context.ConnectionId
        });
    }

    public async Task RequestExecutionHistory(string tenantId, DateTime from, DateTime to)
    {
        await Clients.Caller.SendAsync("ExecutionHistoryRequested", new
        {
            tenantId,
            from,
            to,
            status = "processing",
            timestamp = DateTime.UtcNow
        });
    }

    public async Task CancelOrder(string tenantId, string orderId)
    {
        await Clients.Group("trading_engines").SendAsync("OrderCancellation", new
        {
            tenantId,
            orderId,
            cancelledBy = Context.ConnectionId,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task ModifyOrder(string tenantId, string orderId, object modifications)
    {
        await Clients.Group("trading_engines").SendAsync("OrderModification", new
        {
            tenantId,
            orderId,
            modifications,
            modifiedBy = Context.ConnectionId,
            timestamp = DateTime.UtcNow
        });
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await Clients.Caller.SendAsync("ExecutionConnected", new
        {
            connectionId = Context.ConnectionId,
            timestamp = DateTime.UtcNow,
            supportedCommands = new[] { "buy", "sell", "cancel", "modify", "close" }
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}