namespace NtBot.Connector.Windows.Configuration;

public sealed class QuantConnectorOptions
{
    public const string SectionName = "Quant";

    public bool EnableOhlcvSync { get; set; } = true;

    public int OhlcvSyncIntervalMs { get; set; } = 60_000;

    public int StartupDelayMs { get; set; } = 5_000;

    /// <summary>Timeframe legado — usado se <see cref="Timeframes"/> estiver vazio.</summary>
    public string Timeframe { get; set; } = "M5";

    /// <summary>Timeframes OHLCV sincronizados do MT5 (ex.: M5, M15, M30, H1).</summary>
    public List<string> Timeframes { get; set; } = ["M5", "M15", "M30", "H1"];

    public int CandleCount { get; set; } = 120;
}
