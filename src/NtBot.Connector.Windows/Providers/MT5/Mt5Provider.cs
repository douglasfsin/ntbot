using NtBot.Connector.Windows.Core;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.Providers.MT5;

public class Mt5Provider : IBrokerPlugin
{
    private readonly ILogger<Mt5Provider> _logger;
    private bool _connected;

    public Mt5Provider(ILogger<Mt5Provider> logger) => _logger = logger;

    public BrokerSource Source => BrokerSource.MT5;
    public string Name => "MetaTrader 5";
    public bool IsConnected => _connected;

    public event Action<NormalizedMarketTick>? OnTick;
    public event Action<NormalizedBrokerStatus>? OnStatusChanged;

    public Task ConnectAsync(CancellationToken ct)
    {
        _connected = true;
        _logger.LogInformation("MT5 provider stub ativo");
        OnStatusChanged?.Invoke(Status("connected"));
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken ct)
    {
        _connected = false;
        OnStatusChanged?.Invoke(Status("disconnected"));
        return Task.CompletedTask;
    }

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

    private static NormalizedBrokerStatus Status(string status) => new()
    {
        Source = BrokerSource.MT5,
        IsConnected = status == "connected",
        Status = status,
        TimestampUtc = DateTime.UtcNow
    };
}
