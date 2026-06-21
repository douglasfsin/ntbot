namespace NtBot.Connector.Windows.Configuration;

public class Mt5Config
{
    public List<string> Symbols { get; set; } = ["XAUUSD", "EURUSD"];

    public string? Mt5Path { get; set; }

    public int Login { get; set; }

    public string? Password { get; set; }

    public string? Server { get; set; }

    public double TickIntervalSeconds { get; set; } = 0.5;

    public int BookDepth { get; set; } = 10;

    /// <summary>python.exe ou py — vazio = auto-detect.</summary>
    public string? PythonExecutable { get; set; }

    public int ApiPort { get; set; } = 8228;
}
