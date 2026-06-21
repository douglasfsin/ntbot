using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NtBot.Api.Hubs;

[Authorize]
public class MarketIntelligenceHub : Hub
{
    public async Task SubscribeMarket()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "market_all");
        await Clients.Caller.SendAsync("MarketSubscribed", new { timestamp = DateTime.UtcNow });
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "market_all");
        await Clients.Caller.SendAsync("MarketConnected", new { connectionId = Context.ConnectionId, timestamp = DateTime.UtcNow });
        await base.OnConnectedAsync();
    }
}
