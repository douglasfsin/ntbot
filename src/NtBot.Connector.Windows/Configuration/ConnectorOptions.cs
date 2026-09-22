namespace NtBot.Connector.Windows.Configuration;

public class ConnectorOptions
{
    public const string SectionName = "Connector";

    public string ApiBaseUrl { get; set; } = "http://localhost:5053";
    public string ApiKey { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public int IngestIntervalMs { get; set; } = 1000;
    public int HeartbeatIntervalMs { get; set; } = 30000;
    public int ReconnectBaseDelayMs { get; set; } = 2000;
    public int MaxReconnectDelayMs { get; set; } = 60000;

    /// <summary>
    /// Fonte ativa de ticks Profit: Dde | Rtd | ProfitDll.
    /// Se vazio, deriva de EnableProfitDde / EnableProfit (legado).
    /// </summary>
    public string ProfitMarketDataMode { get; set; } = string.Empty;

    /// <summary>Legado — preferir ProfitMarketDataMode=Rtd.</summary>
    public bool EnableProfit { get; set; } = true;

    /// <summary>Legado — preferir ProfitMarketDataMode=Dde.</summary>
    public bool EnableProfitDde { get; set; }

    /// <summary>
    /// Em modo Dde, permite RTD como fallback quando DDE fica sem ticks
    /// (desligado automaticamente em ReplayMode).
    /// </summary>
    public bool AllowRtdFallbackWhenDdeStale { get; set; }

    public bool EnableMt5 { get; set; }
    public bool EnableNinjaTrader { get; set; }
    public bool EnableTradingView { get; set; }
    public string ProfitRtdConfigPath { get; set; } = "Configuration/rtd_config.json";
    public string ProfitDdeConfigPath { get; set; } = "Configuration/profit_dde_config.json";
    public string Mt5ConfigPath { get; set; } = "Configuration/mt5_config.json";
    public string Mt5Host { get; set; } = "localhost";
    public int Mt5Port { get; set; } = 8228;
    public string NinjaTraderBaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>Base URL da MarketData.API para modo ProfitDLL (ex: https://localhost:5242).</summary>
    public string MarketDataApiBaseUrl { get; set; } = "https://localhost:5242";

    /// <summary>Tickers assinados no marketHub quando modo = ProfitDll.</summary>
    public string[] ProfitDllTickers { get; set; } =
        ["WINFUT", "WDOFUT", "DOLFUT", "PETR4", "VALE3", "ITUB4", "BBDC4"];

    public ProfitMarketDataMode ResolveProfitMarketDataMode()
    {
        if (ProfitMarketDataModeExtensions.TryParse(ProfitMarketDataMode, out var parsed)
            && !string.IsNullOrWhiteSpace(ProfitMarketDataMode))
            return parsed;

        if (EnableProfitDde)
            return Configuration.ProfitMarketDataMode.Dde;

        if (EnableProfit)
            return Configuration.ProfitMarketDataMode.Rtd;

        return Configuration.ProfitMarketDataMode.Dde;
    }

    public bool IsProfitTickSourceEnabled(ProfitMarketDataMode mode) =>
        ResolveProfitMarketDataMode() == mode;
}

