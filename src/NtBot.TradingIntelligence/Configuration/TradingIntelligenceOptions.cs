namespace NtBot.TradingIntelligence.Configuration;

public sealed class TradingIntelligenceOptions
{
    public const string SectionName = "TradingIntelligence";

    public int DefaultRefreshSeconds { get; set; } = 60;
    public int AiRefreshSeconds { get; set; } = 120;

    public IReadOnlyList<string> SupportedAssets { get; set; } =
        ["WIN", "WDO", "PETR4", "VALE3", "XAUUSD", "SP500", "NASDAQ", "BTCUSD"];

    public IReadOnlyList<string> DashboardAssets { get; set; } = ["WIN", "WDO", "PETR4"];

    public IReadOnlyList<string> ChartTimeframes { get; set; } = ["5", "15", "30", "60"];

    public bool UseRedis { get; set; }
    public string? RedisConnectionString { get; set; }
    public int CacheTtlSeconds { get; set; } = 55;

    /// <summary>Webhook n8n Master Agent (opcional).</summary>
    public string? N8nWebhookUrl { get; set; }

    /// <summary>Webhooks especialistas por ativo (ex: WIN → url).</summary>
    public Dictionary<string, string> N8nAssetWebhookUrls { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Timeframes extras para consenso multi-TF (além de <see cref="ChartTimeframes"/>).
    /// Ex.: H4/D1 para ouro quando o MT5 fornecer.
    /// </summary>
    public IReadOnlyList<string> ConsensusExtraTimeframes { get; set; } = ["240", "1440"];
}
