using NtBot.Domain.Entities;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Services;

public interface IDriverCompositionRepository
{
    Task<IReadOnlyList<DriverComposition>> ListAsync(string targetAsset, Guid? tenantId, bool enabledOnly = true, CancellationToken cancellationToken = default);
    Task<DriverComposition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DriverComposition> AddAsync(DriverComposition entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(DriverComposition entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(DriverComposition entity, CancellationToken cancellationToken = default);
    Task DeleteByTargetAsync(string targetAsset, Guid? tenantId, CancellationToken cancellationToken = default);
}

public sealed class DriverCompositionAdminService : IDriverCompositionAdminService
{
    private readonly IDriverCompositionRepository _repo;

    public DriverCompositionAdminService(IDriverCompositionRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<DriverCompositionDto>> ListAsync(
        string targetAsset,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(targetAsset);
        var items = await _repo.ListAsync(normalized, tenantId, enabledOnly: false, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<DriverCompositionDto?> CreateAsync(
        DriverCompositionUpsertRequest request,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var entity = new DriverComposition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TargetAsset = Macro.Configuration.MacroSymbolAliases.Normalize(request.TargetAsset),
            DriverAsset = request.DriverAsset.Trim().ToUpperInvariant(),
            Weight = request.Weight,
            Enabled = request.Enabled,
            DisplayOrder = request.DisplayOrder,
            Description = request.Description,
            Category = request.Category,
            Inverse = request.Inverse,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(entity, cancellationToken);
        return Map(entity);
    }

    public async Task<DriverCompositionDto?> UpdateAsync(
        Guid id,
        DriverCompositionUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(id, cancellationToken);
        if (entity is null) return null;

        entity.DriverAsset = request.DriverAsset.Trim().ToUpperInvariant();
        entity.Weight = request.Weight;
        entity.Enabled = request.Enabled;
        entity.DisplayOrder = request.DisplayOrder;
        entity.Description = request.Description;
        entity.Category = request.Category;
        entity.Inverse = request.Inverse;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(entity, cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repo.GetByIdAsync(id, cancellationToken);
        if (entity is null) return false;
        await _repo.DeleteAsync(entity, cancellationToken);
        return true;
    }

    public async Task<int> DuplicateAsync(
        string sourceAsset,
        string targetAsset,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var source = Macro.Configuration.MacroSymbolAliases.Normalize(sourceAsset);
        var target = Macro.Configuration.MacroSymbolAliases.Normalize(targetAsset);
        var items = await _repo.ListAsync(source, tenantId, enabledOnly: false, cancellationToken);

        await _repo.DeleteByTargetAsync(target, tenantId, cancellationToken);

        var count = 0;
        foreach (var item in items)
        {
            await _repo.AddAsync(new DriverComposition
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TargetAsset = target,
                DriverAsset = item.DriverAsset,
                Weight = item.Weight,
                Enabled = item.Enabled,
                DisplayOrder = item.DisplayOrder,
                Description = item.Description,
                Category = item.Category,
                Inverse = item.Inverse,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);
            count++;
        }

        return count;
    }

    public Task<IReadOnlyList<DriverCompositionDto>> ExportAsync(
        string targetAsset,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default) =>
        ListAsync(targetAsset, tenantId, cancellationToken);

    public async Task<int> ImportAsync(
        string targetAsset,
        IReadOnlyList<DriverCompositionUpsertRequest> items,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(targetAsset);
        await _repo.DeleteByTargetAsync(normalized, tenantId, cancellationToken);

        var order = 0;
        foreach (var item in items)
        {
            item.TargetAsset = normalized;
            item.DisplayOrder = item.DisplayOrder == 0 ? order++ : item.DisplayOrder;
            await CreateAsync(item, tenantId, cancellationToken);
        }

        return items.Count;
    }

    public async Task ReorderAsync(
        string targetAsset,
        IReadOnlyList<Guid> orderedIds,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var order = 1;
        foreach (var id in orderedIds)
        {
            var item = await _repo.GetByIdAsync(id, cancellationToken);
            if (item is null) continue;
            item.DisplayOrder = order++;
            item.UpdatedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(item, cancellationToken);
        }
    }

    private static DriverCompositionDto Map(DriverComposition e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        TargetAsset = e.TargetAsset,
        DriverAsset = e.DriverAsset,
        Weight = e.Weight,
        Enabled = e.Enabled,
        DisplayOrder = e.DisplayOrder,
        Description = e.Description,
        Category = e.Category,
        Inverse = e.Inverse
    };
}
