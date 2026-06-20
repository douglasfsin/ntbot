using System.Collections.Concurrent;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.Services;

public sealed class PlatformStatusEntry
{
    public required string Name { get; init; }
    public BrokerSource Source { get; init; }
    public bool IsConnected { get; init; }
    public bool IsEnabled { get; init; }
    public string Status { get; init; } = "unknown";
    public string? Message { get; init; }
    public DateTime UpdatedUtc { get; init; }
    public int ReconnectAttempts { get; init; }
}

public interface IPlatformStatusRegistry
{
    event Action? Changed;
    IReadOnlyList<PlatformStatusEntry> Snapshot { get; }
    void UpdatePlatform(string name, BrokerSource source, bool isEnabled, NormalizedBrokerStatus status, int reconnectAttempts = 0);
    void UpdateApi(bool isOnline, string status, string? message = null);
    PlatformStatusEntry? GetApiStatus();
}

public sealed class PlatformStatusRegistry : IPlatformStatusRegistry
{
    private readonly ConcurrentDictionary<string, PlatformStatusEntry> _platforms = new();
    private PlatformStatusEntry? _api;

    public event Action? Changed;

    public IReadOnlyList<PlatformStatusEntry> Snapshot =>
        _platforms.Values.OrderBy(p => p.Name).ToList();

    public void UpdatePlatform(string name, BrokerSource source, bool isEnabled, NormalizedBrokerStatus status, int reconnectAttempts = 0)
    {
        _platforms[name] = new PlatformStatusEntry
        {
            Name = name,
            Source = source,
            IsConnected = status.IsConnected,
            IsEnabled = isEnabled,
            Status = status.Status,
            Message = status.Message,
            UpdatedUtc = status.TimestampUtc,
            ReconnectAttempts = reconnectAttempts
        };
        Changed?.Invoke();
    }

    public void UpdateApi(bool isOnline, string status, string? message = null)
    {
        _api = new PlatformStatusEntry
        {
            Name = "NTBot API",
            Source = BrokerSource.Unknown,
            IsConnected = isOnline,
            IsEnabled = true,
            Status = status,
            Message = message,
            UpdatedUtc = DateTime.UtcNow
        };
        Changed?.Invoke();
    }

    public PlatformStatusEntry? GetApiStatus() => _api;
}
