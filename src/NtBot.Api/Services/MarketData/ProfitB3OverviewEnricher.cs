using NtBot.MarketData.Services;
using NtBot.MarketIntelligence.Configuration;
using NtBot.MarketIntelligence.Models;
using NtBot.MarketIntelligence.Providers;

namespace NtBot.Api.Services.MarketData;

public sealed class ProfitB3OverviewEnricher : IMarketOverviewEnricher
{
    private readonly IB3EquitySnapshotProvider _b3;

    public ProfitB3OverviewEnricher(IB3EquitySnapshotProvider b3) => _b3 = b3;

    public async Task<MarketOverview> EnrichAsync(MarketOverview overview, CancellationToken cancellationToken = default)
    {
        try
        {
            var b3Snapshots = await _b3.GetSnapshotsAsync(cancellationToken);
            if (b3Snapshots.Count == 0)
                return overview;

            var marketSnapshots = b3Snapshots.Select(s => new MarketSnapshot
            {
                Timestamp = s.Timestamp,
                Provider = "ProfitDLL",
                Symbol = s.Symbol,
                Name = s.Name,
                Category = MarketCategory.Equity,
                Price = s.Price,
                Change = 0,
                ChangePercent = 0,
                Open = s.Price,
                High = s.Price,
                Low = s.Price,
                PreviousClose = s.Price,
                MarketStatus = MarketStatus.Open
            }).ToList();

            var lookup = overview.Commodities
                .Concat(overview.Indexes)
                .Concat(overview.Currencies)
                .Concat(overview.Treasury)
                .Concat(overview.Sectors)
                .GroupBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var snap in marketSnapshots)
                lookup[snap.Symbol] = snap;

            return new MarketOverview
            {
                Timestamp = DateTime.UtcNow,
                Provider = overview.Provider + "+ProfitB3",
                Commodities = overview.Commodities,
                Indexes = lookup.Values.Where(s => s.Category == MarketCategory.Index).Concat(marketSnapshots).ToList(),
                Currencies = overview.Currencies,
                Treasury = overview.Treasury,
                Sectors = lookup.Values.Where(s => s.Category == MarketCategory.Sector || s.Category == MarketCategory.Equity).ToList(),
                MarketRegime = overview.MarketRegime,
                Vix = overview.Vix
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return overview;
        }
        catch (OperationCanceledException)
        {
            return overview;
        }
        catch
        {
            return overview;
        }
    }
}
