namespace NtBot.Macro.Providers;

public interface IMacroProvider
{
    string Name { get; }
    int Priority { get; }
    IReadOnlyList<string> Capabilities { get; }
    Task<MacroProviderRuntimeInfo> GetRuntimeInfoAsync(CancellationToken cancellationToken = default);
    Task<MacroProviderPayload?> FetchAsync(CancellationToken cancellationToken = default);
}

public sealed class MacroProviderRuntimeInfo
{
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public int Priority { get; init; }
    public MacroProviderHealth HealthStatus { get; init; }
    public DateTime? LastUpdate { get; init; }
    public IReadOnlyList<string> Capabilities { get; init; } = [];
}
