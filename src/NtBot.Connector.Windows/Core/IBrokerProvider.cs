using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.Core;

public interface IMarketDataProvider
{
    BrokerSource Source { get; }
    bool IsConnected { get; }
    Task ConnectAsync(CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);
    event Action<NormalizedMarketTick>? OnTick;
    event Action<NormalizedBrokerStatus>? OnStatusChanged;
}

public interface ITradingProvider
{
    Task<IReadOnlyList<NormalizedOrder>> GetOpenOrdersAsync(CancellationToken ct);
    Task<IReadOnlyList<NormalizedExecution>> GetRecentExecutionsAsync(CancellationToken ct);
}

public interface IPositionProvider
{
    Task<IReadOnlyList<NormalizedPosition>> GetPositionsAsync(CancellationToken ct);
}

public interface IOrderProvider
{
    Task<IReadOnlyList<NormalizedOrder>> GetOrdersAsync(CancellationToken ct);
}

public interface IBrokerPlugin : IMarketDataProvider, ITradingProvider, IPositionProvider, IOrderProvider
{
    string Name { get; }
    Task<NormalizedAccount?> GetAccountAsync(CancellationToken ct);
}
