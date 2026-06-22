using NtBot.Connector.Services;
using NtBot.MarketIntelligence.Configuration;
using NtBot.MarketIntelligence.Models;
using NtBot.Shared.Normalized;

namespace NtBot.Api.Services.Connector;

/// <summary>
/// Sobrepõe cotações MT5 do connector (tempo real) sobre moedas Yahoo no Market Intelligence.
/// </summary>
public sealed class ConnectorLiveMarketOverlay
{
    private readonly IConnectorLiveState _liveState;

    private static readonly Dictionary<string, (string Name, string YahooSymbol)> ForexMeta =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = ("EUR/USD", "EURUSD=X"),
            ["GBPUSD"] = ("GBP/USD", "GBPUSD=X"),
            ["USDJPY"] = ("USD/JPY", "JPY=X"),
            ["AUDUSD"] = ("AUD/USD", "AUDUSD=X"),
            ["NZDUSD"] = ("NZD/USD", "NZDUSD=X"),
            ["USDCAD"] = ("USD/CAD", "USDCAD=X"),
            ["USDCHF"] = ("USD/CHF", "USDCHF=X"),
            ["XAUUSD"] = ("Gold Spot", "GC=F"),
        };

    public ConnectorLiveMarketOverlay(IConnectorLiveState liveState) => _liveState = liveState;

    public IReadOnlyList<MarketSnapshot> BuildMt5Currencies(Guid tenantId)
    {
        var live = _liveState.GetSnapshot(tenantId);
        if (live is null || live.Ticks.Count == 0)
            return [];

        var list = new List<MarketSnapshot>();
        foreach (var (mt5Symbol, meta) in ForexMeta)
        {
            if (!TryGetMt5Tick(live.Ticks, mt5Symbol, out var tick))
                continue;

            var price = ResolvePrice(tick);
            if (price is null or <= 0)
                continue;

            list.Add(new MarketSnapshot
            {
                Timestamp = tick.TimestampUtc,
                Provider = "MT5",
                Symbol = mt5Symbol,
                Name = meta.Name,
                Category = MarketCategory.Currency,
                Price = price.Value,
                Open = price.Value,
                High = tick.Ask ?? price.Value,
                Low = tick.Bid ?? price.Value,
                MarketStatus = live.IsLive ? MarketStatus.Open : MarketStatus.Unknown
            });
        }

        return list;
    }

    public MarketOverview Apply(Guid tenantId, MarketOverview overview)
    {
        var mt5 = BuildMt5Currencies(tenantId);
        if (mt5.Count == 0)
            return overview;

        var yahooToReplace = ForexMeta.Values
            .Select(v => v.YahooSymbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var currencies = mt5
            .Concat(overview.Currencies.Where(c => !yahooToReplace.Contains(c.Symbol)))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var commodities = overview.Commodities.ToList();
        var xau = mt5.FirstOrDefault(c => c.Symbol.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase));
        if (xau is not null)
        {
            commodities = commodities
                .Where(c => !c.Symbol.Equals("GC=F", StringComparison.OrdinalIgnoreCase))
                .ToList();
            commodities.Insert(0, new MarketSnapshot
            {
                Timestamp = xau.Timestamp,
                Provider = "MT5",
                Symbol = "XAUUSD",
                Name = "Gold (MT5)",
                Category = MarketCategory.Commodity,
                Price = xau.Price,
                Open = xau.Open,
                High = xau.High,
                Low = xau.Low,
                MarketStatus = xau.MarketStatus
            });
        }

        return new MarketOverview
        {
            Timestamp = DateTime.UtcNow,
            Provider = $"MT5 + {overview.Provider}",
            Commodities = commodities,
            Indexes = overview.Indexes,
            Currencies = currencies,
            Treasury = overview.Treasury,
            Sectors = overview.Sectors,
            MarketRegime = overview.MarketRegime,
            Vix = overview.Vix
        };
    }

    private static bool TryGetMt5Tick(
        IReadOnlyDictionary<string, NormalizedMarketTick> ticks,
        string symbol,
        out NormalizedMarketTick tick)
    {
        if (ticks.TryGetValue(symbol, out tick!) && tick.Source == BrokerSource.MT5)
            return true;

        tick = default!;
        return false;
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
