using NtBot.Domain.Entities;

namespace NtBot.Infrastructure.Cache;

/// <summary>
/// Cache em memória para configurações persistidas no banco (providers, composições, ativos).
/// Carrega na primeira leitura; invalida ou atualiza quando a configuração muda.
/// </summary>
public interface IDbConfigurationCache
{
    Task<IReadOnlyList<MacroProvider>> GetMacroProvidersAsync(CancellationToken cancellationToken = default);
    Task<MacroProvider?> GetMacroProviderByNameAsync(string name, CancellationToken cancellationToken = default);
    void UpdateMacroProvider(MacroProvider provider);
    void InvalidateMacroProviders();

    Task<IReadOnlyList<MarketIntelligenceProvider>> GetMarketIntelligenceProvidersAsync(
        CancellationToken cancellationToken = default);
    Task<MarketIntelligenceProvider?> GetMarketIntelligenceProviderByNameAsync(
        string name,
        CancellationToken cancellationToken = default);
    void UpdateMarketIntelligenceProvider(MarketIntelligenceProvider provider);
    void InvalidateMarketIntelligenceProviders();

    Task<IReadOnlyList<DriverComposition>> GetDriverCompositionsAsync(
        string targetAsset,
        Guid? tenantId,
        bool enabledOnly,
        CancellationToken cancellationToken = default);

    void InvalidateDriverCompositions(string? targetAsset = null);

    Task<IReadOnlyList<AssetConfiguration>> GetAssetConfigurationsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    void InvalidateAssetConfigurations(Guid? tenantId = null);
}
