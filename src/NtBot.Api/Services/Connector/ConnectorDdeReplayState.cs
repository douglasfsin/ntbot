using System.Text.Json;

namespace NtBot.Api.Services.Connector;

public sealed class ConnectorDdeReplayStateDto
{
    public bool Enabled { get; set; }
    public Dictionary<string, string> Contracts { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WIN"] = "WINFUT",
        ["WDO"] = "WDOFUT"
    };
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}

public interface IConnectorDdeReplayState
{
    ConnectorDdeReplayStateDto Get();
    ConnectorDdeReplayStateDto Set(bool enabled, IReadOnlyDictionary<string, string>? contracts = null, string? updatedBy = null);
}

public sealed class ConnectorDdeReplayState : IConnectorDdeReplayState
{
    private readonly object _lock = new();
    private ConnectorDdeReplayStateDto _state = new();

    public ConnectorDdeReplayStateDto Get()
    {
        lock (_lock)
        {
            return Clone(_state);
        }
    }

    public ConnectorDdeReplayStateDto Set(
        bool enabled,
        IReadOnlyDictionary<string, string>? contracts = null,
        string? updatedBy = null)
    {
        lock (_lock)
        {
            _state.Enabled = enabled;
            if (contracts is { Count: > 0 })
            {
                foreach (var (key, value) in contracts)
                {
                    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                        continue;
                    _state.Contracts[key.Trim().ToUpperInvariant()] = value.Trim().ToUpperInvariant();
                }
            }

            _state.UpdatedUtc = DateTime.UtcNow;
            _state.UpdatedBy = updatedBy;
            return Clone(_state);
        }
    }

    private static ConnectorDdeReplayStateDto Clone(ConnectorDdeReplayStateDto source) => new()
    {
        Enabled = source.Enabled,
        Contracts = new Dictionary<string, string>(source.Contracts, StringComparer.OrdinalIgnoreCase),
        UpdatedUtc = source.UpdatedUtc,
        UpdatedBy = source.UpdatedBy
    };
}
