using NtBot.Web.Models;

namespace NtBot.Web.Services;

public static class MarketOverviewMerger
{
    private static readonly Dictionary<string, string> YahooSymbolsToReplace =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = "EURUSD=X",
            ["GBPUSD"] = "GBPUSD=X",
            ["USDJPY"] = "JPY=X",
            ["AUDUSD"] = "AUDUSD=X",
            ["NZDUSD"] = "NZDUSD=X",
            ["USDCAD"] = "USDCAD=X",
            ["USDCHF"] = "USDCHF=X",
            ["XAUUSD"] = "GC=F",
        };

    public static void ApplyMt5Forex(MarketOverviewModel overview, IReadOnlyList<MarketSnapshotModel> mt5Currencies)
    {
        if (mt5Currencies.Count == 0)
            return;

        var yahooToReplace = YahooSymbolsToReplace.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        overview.Currencies = mt5Currencies
            .Concat(overview.Currencies.Where(c => !yahooToReplace.Contains(c.Symbol)))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var xau = mt5Currencies.FirstOrDefault(c =>
            c.Symbol.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase));
        if (xau is not null)
        {
            overview.Commodities = overview.Commodities
                .Where(c => !c.Symbol.Equals("GC=F", StringComparison.OrdinalIgnoreCase))
                .ToList();
            overview.Commodities.Insert(0, new MarketSnapshotModel
            {
                Timestamp = xau.Timestamp,
                Provider = "MT5",
                Symbol = "XAUUSD",
                Name = "Gold (MT5)",
                Category = "Commodity",
                Price = xau.Price,
                Open = xau.Open,
                High = xau.High,
                Low = xau.Low,
                MarketStatus = xau.MarketStatus
            });
        }

        overview.Provider = overview.Provider.Contains("MT5", StringComparison.OrdinalIgnoreCase)
            ? overview.Provider
            : $"MT5 + {overview.Provider}";
        overview.Timestamp = DateTime.UtcNow;
    }
}
