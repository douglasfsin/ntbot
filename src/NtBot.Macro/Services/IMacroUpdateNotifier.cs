namespace NtBot.Macro.Services;

public interface IMacroUpdateNotifier
{
    Task NotifySnapshotUpdatedAsync(MacroSnapshot snapshot, CancellationToken cancellationToken = default);
    Task NotifyProvidersUpdatedAsync(IReadOnlyList<MacroProviderStatusDto> providers, CancellationToken cancellationToken = default);
}

public sealed class NoOpMacroUpdateNotifier : IMacroUpdateNotifier
{
    public Task NotifySnapshotUpdatedAsync(MacroSnapshot snapshot, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task NotifyProvidersUpdatedAsync(IReadOnlyList<MacroProviderStatusDto> providers, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
