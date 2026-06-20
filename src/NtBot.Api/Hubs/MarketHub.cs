using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace NtBot.Api.Hubs;

public class MarketHub : Hub
{
    public async Task SubscribeToTicks(string symbol)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"ticks_{symbol}");
        await Clients.Caller.SendAsync("Subscribed", new { symbol, group = $"ticks_{symbol}" });
    }

    public async Task UnsubscribeFromTicks(string symbol)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticks_{symbol}");
        await Clients.Caller.SendAsync("Unsubscribed", new { symbol, group = $"ticks_{symbol}" });
    }

    public async Task SubscribeToMarketData(string source)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"market_{source}");
        await Clients.Caller.SendAsync("MarketSubscribed", new { source, group = $"market_{source}" });
    }

    public async Task UnsubscribeFromMarketData(string source)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"market_{source}");
        await Clients.Caller.SendAsync("MarketUnsubscribed", new { source, group = $"market_{source}" });
    }

    public async Task RequestTickHistory(string symbol, DateTime from, DateTime to)
    {
        // This would typically query a database or cache
        await Clients.Caller.SendAsync("TickHistoryRequested", new
        {
            symbol,
            from,
            to,
            status = "processing",
            timestamp = DateTime.UtcNow
        });
    }

    public async Task SubscribeToAllSources()
    {
        string[] sources = { "MT5", "NinjaTrader", "ProfitChart", "Simulator" };
        foreach (var source in sources)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"market_{source}");
        }
        await Clients.Caller.SendAsync("AllSourcesSubscribed", new { sources, timestamp = DateTime.UtcNow });
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await Clients.Caller.SendAsync("MarketConnected", new
        {
            connectionId = Context.ConnectionId,
            timestamp = DateTime.UtcNow,
            availableSources = new[] { "MT5", "NinjaTrader", "ProfitChart", "Simulator" }
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}