using NtBot.MarketIntelligence.Configuration;
using NtBot.MarketIntelligence.Models;

namespace NtBot.MarketIntelligence.Engine;

public interface IMarketIntelligenceEngine
{
    MarketOverview BuildOverview(IReadOnlyList<MarketSnapshot> snapshots, string provider);
    IReadOnlyList<HeatMapCell> BuildHeatMap(IReadOnlyList<MarketSnapshot> snapshots);
    string DetectMarketRegime(MarketOverview overview);
}

public sealed class MarketIntelligenceEngine : IMarketIntelligenceEngine
{
    public MarketOverview BuildOverview(IReadOnlyList<MarketSnapshot> snapshots, string provider)
    {
        var overview = new MarketOverview
        {
            Timestamp = DateTime.UtcNow,
            Provider = provider,
            Commodities = snapshots.Where(s => s.Category == MarketCategory.Commodity).ToList(),
            Indexes = snapshots.Where(s => s.Category == MarketCategory.Index).ToList(),
            Currencies = snapshots.Where(s => s.Category == MarketCategory.Currency).ToList(),
            Treasury = snapshots.Where(s => s.Category == MarketCategory.Treasury).ToList(),
            Sectors = snapshots.Where(s => s.Category == MarketCategory.Sector).ToList(),
            Vix = snapshots.FirstOrDefault(s => s.Symbol == "^VIX")
        };

        overview = new MarketOverview
        {
            Timestamp = overview.Timestamp,
            Provider = overview.Provider,
            Commodities = overview.Commodities,
            Indexes = overview.Indexes,
            Currencies = overview.Currencies,
            Treasury = overview.Treasury,
            Sectors = overview.Sectors,
            Vix = overview.Vix,
            MarketRegime = DetectMarketRegime(overview)
        };

        return overview;
    }

    public IReadOnlyList<HeatMapCell> BuildHeatMap(IReadOnlyList<MarketSnapshot> snapshots) =>
        snapshots.Select(s => new HeatMapCell
        {
            Symbol = s.Symbol,
            Label = MarketAssetCatalog.Find(s.Symbol)?.ShortLabel ?? s.Name,
            Category = s.Category,
            ChangePercent = s.ChangePercent
        }).ToList();

    public string DetectMarketRegime(MarketOverview overview)
    {
        var vix = overview.Vix?.Price ?? 20;
        var spx = overview.Indexes.FirstOrDefault(i => i.Symbol == "^GSPC")?.ChangePercent ?? 0;

        if (vix >= 25 || spx <= -1.5m)
            return "Risk-Off";
        if (vix <= 16 && spx >= 0.5m)
            return "Risk-On";
        return "Neutral";
    }
}
