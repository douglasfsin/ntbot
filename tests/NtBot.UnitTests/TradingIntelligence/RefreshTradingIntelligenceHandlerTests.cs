using NtBot.TradingIntelligence.Commands;
using NtBot.TradingIntelligence.Models;
using NtBot.TradingIntelligence.Services;

namespace NtBot.UnitTests.TradingIntelligence;

public class RefreshTradingIntelligenceHandlerTests
{
    [Fact]
    public async Task Handle_SingleAsset_RefreshesAndNotifies()
    {
        var snapshot = new TradingIntelligenceSnapshot { Asset = "WIN" };
        var service = new FakeTradingIntelligenceService { Snapshot = snapshot };
        var notifier = new FakeNotifier();
        var handler = new RefreshTradingIntelligenceHandler(service, notifier);

        var result = await handler.Handle(
            new RefreshTradingIntelligenceCommand("WIN", NotifyClients: true),
            CancellationToken.None);

        Assert.Equal(1, result.Refreshed);
        Assert.Single(result.Snapshots);
        Assert.Equal("WIN", result.Snapshots[0].Asset);
        Assert.Equal(1, notifier.NotifyCount);
    }

    [Fact]
    public async Task Handle_AllAssets_RefreshesEachWithoutNotifyWhenDisabled()
    {
        var service = new FakeTradingIntelligenceService
        {
            AllSnapshots =
            [
                new TradingIntelligenceSnapshot { Asset = "WIN" },
                new TradingIntelligenceSnapshot { Asset = "WDO" }
            ]
        };
        var notifier = new FakeNotifier();
        var handler = new RefreshTradingIntelligenceHandler(service, notifier);

        var result = await handler.Handle(
            new RefreshTradingIntelligenceCommand(NotifyClients: false),
            CancellationToken.None);

        Assert.Equal(2, result.Refreshed);
        Assert.Equal(0, notifier.NotifyCount);
    }

    private sealed class FakeTradingIntelligenceService : ITradingIntelligenceService
    {
        public TradingIntelligenceSnapshot? Snapshot { get; init; }
        public IReadOnlyList<TradingIntelligenceSnapshot> AllSnapshots { get; init; } = [];

        public Task<TradingIntelligenceSnapshot?> GetSnapshotAsync(string asset, Guid? tenantId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Snapshot);

        public Task<TradingIntelligenceSnapshot?> RefreshSnapshotAsync(string asset, Guid? tenantId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Snapshot);

        public Task<IReadOnlyList<TradingIntelligenceSnapshot>> RefreshAllAsync(Guid? tenantId = null, bool notifyClients = true, CancellationToken cancellationToken = default)
            => Task.FromResult(AllSnapshots);

        public Task<IReadOnlyList<TradingIntelligenceDashboardItem>> GetDashboardAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TradingIntelligenceDashboardItem>>([]);

        public TradingIntelligenceStatus GetStatus() => new();
    }

    private sealed class FakeNotifier : ITradingIntelligenceUpdateNotifier
    {
        public int NotifyCount { get; private set; }

        public Task NotifySnapshotUpdatedAsync(TradingIntelligenceSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            NotifyCount++;
            return Task.CompletedTask;
        }
    }
}
