using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Cache;
using NtBot.Infrastructure.Persistence;
using NtBot.MarketDrivers.Configuration;
using NtBot.MarketDrivers.Providers;
using NtBot.TradingIntelligence.Services;

namespace NtBot.TradingIntelligence.Persistence;

public sealed class DriverCompositionRepository : IDriverCompositionRepository
{
    private readonly NtBotDbContext _db;
    private readonly IDbConfigurationCache _configCache;

    public DriverCompositionRepository(NtBotDbContext db, IDbConfigurationCache configCache)
    {
        _db = db;
        _configCache = configCache;
    }

    public Task<IReadOnlyList<DriverComposition>> ListAsync(
        string targetAsset,
        Guid? tenantId,
        bool enabledOnly = true,
        CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(targetAsset);
        return _configCache.GetDriverCompositionsAsync(normalized, tenantId, enabledOnly, cancellationToken);
    }

    public Task<DriverComposition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.DriverCompositions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<DriverComposition> AddAsync(DriverComposition entity, CancellationToken cancellationToken = default)
    {
        _db.DriverCompositions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        _configCache.InvalidateDriverCompositions(entity.TargetAsset);
        return entity;
    }

    public async Task UpdateAsync(DriverComposition entity, CancellationToken cancellationToken = default)
    {
        _db.DriverCompositions.Update(entity);
        await _db.SaveChangesAsync(cancellationToken);
        _configCache.InvalidateDriverCompositions(entity.TargetAsset);
    }

    public async Task DeleteAsync(DriverComposition entity, CancellationToken cancellationToken = default)
    {
        _db.DriverCompositions.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        _configCache.InvalidateDriverCompositions(entity.TargetAsset);
    }

    public async Task DeleteByTargetAsync(string targetAsset, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(targetAsset);
        var items = await _db.DriverCompositions
            .Where(d => d.TargetAsset == normalized && d.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        _db.DriverCompositions.RemoveRange(items);
        await _db.SaveChangesAsync(cancellationToken);
        _configCache.InvalidateDriverCompositions(normalized);
    }
}

public sealed class DriverCompositionStore : IDriverCompositionStore
{
    private readonly IDbConfigurationCache _configCache;

    public DriverCompositionStore(IDbConfigurationCache configCache) => _configCache = configCache;

    public async Task<IReadOnlyList<DriverSourceDefinition>> GetSourcesAsync(
        string targetAsset,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(targetAsset);

        var dbItems = await _configCache.GetDriverCompositionsAsync(
            normalized,
            tenantId,
            enabledOnly: true,
            cancellationToken);

        if (dbItems.Count == 0)
            return [];

        return dbItems.Select(Map).ToList();
    }

    public async Task<bool> HasCustomCompositionAsync(
        string targetAsset,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(targetAsset);
        var items = await _configCache.GetDriverCompositionsAsync(
            normalized,
            tenantId,
            enabledOnly: true,
            cancellationToken);
        return items.Count > 0;
    }

    private static DriverSourceDefinition Map(DriverComposition row)
    {
        var category = Enum.TryParse<MarketDriverCategory>(row.Category, true, out var parsed)
            ? parsed
            : row.DriverAsset switch
            {
                "MACRO" or "MACRO_FED" => MarketDriverCategory.Macro,
                "FLOW" => MarketDriverCategory.Fluxo,
                "CORR" => MarketDriverCategory.Correlacao,
                "MOM" => MarketDriverCategory.Momentum,
                _ => MarketDriverCategory.Correlacao
            };

        return new DriverSourceDefinition(
            row.DriverAsset,
            row.Description ?? row.DriverAsset,
            category,
            row.Weight,
            row.Inverse);
    }
}
