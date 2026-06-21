using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NtBot.Api.Hubs;

[Authorize]
public class TradingIntelligenceHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "trading_intelligence_all");
        await base.OnConnectedAsync();
    }
}
