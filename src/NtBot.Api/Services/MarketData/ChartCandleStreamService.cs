using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using NtBot.Api.Controllers;
using NtBot.Api.Hubs;
using NtBot.Shared.MarketData;

namespace NtBot.Api.Services.MarketData;

public sealed class ChartCandlesPush
{
    public string Symbol { get; init; } = string.Empty;
    public string Timeframe { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public bool IsPartial { get; init; }
    public List<ChartCandleDto> Candles { get; init; } = [];
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}

public interface IChartCandleCache
{
    ChartCandlesPush? Get(string symbol, string timeframe, int count);
    void Set(string symbol, string timeframe, int count, ChartCandlesPush payload);
}

public sealed class ChartCandleCache : IChartCandleCache
{
    private readonly ConcurrentDictionary<string, (ChartCandlesPush Payload, DateTime ExpiresUtc)> _entries = new(StringComparer.OrdinalIgnoreCase);

    public ChartCandlesPush? Get(string symbol, string timeframe, int count)
    {
        var key = BuildKey(symbol, timeframe, count);
        if (!_entries.TryGetValue(key, out var item))
            return null;

        if (item.ExpiresUtc <= DateTime.UtcNow)
        {
            _entries.TryRemove(key, out _);
            return null;
        }

        return item.Payload;
    }

    public void Set(string symbol, string timeframe, int count, ChartCandlesPush payload)
    {
        var ttl = payload.Source.Contains("stale", StringComparison.OrdinalIgnoreCase) ||
                  payload.Source.Contains("synthetic", StringComparison.OrdinalIgnoreCase)
            ? TimeSpan.FromMinutes(2)
            : TimeSpan.FromMinutes(5);

        _entries[BuildKey(symbol, timeframe, count)] = (payload, DateTime.UtcNow.Add(ttl));
    }

    private static string BuildKey(string symbol, string timeframe, int count) =>
        $"{symbol.ToUpperInvariant()}|{timeframe}|{count}";
}

public interface IChartCandleStreamService
{
    Task SubscribeAsync(string connectionId, string symbol, string timeframe, int count = 80, CancellationToken cancellationToken = default);
    Task UnsubscribeAsync(string connectionId, string symbol, string timeframe, CancellationToken cancellationToken = default);
    static string GroupName(string symbol, string timeframe) =>
        $"chart_{symbol.ToUpperInvariant()}_{ChartTimeframe.ToChartKey(timeframe)}";
}

public sealed class ChartCandleStreamService : IChartCandleStreamService
{
    private readonly IChartCandleCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<TradingIntelligenceHub> _hub;
    private readonly ILogger<ChartCandleStreamService> _logger;
    private readonly ConcurrentDictionary<string, byte> _inflight = new(StringComparer.OrdinalIgnoreCase);

    public ChartCandleStreamService(
        IChartCandleCache cache,
        IServiceScopeFactory scopeFactory,
        IHubContext<TradingIntelligenceHub> hub,
        ILogger<ChartCandleStreamService> logger)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
    }

    public async Task SubscribeAsync(
        string connectionId,
        string symbol,
        string timeframe,
        int count = 80,
        CancellationToken cancellationToken = default)
    {
        var normalized = MarketCandleService.NormalizeSymbol(symbol);
        var tf = ChartTimeframe.ToChartKey(timeframe);
        var group = IChartCandleStreamService.GroupName(normalized, tf);

        await _hub.Groups.AddToGroupAsync(connectionId, group, cancellationToken);

        var cached = _cache.Get(normalized, tf, count);
        if (cached is not null && cached.Candles.Count > 0)
        {
            await _hub.Clients.Client(connectionId)
                .SendAsync("ChartCandlesUpdated", cached, cancellationToken);
        }

        var fetchKey = $"{normalized}|{tf}|{count}";
        if (!_inflight.TryAdd(fetchKey, 0))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshAndBroadcastAsync(normalized, tf, count, group, CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Chart candle refresh failed for {Symbol} {Timeframe}", normalized, tf);
            }
            finally
            {
                _inflight.TryRemove(fetchKey, out _);
            }
        }, CancellationToken.None);
    }

    public Task UnsubscribeAsync(
        string connectionId,
        string symbol,
        string timeframe,
        CancellationToken cancellationToken = default)
    {
        var normalized = MarketCandleService.NormalizeSymbol(symbol);
        var tf = ChartTimeframe.ToChartKey(timeframe);
        return _hub.Groups.RemoveFromGroupAsync(
            connectionId,
            IChartCandleStreamService.GroupName(normalized, tf),
            cancellationToken);
    }

    private async Task RefreshAndBroadcastAsync(
        string symbol,
        string timeframe,
        int count,
        string group,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var candles = scope.ServiceProvider.GetRequiredService<IMarketCandleService>();

        using var fetchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        fetchCts.CancelAfter(TimeSpan.FromSeconds(45));

        var result = await candles.GetCandlesAsync(symbol, count, timeframe, fetchCts.Token);
        if (result.Candles.Count == 0)
        {
            _logger.LogWarning("Chart candles empty for {Symbol} {Timeframe} (source={Source})", symbol, timeframe, result.Source);
            return;
        }

        if (!result.HasSufficientData(5))
            _logger.LogInformation(
                "Chart candles partial for {Symbol} {Timeframe}: {Count} bars source={Source}",
                symbol, timeframe, result.Candles.Count, result.Source);

        var chartTf = ChartTimeframe.ToChartKey(timeframe);
        var payload = new ChartCandlesPush
        {
            Symbol = symbol,
            Timeframe = chartTf,
            Source = result.Source,
            IsPartial = !result.HasSufficientData(5) ||
                        result.Source.Contains("partial", StringComparison.OrdinalIgnoreCase) ||
                        result.Source.Contains("stale", StringComparison.OrdinalIgnoreCase),
            Candles = result.Candles
                .OrderBy(c => c.OpenTime)
                .Select(c => new ChartCandleDto
                {
                    Time = new DateTimeOffset(c.OpenTime).ToUnixTimeSeconds(),
                    Open = c.Open,
                    High = c.High,
                    Low = c.Low,
                    Close = c.Close
                })
                .ToList(),
            UpdatedAt = DateTime.UtcNow
        };

        // Store under both chart key and normalized TF so REST + SignalR share the cache.
        _cache.Set(symbol, chartTf, count, payload);
        _cache.Set(symbol, ChartTimeframe.Normalize(timeframe), count, payload);
        await _hub.Clients.Group(group).SendAsync("ChartCandlesUpdated", payload, cancellationToken);
    }
}
