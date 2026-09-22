namespace NtBot.Api.Configuration;

/// <summary>
/// Writes TI zone delim files into MetaTrader Common/Files so indicators can
/// read zones via FileOpen when WebRequest allowlist (err 4014) fails.
/// </summary>
public sealed class Mt5ZonesFileBridgeOptions
{
    public const string SectionName = "Mt5ZonesFileBridge";

    /// <summary>When false, no files are written (HTTP endpoint still works).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Destination directory. Empty = %APPDATA%\MetaQuotes\Terminal\Common\Files
    /// (MT5 FILE_COMMON). Override for Docker / custom layouts.
    /// </summary>
    public string OutputDirectory { get; set; } = "";

    /// <summary>Logical symbols to refresh in the background worker.</summary>
    public List<string> Symbols { get; set; } = ["XAUUSD"];

    /// <summary>TI timeframe key written into each file (5/15/30/60/240/1440).</summary>
    public string Timeframe { get; set; } = "60";

    /// <summary>Max zones per file (0 = builder default).</summary>
    public int MaxZones { get; set; } = 6;

    /// <summary>Background refresh interval in seconds.</summary>
    public int RefreshSeconds { get; set; } = 30;

    /// <summary>File name pattern; {symbol} is replaced with the canonical symbol.</summary>
    public string FileNamePattern { get; set; } = "NTBot_zones_{symbol}.txt";
}
