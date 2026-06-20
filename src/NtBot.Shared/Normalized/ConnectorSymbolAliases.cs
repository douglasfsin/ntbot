namespace NtBot.Shared.Normalized;

public static class ConnectorSymbolAliases
{
    public static IEnumerable<string> Expand(string symbol)
    {
        yield return symbol;

        if (symbol.Equals("WIN", StringComparison.OrdinalIgnoreCase))
            yield return "WINFUT";
        else if (symbol.Equals("WINFUT", StringComparison.OrdinalIgnoreCase))
            yield return "WIN";
    }
}
