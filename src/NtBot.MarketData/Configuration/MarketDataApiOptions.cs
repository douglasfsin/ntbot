namespace NtBot.MarketData.Configuration;

public sealed class MarketDataApiOptions
{
    public const string SectionName = "MarketDataApi";

    /// <summary>
    /// Base URL da MarketData.API. Kestrel em Program.cs escuta HTTPS em 5242
    /// (ex: https://localhost:5242). HTTP na mesma porta fecha a conexão (ResponseEnded).
    /// </summary>
    public string BaseUrl { get; set; } = "https://localhost:5242";

    /// <summary>
    /// Ativa o bridge SignalR para ticks B3/ProfitDLL.
    /// Desligue em Dev quando MarketData.API estiver parado para rebuild no VS —
    /// charts XAUUSD/MT5 e DB não dependem deste hub.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Tickers ProfitDLL para assinar no marketHub.</summary>
    public string[] Tickers { get; set; } = ["WINFUT", "DOLFUT", "WDOFUT", "PETR4", "VALE3", "ITUB4", "BBDC4", "WEGE3", "ABEV3"];

    /// <summary>Base de backoff de reconexão em segundos (exponencial até 5 min).</summary>
    public int ReconnectSeconds { get; set; } = 30;

    /// <summary>
    /// Após falhas de unreachable (connection refused / ResponseEnded), pausa tentativas
    /// por este intervalo (minutos) além do backoff exponencial. 0 = só backoff.
    /// </summary>
    public int UnreachableSkipMinutes { get; set; } = 5;
}
