using NtBot.Connector.Windows.Configuration;

namespace NtBot.Connector.Windows.Providers.Profit;

public sealed class DdeReplayStatus
{
    public bool Enabled { get; init; }
    public IReadOnlyDictionary<string, string> Contracts { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<DdeReplayAssetRow> Assets { get; init; } = [];
    public bool IsConnected { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class DdeReplayAssetRow
{
    public string LogicalSymbol { get; init; } = string.Empty;
    public string DdeSymbol { get; init; } = string.Empty;
    public string? MirrorFromSymbol { get; init; }
    public bool IsMirrorOnly { get; init; }
    public bool IsActive { get; init; }
}

public interface IDdeReplayController
{
    DdeReplayStatus GetStatus();
    DdeReplayStatus SetReplayMode(bool enabled, IReadOnlyDictionary<string, string>? contracts = null);
    void RequestResubscribe();
}
