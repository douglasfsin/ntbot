namespace NtBot.MarketDrivers.Providers;

public interface IDriverCompositionStore
{
    Task<IReadOnlyList<Configuration.DriverSourceDefinition>> GetSourcesAsync(
        string targetAsset,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<bool> HasCustomCompositionAsync(
        string targetAsset,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);
}
