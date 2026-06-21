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
    public bool EnableProfit { get; set; } = true;
    public bool EnableMt5 { get; set; }
    public bool EnableNinjaTrader { get; set; }
    public bool EnableTradingView { get; set; }
    public string ProfitRtdConfigPath { get; set; } = "Configuration/rtd_config.json";
    public string Mt5ConfigPath { get; set; } = "Configuration/mt5_config.json";
    public string Mt5Host { get; set; } = "localhost";
    public int Mt5Port { get; set; } = 8228;
    public string NinjaTraderBaseUrl { get; set; } = "http://localhost:8080";
}
