namespace NtBot.MarketDrivers.Configuration;

public sealed class MarketDriversOptions
{
    public const string SectionName = "MarketDrivers";

    public int DefaultRefreshSeconds { get; set; } = 60;
    public IReadOnlyList<string> DashboardAssets { get; set; } =
        ["PETR4", "VALE3", "WIN", "WDO", "XAUUSD"];
}

public static class DriverScoreWeights
{
    public const decimal Macro = 0.30m;
    public const decimal Quant = 0.25m;
    public const decimal Correlation = 0.15m;
    public const decimal Commodities = 0.10m;
    public const decimal Momentum = 0.10m;
    public const decimal Volatility = 0.05m;
    public const decimal Calendar = 0.05m;
}

public sealed record AssetDriverDefinition(
    string Asset,
    IReadOnlyList<DriverSourceDefinition> Sources);

public sealed record DriverSourceDefinition(
    string Symbol,
    string Label,
    MarketDriverCategory Category,
    decimal Weight,
    bool Inverse = false);

public enum MarketDriverCategory
{
    Macro,
    Commodities,
    Moedas,
    Treasury,
    Volatilidade,
    Correlacao,
    Momentum,
    Fluxo,
    EventosEconomicos,
    Setores,
    MarketBreadth,
    Sentimento
}

public static class MarketDriversCatalog
{
    public static IReadOnlyList<AssetDriverDefinition> All { get; } =
    [
        new("PETR4",
        [
            new("CL=F", "WTI", MarketDriverCategory.Commodities, 0.22m),
            new("BZ=F", "Brent", MarketDriverCategory.Commodities, 0.18m),
            new("BRL=X", "USD", MarketDriverCategory.Moedas, 0.12m, Inverse: true),
            new("XLE", "Energy ETF", MarketDriverCategory.Setores, 0.10m),
            new("^GSPC", "Ibovespa Proxy", MarketDriverCategory.MarketBreadth, 0.10m),
            new("^VIX", "VIX", MarketDriverCategory.Volatilidade, 0.08m, Inverse: true),
            new("MACRO", "Macro", MarketDriverCategory.Macro, 0.20m)
        ]),
        new("VALE3",
        [
            new("HG=F", "Copper", MarketDriverCategory.Commodities, 0.24m),
            new("GC=F", "Gold", MarketDriverCategory.Commodities, 0.12m),
            new("BRL=X", "USD", MarketDriverCategory.Moedas, 0.10m, Inverse: true),
            new("XLI", "Materials ETF", MarketDriverCategory.Setores, 0.10m),
            new("^GSPC", "Ibovespa Proxy", MarketDriverCategory.MarketBreadth, 0.10m),
            new("MACRO", "Macro", MarketDriverCategory.Macro, 0.20m),
            new("CORR", "Correlação", MarketDriverCategory.Correlacao, 0.14m)
        ]),
        new("WIN",
        [
            new("PETR4", "PETR4", MarketDriverCategory.Correlacao, 0.18m),
            new("VALE3", "VALE3", MarketDriverCategory.Correlacao, 0.12m),
            new("ITUB4", "ITUB4", MarketDriverCategory.Correlacao, 0.10m),
            new("BBDC4", "BBDC4", MarketDriverCategory.Correlacao, 0.08m),
            new("WEGE3", "WEGE3", MarketDriverCategory.Correlacao, 0.10m),
            new("ABEV3", "ABEV3", MarketDriverCategory.Correlacao, 0.07m),
            new("MACRO", "Macro", MarketDriverCategory.Macro, 0.20m),
            new("FLOW", "Fluxo", MarketDriverCategory.Fluxo, 0.15m)
        ]),
        new("WDO",
        [
            new("DX-Y.NYB", "Dollar Index", MarketDriverCategory.Moedas, 0.22m),
            new("^TNX", "US10Y", MarketDriverCategory.Treasury, 0.15m),
            new("MACRO_FED", "FED", MarketDriverCategory.Macro, 0.15m),
            new("BRL=X", "USD/BRL", MarketDriverCategory.Moedas, 0.18m),
            new("^VIX", "VIX", MarketDriverCategory.Volatilidade, 0.10m, Inverse: true),
            new("FLOW", "Fluxo", MarketDriverCategory.Fluxo, 0.10m),
            new("MOM", "Momentum", MarketDriverCategory.Momentum, 0.10m)
        ]),
        new("XAUUSD",
        [
            new("GC=F", "Gold", MarketDriverCategory.Commodities, 0.30m),
            new("^TNX", "Real Yield", MarketDriverCategory.Treasury, 0.18m, Inverse: true),
            new("DX-Y.NYB", "Dollar Index", MarketDriverCategory.Moedas, 0.18m, Inverse: true),
            new("^VIX", "VIX", MarketDriverCategory.Volatilidade, 0.12m),
            new("^IRX", "Treasury", MarketDriverCategory.Treasury, 0.10m),
            new("MACRO", "Macro", MarketDriverCategory.Macro, 0.12m)
        ])
    ];

    public static AssetDriverDefinition? Find(string asset) =>
        All.FirstOrDefault(a => string.Equals(a.Asset, asset, StringComparison.OrdinalIgnoreCase));

    public static bool IsSupported(string asset) => Find(asset) is not null;
}
