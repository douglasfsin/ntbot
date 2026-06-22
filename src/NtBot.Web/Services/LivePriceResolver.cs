using NtBot.Shared.MarketData;
using NtBot.Shared.Normalized;

namespace NtBot.Web.Services;

public static class LivePriceResolver
{
    public static IEnumerable<string> ExpandLookupKeys(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            yield break;

        foreach (var alias in ConnectorSymbolAliases.Expand(symbol))
            yield return alias;

        switch (symbol.Trim().ToUpperInvariant())
        {
            case "NASDAQ":
                yield return "NASDAQ";
                yield return "NQ";
                yield return "NAS100";
                yield return "USTEC";
                break;
            case "SP500":
                yield return "SP500";
                yield return "US500";
                yield return "ES";
                break;
            case "BTCUSD":
                yield return "BTCUSD";
                yield return "BTC";
                break;
        }
    }

    public static decimal? ResolvePrice(IReadOnlyDictionary<string, decimal> prices, string symbol)
    {
        foreach (var key in ExpandLookupKeys(symbol))
        {
            if (prices.TryGetValue(key, out var price) && price > 0)
                return price;
        }

        return null;
    }

    public static void ApplyTick(
        IDictionary<string, decimal> prices,
        string symbol,
        decimal? last,
        decimal? bid,
        decimal? ask)
    {
        var value = last ?? (bid is > 0 && ask is > 0 ? (bid + ask) / 2m : bid ?? ask);
        if (value is null or <= 0)
            return;

        foreach (var key in ExpandLookupKeys(symbol))
            prices[key] = value.Value;
    }

    public static string FormatPrice(string symbol, decimal price) =>
        symbol.ToUpperInvariant() switch
        {
            "WIN" or "WDO" => price.ToString("N0"),
            "XAUUSD" => price.ToString("N2"),
            _ => price.ToString("N4")
        };
}
