namespace NtBot.Connector.Windows.Configuration;

public class MarketDataGatewayOptions
{
    public const string SectionName = "MarketDataGateway";

    public int BatchIntervalMs { get; set; } = 50;
    public int ChannelCapacity { get; set; } = 8192;
    public int WarningStaleMs { get; set; } = 2000;
    public int ReconnectStaleMs { get; set; } = 5000;
    public int RestartStaleMs { get; set; } = 10000;
    public int WatchdogIntervalMs { get; set; } = 1000;
}
