using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NtBot.Api.Hubs;

[Authorize]
public class MarketDriversHub : Hub
{
    public async Task SubscribeDrivers()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "market_drivers_all");
        await Clients.Caller.SendAsync("MarketDriversSubscribed", new { timestamp = DateTime.UtcNow });
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "market_drivers_all");
        await base.OnConnectedAsync();
    }
}
