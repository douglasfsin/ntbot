namespace NtBot.Connector.Windows.Configuration;

public sealed class QuantConnectorOptions
{
    public const string SectionName = "Quant";

    public bool EnableOhlcvSync { get; set; } = true;

    /// <summary>Sincroniza também todos os símbolos de mt5_config.json (além dos mapeamentos Quant).</summary>
    public bool IncludeMt5ConfigSymbols { get; set; } = true;

    public int OhlcvSyncIntervalMs { get; set; } = 60_000;

    public int StartupDelayMs { get; set; } = 5_000;

    public string Timeframe { get; set; } = "M5";

    public int CandleCount { get; set; } = 120;

    public List<QuantCandleSymbolPair> CandleSymbols { get; set; } =
    [
        new() { Symbol = "WINFUT", Mt5Symbols = ["WIN$", "WINFUT", "WINJ26", "WINM26"] },
        new() { Symbol = "WDOFUT", Mt5Symbols = ["WDO$", "WDOFUT", "WDOJ26", "WDOM26"] },
        new() { Symbol = "NQ", Mt5Symbols = ["NAS100", "USTEC", "US100", "NQ", "US500"] }
    ];
}

public sealed class QuantCandleSymbolPair
{
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Primeiro símbolo MT5 a tentar (legado).</summary>
    public string Mt5Symbol { get; set; } = string.Empty;

    /// <summary>Lista de candidatos MT5, em ordem de prioridade.</summary>
    public List<string> Mt5Symbols { get; set; } = [];

    public IEnumerable<string> ResolveMt5Candidates()
    {
        if (!string.IsNullOrWhiteSpace(Mt5Symbol))
            yield return Mt5Symbol.Trim();

        foreach (var candidate in Mt5Symbols)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var normalized = candidate.Trim();
            if (string.Equals(normalized, Mt5Symbol?.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            yield return normalized;
        }
    }
}
