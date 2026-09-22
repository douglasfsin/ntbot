using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NtBot.Api.Services.MarketData;
using NtBot.Macro.Configuration;
using NtBot.Shared.MarketData;
using NtBot.TradingIntelligence.Cache;
using NtBot.TradingIntelligence.Services;

namespace NtBot.Api.Hubs;

[Authorize]
public class TradingIntelligenceHub : Hub
{
    private readonly IChartCandleStreamService _chartStream;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TradingIntelligenceHub> _logger;

    public TradingIntelligenceHub(
        IChartCandleStreamService chartStream,
        IServiceScopeFactory scopeFactory,
        ILogger<TradingIntelligenceHub> logger)
    {
        _chartStream = chartStream;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "trading_intelligence_all");
        await base.OnConnectedAsync();
    }

    public async Task SubscribeAsset(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return;

        var normalized = MacroSymbolAliases.Normalize(symbol);
        await Groups.AddToGroupAsync(Context.ConnectionId, AssetGroup(normalized));
        await Groups.AddToGroupAsync(Context.ConnectionId, "trading_intelligence_all");

        await PushCachedSnapshotAsync(normalized);
        // Offload heavy snapshot build to the thread pool so hub Invoke/negotiate stay responsive.
        var connectionId = Context.ConnectionId;
        _ = Task.Run(() => RefreshAssetAsync(connectionId, normalized));
    }

    public Task SubscribeChart(string symbol, string timeframe = "60", int count = 80) =>
        _chartStream.SubscribeAsync(Context.ConnectionId, symbol, timeframe, count, Context.ConnectionAborted);

    public Task UnsubscribeChart(string symbol, string timeframe = "60") =>
        _chartStream.UnsubscribeAsync(Context.ConnectionId, symbol, timeframe, Context.ConnectionAborted);

    public async Task SubscribePrice(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return;

        var normalized = MarketCandleService.NormalizeSymbol(symbol);
        await Groups.AddToGroupAsync(Context.ConnectionId, ChartPriceService.BuildPriceGroup(normalized));

        using var scope = _scopeFactory.CreateScope();
        var prices = scope.ServiceProvider.GetRequiredService<IChartPriceService>();
        var current = await prices.GetPriceAsync(normalized, cancellationToken: Context.ConnectionAborted);
        if (current is not null)
            await Clients.Caller.SendAsync("ChartPriceUpdated", current);
    }

    public Task UnsubscribePrice(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return Task.CompletedTask;

        var normalized = MarketCandleService.NormalizeSymbol(symbol);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, ChartPriceService.BuildPriceGroup(normalized));
    }

    public static string AssetGroup(string asset) => $"ti_asset_{asset}";

    public static string ChartGroup(string symbol, string timeframe) =>
        IChartCandleStreamService.GroupName(
            MarketCandleService.NormalizeSymbol(symbol),
            ChartTimeframe.ToChartKey(timeframe));

    private async Task PushCachedSnapshotAsync(string normalized)
    {
        using var scope = _scopeFactory.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ITradingIntelligenceCacheService>();
        // Somente cache/last-known — nunca build pesado no Invoke do hub.
        var cached = cache.GetLastKnownSnapshot(normalized)
            ?? await cache.GetSnapshotAsync(normalized, cancellationToken: Context.ConnectionAborted);
        if (cached is not null)
            await Clients.Caller.SendAsync("TradingIntelligenceSnapshotUpdated", cached);
    }

    private static readonly SemaphoreSlim RefreshGate = new(2, 2);

    private async Task RefreshAssetAsync(string connectionId, string asset)
    {
        if (!await RefreshGate.WaitAsync(0))
        {
            _logger.LogDebug("Skipping TI background refresh for {Asset} — refresh queue saturated", asset);
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var ti = scope.ServiceProvider.GetRequiredService<ITradingIntelligenceService>();
            var notifier = scope.ServiceProvider.GetService<ITradingIntelligenceUpdateNotifier>();
            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<TradingIntelligenceHub>>();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var snapshot = await ti.GetSnapshotAsync(asset, cancellationToken: cts.Token);
            if (snapshot is null)
            {
                _logger.LogWarning("Trading intelligence refresh returned null for {Asset}", asset);
                return;
            }

            await hub.Clients.Client(connectionId)
                .SendAsync("TradingIntelligenceSnapshotUpdated", snapshot);
            if (notifier is not null)
                await notifier.NotifySnapshotUpdatedAsync(snapshot);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background trading intelligence refresh failed for {Asset}", asset);
        }
        finally
        {
            RefreshGate.Release();
        }
    }
}
