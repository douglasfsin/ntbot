namespace NtBot.MarketIntelligence.Configuration;

public static class MarketProviderNames
{
    public const string YahooFinance = "Yahoo Finance";
}

public sealed class MarketIntelligenceOptions
{
    public const string SectionName = "MarketIntelligence";

    public bool UseRedis { get; set; }
    public string? RedisConnectionString { get; set; }
    public int DefaultRefreshSeconds { get; set; } = 60;
    public int HistoryDays { get; set; } = 130;
}

public sealed record MarketAssetDefinition(
    string Symbol,
    string Name,
    MarketCategory Category,
    string? ShortLabel = null);

public enum MarketCategory
{
    Commodity,
    Index,
    Currency,
    Treasury,
    Sector,
    Equity
}

public static class MarketAssetCatalog
{
    public static IReadOnlyList<MarketAssetDefinition> All { get; } =
    [
        // Commodities
        new("CL=F", "Crude Oil WTI", MarketCategory.Commodity, "WTI"),
        new("BZ=F", "Brent Oil", MarketCategory.Commodity, "Brent"),
        new("NG=F", "Natural Gas", MarketCategory.Commodity, "Natural Gas"),
        new("GC=F", "Gold", MarketCategory.Commodity, "Gold"),
        new("SI=F", "Silver", MarketCategory.Commodity, "Silver"),
        new("HG=F", "Copper", MarketCategory.Commodity, "Copper"),
        // Indexes
        new("^GSPC", "S&P 500", MarketCategory.Index, "S&P500"),
        new("^IXIC", "NASDAQ", MarketCategory.Index, "NASDAQ"),
        new("^DJI", "Dow Jones", MarketCategory.Index, "DOW"),
        new("^RUT", "Russell 2000", MarketCategory.Index, "RUT"),
        new("^VIX", "VIX", MarketCategory.Index, "VIX"),
        // Currencies
        new("BRL=X", "USD/BRL", MarketCategory.Currency, "USD/BRL"),
        new("EURUSD=X", "EUR/USD", MarketCategory.Currency, "EUR/USD"),
        new("JPY=X", "USD/JPY", MarketCategory.Currency, "USD/JPY"),
        new("GBPUSD=X", "GBP/USD", MarketCategory.Currency, "GBP/USD"),
        // Treasury
        new("^TNX", "US 10Y", MarketCategory.Treasury, "US10Y"),
        new("^IRX", "US 13W", MarketCategory.Treasury, "US13W"),
        // Sectors
        new("XLE", "Energy", MarketCategory.Sector, "Energy"),
        new("XLF", "Financial", MarketCategory.Sector, "Financial"),
        new("XLK", "Technology", MarketCategory.Sector, "Technology"),
        new("XLI", "Industrial", MarketCategory.Sector, "Industrial"),
        new("XLV", "Healthcare", MarketCategory.Sector, "Healthcare")
    ];

    public static MarketAssetDefinition? Find(string symbol) =>
        All.FirstOrDefault(a => string.Equals(a.Symbol, symbol, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<MarketAssetDefinition> ByCategory(MarketCategory category) =>
        All.Where(a => a.Category == category).ToList();
}

public static class MarketAssetRelations
{
    public sealed record Relation(string Asset, IReadOnlyList<(string Symbol, string Label)> Drivers);

    public static IReadOnlyList<Relation> All { get; } =
    [
        new("PETR4", [("CL=F", "WTI"), ("BZ=F", "Brent"), ("BRL=X", "USD"), ("XLE", "Energy ETF")]),
        new("VALE3", [("HG=F", "Copper"), ("GC=F", "Gold"), ("BRL=X", "USD"), ("XLI", "Materials ETF")]),
        new("WIN", [
            ("PETR4", "PETR4"), ("VALE3", "VALE3"), ("ITUB4", "ITUB4"),
            ("BBDC4", "BBDC4"), ("ABEV3", "ABEV3"), ("WEGE3", "WEGE3")
        ])
    ];
}

public static class QuantScoreWeights
{
    public const decimal Macro = 0.30m;
    public const decimal Commodities = 0.20m;
    public const decimal Correlation = 0.20m;
    public const decimal Volatility = 0.10m;
    public const decimal Currencies = 0.10m;
    public const decimal Momentum = 0.10m;
}
