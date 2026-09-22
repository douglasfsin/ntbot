namespace NtBot.Shared.Normalized;

public static class ConnectorSymbolAliases
{
    public static IEnumerable<string> Expand(string symbol)
    {
        yield return symbol;

        if (symbol.Equals("WIN", StringComparison.OrdinalIgnoreCase))
        {
            yield return "WINFUT";
            yield return "WINQ26";
        }
        else if (symbol.Equals("WINFUT", StringComparison.OrdinalIgnoreCase))
        {
            yield return "WIN";
            yield return "WINQ26";
        }
        else if (symbol.Equals("WDO", StringComparison.OrdinalIgnoreCase)
                 || symbol.Equals("WDOFUT", StringComparison.OrdinalIgnoreCase)
                 || symbol.Equals("DOLFUT", StringComparison.OrdinalIgnoreCase))
        {
            yield return "WDO";
            yield return "WDOFUT";
            yield return "DOLFUT";
        }
        else if (symbol.Equals("WINQ26", StringComparison.OrdinalIgnoreCase))
        {
            yield return "WIN";
            yield return "WINFUT";
        }
    }
}
