namespace NtBot.Api.Configuration;

public sealed class QuantOptions
{
    public const string SectionName = "Quant";

    /// <summary>URL base do serviço Python MT5 (ex.: http://host.docker.internal:8228).</summary>
    public string? Mt5ApiUrl { get; set; }

    /// <summary>
    /// Allowlist de símbolos FX/metals esperados no host MT5 (:8228). Alinhar com
    /// <c>MarketData.API</c> <c>MT5:Symbols</c> e Connector <c>mt5_config.json</c>.
    /// Não representa o Market Watch completo; agentes MCP devem usar a mesma lista.
    /// </summary>
    public List<string> Mt5Symbols { get; set; } =
    [
        "XAUUSD", "EURUSD", "NZDUSD", "USDJPY", "GBPUSD",
        "USDBRL", "USOUSD", "VIX", "USDMXN", "UKOUSD"
    ];

    public string DefaultTimeframe { get; set; } = "M5";

    /// <summary>Idade máxima (minutos) dos candles em cache no banco antes de buscar no MT5.</summary>
    public int DatabaseMaxAgeMinutes { get; set; } = 15;

    /// <summary>Mínimo de candles para considerar série utilizável.</summary>
    public int MinCandles { get; set; } = 50;

    /// <summary>Mapeamento símbolo lógico → símbolo MT5 da corretora.</summary>
    public Dictionary<string, string> SymbolMap { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WINFUT"] = "WIN$",
        ["WIN"] = "WIN$",
        ["WDOFUT"] = "WDO$",
        ["WDO"] = "WDO$",
        ["NQ"] = "NAS100",
        ["MNQ"] = "NAS100",
        ["ES"] = "US500",
        ["MES"] = "US500",
        ["XAU"] = "XAUUSD",
        ["GOLD"] = "XAUUSD",
        ["SP500"] = "US500",
        ["NASDAQ"] = "NAS100"
    };
}
