using Microsoft.AspNetCore.SignalR;
using NtBot.Api.Hubs;
using NtBot.Connector.Services;
using NtBot.Shared.MarketData;
using NtBot.Shared.Normalized;

namespace NtBot.Api.Services.MarketData;

public sealed class ChartPriceDto
{
    public string Symbol { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal? Bid { get; init; }
    public decimal? Ask { get; init; }
    public DateTime TimestampUtc { get; init; }
    public string Source { get; init; } = "unavailable";
}

public interface IChartPriceService
{
    Task<ChartPriceDto?> GetPriceAsync(string symbol, Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task PublishTickAsync(NormalizedMarketTick tick, CancellationToken cancellationToken = default);
    string PriceGroup(string symbol);
}

public sealed class ChartPriceService : IChartPriceService
{
    private readonly ICandleRedisStore _candleRedis;
    private readonly IConnectorLiveState _liveState;
    private readonly IChartCandleCache _chartCache;
    private readonly IHubContext<TradingIntelligenceHub> _hub;
    private readonly ILogger<ChartPriceService> _logger;

    public ChartPriceService(
        ICandleRedisStore candleRedis,
        IConnectorLiveState liveState,
        IChartCandleCache chartCache,
        IHubContext<TradingIntelligenceHub> hub,
        ILogger<ChartPriceService> logger)
    {
        _candleRedis = candleRedis;
        _liveState = liveState;
        _chartCache = chartCache;
        _hub = hub;
        _logger = logger;
    }

    public string PriceGroup(string symbol) => BuildPriceGroup(symbol);

    public static string BuildPriceGroup(string symbol) =>
        $"chart_price_{CandleSymbolAliases.Canonical(symbol)}";

    public async Task<ChartPriceDto?> GetPriceAsync(
        string symbol,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = CandleSymbolAliases.Canonical(symbol);

        if (tenantId is Guid tid && tid != Guid.Empty)
        {
            var fromLive = TryFromLive(tid, normalized);
            if (fromLive is not null)
                return fromLive;
        }

        // Any tenant with a live tick for this symbol (multi-tenant chart pages).
        var anyLive = TryFromAnyTenant(normalized);
        if (anyLive is not null)
            return anyLive;

        var redisPrice = await _candleRedis.GetLastPriceAsync(normalized, cancellationToken);
        if (redisPrice is { } rp)
        {
            return new ChartPriceDto
            {
                Symbol = normalized,
                Price = rp.Price,
                TimestampUtc = rp.TimestampUtc,
                Source = "redis"
            };
        }

        foreach (var tf in new[] { "1", "5", "15", "60" })
        {
            var cached = _chartCache.Get(normalized, tf, 80)
                ?? _chartCache.Get(normalized, tf, 100)
                ?? _chartCache.Get(normalized, tf, 120);
            if (cached?.Candles is { Count: > 0 })
            {
                var last = cached.Candles[^1];
                return new ChartPriceDto
                {
                    Symbol = normalized,
                    Price = last.Close,
                    TimestampUtc = DateTimeOffset.FromUnixTimeSeconds(last.Time).UtcDateTime,
                    Source = "chart-cache"
                };
            }
        }

        return null;
    }

    public async Task PublishTickAsync(NormalizedMarketTick tick, CancellationToken cancellationToken = default)
    {
        var price = ResolvePrice(tick);
        if (price is null or <= 0 || string.IsNullOrWhiteSpace(tick.Symbol))
            return;

        var normalized = CandleSymbolAliases.Canonical(tick.Symbol);
        var ts = tick.TimestampUtc == default ? DateTime.UtcNow : tick.TimestampUtc.ToUniversalTime();

        try
        {
            await _candleRedis.ApplyTickToM1Async(normalized, price.Value, ts, tick.Volume ?? 0, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to apply tick to Redis M1 for {Symbol}", normalized);
        }

        var dto = new ChartPriceDto
        {
            Symbol = normalized,
            Price = price.Value,
            Bid = tick.Bid,
            Ask = tick.Ask,
            TimestampUtc = ts,
            Source = tick.Source.ToString()
        };

        await _hub.Clients.Group(BuildPriceGroup(normalized))
            .SendAsync("ChartPriceUpdated", dto, cancellationToken);
    }

    private ChartPriceDto? TryFromLive(Guid tenantId, string symbol)
    {
        var snap = _liveState.GetSnapshot(tenantId);
        if (snap?.Ticks is null)
            return null;

        foreach (var alias in CandleSymbolAliases.Expand(symbol))
        {
            if (!snap.Ticks.TryGetValue(alias, out var tick))
                continue;

            var price = ResolvePrice(tick);
            if (price is null or <= 0)
                continue;

            return new ChartPriceDto
            {
                Symbol = symbol,
                Price = price.Value,
                Bid = tick.Bid,
                Ask = tick.Ask,
                TimestampUtc = tick.TimestampUtc,
                Source = "connector-live"
            };
        }

        return null;
    }

    private ChartPriceDto? TryFromAnyTenant(string symbol)
    {
        // ConnectorLiveState has no enumeration API — best-effort via empty Guid is useless.
        // Rely on Redis + chart-cache when tenant unknown.
        return null;
    }

    private static decimal? ResolvePrice(NormalizedMarketTick tick)
    {
        if (tick.Last is > 0)
            return tick.Last;
        if (tick.Bid is > 0 && tick.Ask is > 0)
            return (tick.Bid + tick.Ask) / 2m;
        return tick.Bid ?? tick.Ask;
    }
}
