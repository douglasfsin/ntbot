using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace NtBot.Api.Hubs;

public class TradingHub : Hub
{
    public async Task SubscribeToTrades(string symbol)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"trades_{symbol}");
        await Clients.Caller.SendAsync("Subscribed", new { symbol, group = $"trades_{symbol}" });
    }

    public async Task UnsubscribeFromTrades(string symbol)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"trades_{symbol}");
        await Clients.Caller.SendAsync("Unsubscribed", new { symbol, group = $"trades_{symbol}" });
    }

    public async Task SubscribeToMT5(string symbol)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"mt5_{symbol}");
        await Clients.Caller.SendAsync("MT5Subscribed", new { symbol, group = $"mt5_{symbol}" });
    }

    public async Task UnsubscribeFromMT5(string symbol)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"mt5_{symbol}");
        await Clients.Caller.SendAsync("MT5Unsubscribed", new { symbol, group = $"mt5_{symbol}" });
    }

    public async Task SendTradeCommand(string command, object parameters)
    {
        // Broadcast command to MT5 EAs
        await Clients.Group("mt5_all").SendAsync("TradeCommand", new
        {
            command,
            parameters,
            timestamp = DateTime.UtcNow,
            from = Context.ConnectionId
        });
    }

    public async Task RequestMT5Status()
    {
        await Clients.Group("mt5_all").SendAsync("StatusRequest", new
        {
            timestamp = DateTime.UtcNow,
            requester = Context.ConnectionId
        });
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await Clients.Caller.SendAsync("Connected", new
        {
            connectionId = Context.ConnectionId,
            timestamp = DateTime.UtcNow
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}