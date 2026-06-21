namespace NtBot.Connector.Windows.Configuration;

public sealed class QuantConnectorOptions
{
    public const string SectionName = "Quant";

    public bool EnableOhlcvSync { get; set; } = true;

    public int OhlcvSyncIntervalMs { get; set; } = 300_000;

    public string Timeframe { get; set; } = "M5";

    public int CandleCount { get; set; } = 120;

    public List<QuantCandleSymbolPair> CandleSymbols { get; set; } =
    [
        new("WINFUT", "WIN$"),
        new("WDOFUT", "WDO$"),
        new("NQ", "NAS100")
    ];
}

public sealed record QuantCandleSymbolPair(string Symbol, string Mt5Symbol);
