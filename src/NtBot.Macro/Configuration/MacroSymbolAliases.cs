namespace NtBot.Macro.Configuration;

/// <summary>
/// Normalizes trading symbols to macro recommendation profile keys.
/// </summary>
public static class MacroSymbolAliases
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WINFUT"] = "WIN",
        ["WIN$"] = "WIN",
        ["IND"] = "WIN",
        ["WDOFUT"] = "WDO",
        ["DOL"] = "WDO",
        ["MNQ"] = "NQ",
        ["MES"] = "ES",
        ["XAU"] = "XAUUSD",
        ["GOLD"] = "XAUUSD",
        ["PETR3"] = "PETR4"
    };

    public static string Normalize(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return string.Empty;

        var key = symbol.Trim().ToUpperInvariant();
        return Aliases.TryGetValue(key, out var mapped) ? mapped : key;
    }
}
