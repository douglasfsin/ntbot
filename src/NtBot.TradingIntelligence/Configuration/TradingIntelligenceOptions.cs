namespace NtBot.TradingIntelligence.Configuration;

public sealed class TradingIntelligenceOptions
{
    public const string SectionName = "TradingIntelligence";

    public int DefaultRefreshSeconds { get; set; } = 60;
    public int AiRefreshSeconds { get; set; } = 120;

    public IReadOnlyList<string> SupportedAssets { get; set; } =
        ["WIN", "WDO", "PETR4", "VALE3", "XAUUSD", "SP500", "NASDAQ", "BTCUSD"];

    public IReadOnlyList<string> ChartTimeframes { get; set; } = ["5", "15", "30", "60"];

    /// <summary>Webhook n8n para AI Workspace (opcional).</summary>
    public string? N8nWebhookUrl { get; set; }
}

public static class ConfluenceWeights
{
    public const decimal Macro = 0.20m;
    public const decimal Drivers = 0.20m;
    public const decimal Wyckoff = 0.15m;
    public const decimal Smc = 0.15m;
    public const decimal Volume = 0.10m;
    public const decimal Momentum = 0.10m;
    public const decimal Correlation = 0.05m;
    public const decimal Liquidity = 0.03m;
    public const decimal Calendar = 0.02m;
}

public static class ConfluenceClassification
{
    public static string Classify(int score) => score switch
    {
        >= 95 => "Extreme Confluence",
        >= 85 => "Very High",
        >= 70 => "High",
        >= 55 => "Moderate",
        >= 40 => "Neutral",
        >= 20 => "Weak",
        _ => "Very Weak"
    };
}
