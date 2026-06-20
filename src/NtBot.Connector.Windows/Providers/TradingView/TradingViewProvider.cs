using NtBot.Connector.Windows.Core;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.Providers.TradingView;

public class TradingViewProvider : IBrokerPlugin
{
    public BrokerSource Source => BrokerSource.TradingView;
    public string Name => "TradingView";
    public bool IsConnected { get; private set; }

    public event Action<NormalizedMarketTick>? OnTick;
    public event Action<NormalizedBrokerStatus>? OnStatusChanged;

    public Task ConnectAsync(CancellationToken ct)
    {
        IsConnected = false;
        OnStatusChanged?.Invoke(new NormalizedBrokerStatus
        {
            Source = BrokerSource.TradingView,
            IsConnected = false,
            Status = "stub",
            Message = "Plugin reservado para alertas TradingView",
            TimestampUtc = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<NormalizedOrder>> GetOpenOrdersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedOrder>>([]);

    public Task<IReadOnlyList<NormalizedOrder>> GetOrdersAsync(CancellationToken ct) =>
        GetOpenOrdersAsync(ct);

    public Task<IReadOnlyList<NormalizedExecution>> GetRecentExecutionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedExecution>>([]);

    public Task<IReadOnlyList<NormalizedPosition>> GetPositionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedPosition>>([]);

    public Task<NormalizedAccount?> GetAccountAsync(CancellationToken ct) =>
        Task.FromResult<NormalizedAccount?>(null);
}
