namespace NtBot.Shared.MarketData;

/// <summary>
/// Canonical symbols and storage aliases for OHLCV (WIN vs WINFUT, etc.).
/// </summary>
public static class CandleSymbolAliases
{
    private static readonly Dictionary<string, string> ToCanonical = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WINFUT"] = "WIN",
        ["WIN$"] = "WIN",
        ["IND"] = "WIN",
        ["WDOFUT"] = "WDO",
        ["DOL"] = "WDO",
        ["MNQ"] = "NQ",
        ["MES"] = "ES",
        ["NAS100"] = "NQ",
        ["USTEC"] = "NQ",
        ["US100"] = "NQ",
        ["US500"] = "ES",
        ["SP500"] = "ES",
        ["XAU"] = "XAUUSD",
        ["GOLD"] = "XAUUSD"
    };

    public static string Canonical(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return string.Empty;

        var key = symbol.Trim().ToUpperInvariant();
        return ToCanonical.TryGetValue(key, out var mapped) ? mapped : key;
    }

    public static IReadOnlyList<string> Expand(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return [];

        var canonical = Canonical(symbol);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { canonical, symbol.Trim().ToUpperInvariant() };

        switch (canonical)
        {
            case "WIN":
                set.Add("WINFUT");
                set.Add("WIN$");
                break;
            case "WDO":
                set.Add("WDOFUT");
                set.Add("DOL");
                break;
            case "NQ":
                set.Add("NAS100");
                set.Add("NASDAQ");
                set.Add("MNQ");
                break;
            case "ES":
                set.Add("US500");
                set.Add("SP500");
                set.Add("MES");
                break;
        }

        return set.ToList();
    }
}
