using NtBot.Connector.Windows.Providers.Profit;

namespace NtBot.Connector.Windows.Configuration;

/// <summary>
/// Unifica ativos de profit_dde_config.json e rtd_config.json — cada contrato/ticker vira uma assinatura DDE.
/// </summary>
public static class ProfitMarketAssetResolver
{
    public static List<ProfitDdeAssetConfig> ResolveAll(
        ProfitDdeConfig dde,
        IReadOnlyDictionary<string, ProfitRtdConfigEntry> rtd)
    {
        var map = new Dictionary<string, ProfitDdeAssetConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in dde.Assets)
        {
            if (!asset.IsActive)
                continue;

            map[asset.LogicalSymbol] = CopyAsset(asset);
        }

        foreach (var (logical, entry) in rtd)
        {
            if (!entry.IsActive)
                continue;

            var tickers = (entry.TICKERS ?? [])
                .Prepend(entry.TICK)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (map.TryGetValue(logical, out var existing))
            {
                MergeAliases(existing, tickers);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.TICK))
                Upsert(map, logical, entry.TICK, tickers);

            foreach (var ticker in tickers)
            {
                if (map.ContainsKey(ticker))
                    continue;

                Upsert(map, ticker, ticker, [ticker]);
            }
        }

        return map.Values
            .OrderBy(a => SubscriptionPriority(a.LogicalSymbol))
            .ThenBy(a => a.LogicalSymbol, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int SubscriptionPriority(string symbol) => symbol.ToUpperInvariant() switch
    {
        "WIN" => 0,
        "WDO" => 1,
        var s when s.StartsWith("WIN", StringComparison.Ordinal) => 2,
        var s when s.StartsWith("WDO", StringComparison.Ordinal) => 3,
        var s when s.StartsWith("DI", StringComparison.Ordinal) => 5,
        _ => 10
    };

    private static ProfitDdeAssetConfig CopyAsset(ProfitDdeAssetConfig asset) => new()
    {
        LogicalSymbol = asset.LogicalSymbol,
        DdeSymbol = asset.ResolveSymbol(),
        DdeTopic = asset.DdeTopic,
        PriceItem = asset.PriceItem,
        BidItem = asset.BidItem,
        AskItem = asset.AskItem,
        DdeAliases = asset.DdeAliases?.ToList(),
        MirrorFromSymbol = asset.MirrorFromSymbol,
        IsActive = true
    };

    private static void MergeAliases(ProfitDdeAssetConfig asset, IEnumerable<string> tickers)
    {
        var aliases = asset.DdeAliases?.ToList() ?? [];
        foreach (var ticker in tickers)
        {
            if (string.IsNullOrWhiteSpace(ticker))
                continue;

            if (ticker.Equals(asset.DdeSymbol, StringComparison.OrdinalIgnoreCase)
                || ticker.Equals(asset.LogicalSymbol, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!aliases.Contains(ticker, StringComparer.OrdinalIgnoreCase))
                aliases.Add(ticker);
        }

        if (aliases.Count > 0)
            asset.DdeAliases = aliases;
    }

    private static void Upsert(
        Dictionary<string, ProfitDdeAssetConfig> map,
        string logical,
        string ddeSymbol,
        IReadOnlyList<string> tickers)
    {
        logical = logical.Trim();
        ddeSymbol = ddeSymbol.Trim();
        if (string.IsNullOrWhiteSpace(logical) || string.IsNullOrWhiteSpace(ddeSymbol))
            return;

        var aliases = tickers
            .Where(t => !t.Equals(ddeSymbol, StringComparison.OrdinalIgnoreCase)
                        && !t.Equals(logical, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        map[logical] = new ProfitDdeAssetConfig
        {
            LogicalSymbol = logical,
            DdeSymbol = ddeSymbol,
            PriceItem = "ULT",
            BidItem = "QC",
            AskItem = "QV",
            DdeAliases = aliases.Count > 0 ? aliases : null,
            IsActive = true
        };
    }
}
