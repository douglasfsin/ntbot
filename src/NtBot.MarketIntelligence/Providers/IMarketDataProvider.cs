using NtBot.MarketIntelligence.Models;

namespace NtBot.MarketIntelligence.Providers;

public interface IMarketDataProvider
{
    string Name { get; }
    IReadOnlyList<string> Capabilities { get; }

    Task<MarketProviderRuntimeInfo> GetRuntimeInfoAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketSnapshot>> FetchSnapshotsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PriceHistoryPoint>> FetchHistoryAsync(string symbol, int days, CancellationToken cancellationToken = default);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
