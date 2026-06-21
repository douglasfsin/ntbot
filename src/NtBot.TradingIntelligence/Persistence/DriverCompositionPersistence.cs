using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;
using NtBot.MarketDrivers.Configuration;
using NtBot.MarketDrivers.Providers;
using NtBot.TradingIntelligence.Services;

namespace NtBot.TradingIntelligence.Persistence;

public sealed class DriverCompositionRepository : IDriverCompositionRepository
{
    private readonly NtBotDbContext _db;

    public DriverCompositionRepository(NtBotDbContext db) => _db = db;

    public async Task<IReadOnlyList<DriverComposition>> ListAsync(
        string targetAsset,
        Guid? tenantId,
        bool enabledOnly = true,
        CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(targetAsset);
        var query = _db.DriverCompositions.AsNoTracking()
            .Where(d => d.TargetAsset == normalized);

        if (enabledOnly)
            query = query.Where(d => d.Enabled);

        query = tenantId.HasValue
            ? query.Where(d => d.TenantId == tenantId || d.TenantId == null)
            : query.Where(d => d.TenantId == null);

        return await query.OrderBy(d => d.DisplayOrder).ToListAsync(cancellationToken);
    }

    public Task<DriverComposition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.DriverCompositions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<DriverComposition> AddAsync(DriverComposition entity, CancellationToken cancellationToken = default)
    {
        _db.DriverCompositions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(DriverComposition entity, CancellationToken cancellationToken = default)
    {
        _db.DriverCompositions.Update(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(DriverComposition entity, CancellationToken cancellationToken = default)
    {
        _db.DriverCompositions.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByTargetAsync(string targetAsset, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(targetAsset);
        var items = await _db.DriverCompositions
            .Where(d => d.TargetAsset == normalized && d.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        _db.DriverCompositions.RemoveRange(items);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DriverCompositionStore : IDriverCompositionStore
{
    private readonly NtBotDbContext _db;

    public DriverCompositionStore(NtBotDbContext db) => _db = db;

    public async Task<IReadOnlyList<DriverSourceDefinition>> GetSourcesAsync(
        string targetAsset,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(targetAsset);

        var dbItems = await _db.DriverCompositions.AsNoTracking()
            .Where(d => d.TargetAsset == normalized && d.Enabled)
            .Where(d => tenantId == null ? d.TenantId == null : d.TenantId == tenantId || d.TenantId == null)
            .OrderBy(d => d.DisplayOrder)
            .ToListAsync(cancellationToken);

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
        return await _db.DriverCompositions.AsNoTracking()
            .AnyAsync(d => d.TargetAsset == normalized && d.Enabled &&
                           (tenantId == null ? d.TenantId == null : d.TenantId == tenantId || d.TenantId == null),
                cancellationToken);
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
