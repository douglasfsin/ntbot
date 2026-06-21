using NtBot.MarketDrivers.Models;

namespace NtBot.MarketDrivers.Providers;

public interface IMarketDriverProvider
{
    string Name { get; }
    IReadOnlyList<string> Capabilities { get; }
    Task<IReadOnlyList<MarketDriver>> BuildDriversAsync(MarketDriverContext context, CancellationToken cancellationToken = default);
}
