using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NtBot.Macro.Configuration;
using NtBot.MarketDrivers.Services;

namespace NtBot.Api.Hubs;

[Authorize]
public class MarketDriversHub : Hub
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MarketDriversHub> _logger;

    public MarketDriversHub(IServiceScopeFactory scopeFactory, ILogger<MarketDriversHub> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task SubscribeDrivers()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "market_drivers_all");
        await Clients.Caller.SendAsync("MarketDriversSubscribed", new { timestamp = DateTime.UtcNow });
    }

    public async Task SubscribeAsset(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return;

        var normalized = MacroSymbolAliases.Normalize(symbol);
        await Groups.AddToGroupAsync(Context.ConnectionId, AssetGroup(normalized));
        await Groups.AddToGroupAsync(Context.ConnectionId, "market_drivers_all");

        await PushCachedSnapshotAsync(normalized);
        var connectionId = Context.ConnectionId;
        _ = Task.Run(() => RefreshAssetAsync(connectionId, normalized));
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "market_drivers_all");
        await base.OnConnectedAsync();
    }

    public static string AssetGroup(string asset) => $"md_asset_{asset}";

    private async Task PushCachedSnapshotAsync(string normalized)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMarketDriversService>();
        var cached = service.GetLastKnownSnapshot(normalized);
        if (cached is not null)
            await Clients.Caller.SendAsync("MarketDriversSnapshotUpdated", cached);
    }

    private static readonly SemaphoreSlim RefreshGate = new(2, 2);

    private async Task RefreshAssetAsync(string connectionId, string asset)
    {
        if (!await RefreshGate.WaitAsync(0))
        {
            _logger.LogDebug("Skipping drivers background refresh for {Asset} — refresh queue saturated", asset);
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IMarketDriversService>();
            var notifier = scope.ServiceProvider.GetService<IMarketDriversUpdateNotifier>();
            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<MarketDriversHub>>();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var snapshot = await service.GetSnapshotAsync(asset, cts.Token);
            if (snapshot is null)
            {
                _logger.LogWarning("Market drivers refresh returned null for {Asset}", asset);
                return;
            }

            await hub.Clients.Client(connectionId)
                .SendAsync("MarketDriversSnapshotUpdated", snapshot);
            if (notifier is not null)
                await notifier.NotifySnapshotUpdatedAsync(snapshot);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background market drivers refresh failed for {Asset}", asset);
        }
        finally
        {
            RefreshGate.Release();
        }
    }
}
