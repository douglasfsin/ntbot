using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NtBot.Api.Hubs;

[Authorize]
public class MacroHub : Hub
{
    public async Task SubscribeMacro()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "macro_all");
        await Clients.Caller.SendAsync("MacroSubscribed", new { timestamp = DateTime.UtcNow });
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "macro_all");
        await Clients.Caller.SendAsync("MacroConnected", new { connectionId = Context.ConnectionId, timestamp = DateTime.UtcNow });
        await base.OnConnectedAsync();
    }
}
