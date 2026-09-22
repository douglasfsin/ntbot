using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Configuration;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Infrastructure.Cache;

public sealed class DbConfigurationCache : IDbConfigurationCache
{
    private const string MacroProvidersKey = "config:macro-providers";
    private const string MarketProvidersKey = "config:market-providers";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _ttl;
    private readonly ILogger<DbConfigurationCache> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public DbConfigurationCache(
        IServiceScopeFactory scopeFactory,
        IOptions<DbConfigurationCacheOptions> options,
        ILogger<DbConfigurationCache> logger)
    {
        _scopeFactory = scopeFactory;
        _ttl = options.Value.Ttl;
        _logger = logger;
    }

    public Task<IReadOnlyList<MacroProvider>> GetMacroProvidersAsync(CancellationToken cancellationToken = default) =>
        GetOrLoadAsync(
            MacroProvidersKey,
            async db =>
            {
                var rows = await db.MacroProviders.AsNoTracking()
                    .OrderBy(p => p.Priority)
                    .ToListAsync(cancellationToken);
                return (IReadOnlyList<MacroProvider>)rows.Select(Clone).ToList();
            },
            cancellationToken);

    public async Task<MacroProvider?> GetMacroProviderByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var providers = await GetMacroProvidersAsync(cancellationToken);
        return providers.FirstOrDefault(p => p.Name == name);
    }

    public void UpdateMacroProvider(MacroProvider provider)
    {
        if (!_entries.TryGetValue(MacroProvidersKey, out var entry))
            return;

        if (entry.Value is not List<MacroProvider> list)
            return;

        var clone = Clone(provider);
        var idx = list.FindIndex(p => p.Id == clone.Id);
        if (idx >= 0)
            list[idx] = clone;
        else
            list.Add(clone);

        entry.ExpiresAt = DateTime.UtcNow.Add(_ttl);
        _logger.LogDebug("Macro provider {Name} atualizado no cache de configuração", provider.Name);
    }

    public void InvalidateMacroProviders() => Remove(MacroProvidersKey);

    public Task<IReadOnlyList<MarketIntelligenceProvider>> GetMarketIntelligenceProvidersAsync(
        CancellationToken cancellationToken = default) =>
        GetOrLoadAsync(
            MarketProvidersKey,
            async db =>
            {
                var rows = await db.MarketIntelligenceProviders.AsNoTracking()
                    .OrderBy(p => p.Name)
                    .ToListAsync(cancellationToken);
                return (IReadOnlyList<MarketIntelligenceProvider>)rows.Select(Clone).ToList();
            },
            cancellationToken);

    public async Task<MarketIntelligenceProvider?> GetMarketIntelligenceProviderByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var providers = await GetMarketIntelligenceProvidersAsync(cancellationToken);
        return providers.FirstOrDefault(p => p.Name == name);
    }

    public void UpdateMarketIntelligenceProvider(MarketIntelligenceProvider provider)
    {
        if (!_entries.TryGetValue(MarketProvidersKey, out var entry))
            return;

        if (entry.Value is not List<MarketIntelligenceProvider> list)
            return;

        var clone = Clone(provider);
        var idx = list.FindIndex(p => p.Id == clone.Id);
        if (idx >= 0)
            list[idx] = clone;
        else
            list.Add(clone);

        entry.ExpiresAt = DateTime.UtcNow.Add(_ttl);
        _logger.LogDebug("Market provider {Name} atualizado no cache de configuração", provider.Name);
    }

    public void InvalidateMarketIntelligenceProviders() => Remove(MarketProvidersKey);

    public Task<IReadOnlyList<DriverComposition>> GetDriverCompositionsAsync(
        string targetAsset,
        Guid? tenantId,
        bool enabledOnly,
        CancellationToken cancellationToken = default)
    {
        var key = DriverKey(targetAsset, tenantId, enabledOnly);
        return GetOrLoadAsync(
            key,
            async db =>
            {
                var query = db.DriverCompositions.AsNoTracking()
                    .Where(d => d.TargetAsset == targetAsset);

                if (enabledOnly)
                    query = query.Where(d => d.Enabled);

                query = tenantId.HasValue
                    ? query.Where(d => d.TenantId == tenantId || d.TenantId == null)
                    : query.Where(d => d.TenantId == null);

                var rows = await query.OrderBy(d => d.DisplayOrder).ToListAsync(cancellationToken);
                return (IReadOnlyList<DriverComposition>)rows.Select(Clone).ToList();
            },
            cancellationToken);
    }

    public void InvalidateDriverCompositions(string? targetAsset = null)
    {
        if (string.IsNullOrWhiteSpace(targetAsset))
        {
            RemoveByPrefix("config:drivers:");
            return;
        }

        RemoveByPrefix($"config:drivers:{targetAsset}:");
    }

    public Task<IReadOnlyList<AssetConfiguration>> GetAssetConfigurationsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var key = $"config:assets:{tenantId:N}";
        return GetOrLoadAsync(
            key,
            async db =>
            {
                var rows = await db.AssetConfigurations.AsNoTracking()
                    .Where(a => a.TenantId == tenantId && a.IsActive)
                    .ToListAsync(cancellationToken);
                return (IReadOnlyList<AssetConfiguration>)rows.Select(Clone).ToList();
            },
            cancellationToken);
    }

    public void InvalidateAssetConfigurations(Guid? tenantId = null)
    {
        if (tenantId is null)
            RemoveByPrefix("config:assets:");
        else
            Remove($"config:assets:{tenantId.Value:N}");
    }

    private async Task<T> GetOrLoadAsync<T>(
        string key,
        Func<NtBotDbContext, Task<T>> loader,
        CancellationToken cancellationToken)
    {
        if (TryGet(key, out T? cached) && cached is not null)
            return cached;

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (TryGet(key, out cached) && cached is not null)
                return cached;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NtBotDbContext>();
            var value = await loader(db);
            _entries[key] = new CacheEntry(value, DateTime.UtcNow.Add(_ttl));
            _logger.LogDebug("Configuração {Key} carregada do banco (TTL {TtlHours}h)", key, _ttl.TotalHours);
            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool TryGet<T>(string key, out T? value)
    {
        value = default;
        if (!_entries.TryGetValue(key, out var entry))
            return false;

        if (entry.ExpiresAt <= DateTime.UtcNow)
        {
            _entries.TryRemove(key, out _);
            return false;
        }

        if (entry.Value is T typed)
        {
            value = typed;
            return true;
        }

        return false;
    }

    private void Remove(string key) => _entries.TryRemove(key, out _);

    private void RemoveByPrefix(string prefix)
    {
        foreach (var key in _entries.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
            _entries.TryRemove(key, out _);
    }

    private static string DriverKey(string targetAsset, Guid? tenantId, bool enabledOnly) =>
        $"config:drivers:{targetAsset}:{tenantId?.ToString("N") ?? "global"}:{(enabledOnly ? "enabled" : "all")}";

    private static MacroProvider Clone(MacroProvider p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Enabled = p.Enabled,
        Priority = p.Priority,
        ApiUrl = p.ApiUrl,
        ApiKey = p.ApiKey,
        RefreshIntervalMinutes = p.RefreshIntervalMinutes,
        Status = p.Status,
        LastSync = p.LastSync,
        Capabilities = p.Capabilities,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };

    private static MarketIntelligenceProvider Clone(MarketIntelligenceProvider p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Enabled = p.Enabled,
        RefreshIntervalSeconds = p.RefreshIntervalSeconds,
        Status = p.Status,
        LastSync = p.LastSync,
        Capabilities = p.Capabilities,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };

    private static DriverComposition Clone(DriverComposition d) => new()
    {
        Id = d.Id,
        TenantId = d.TenantId,
        TargetAsset = d.TargetAsset,
        DriverAsset = d.DriverAsset,
        Weight = d.Weight,
        Enabled = d.Enabled,
        DisplayOrder = d.DisplayOrder,
        Description = d.Description,
        Category = d.Category,
        Inverse = d.Inverse,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt
    };

    private static AssetConfiguration Clone(AssetConfiguration a) => new()
    {
        Id = a.Id,
        TenantId = a.TenantId,
        Symbol = a.Symbol,
        IsActive = a.IsActive,
        MaxPositionSize = a.MaxPositionSize,
        RiskPerTrade = a.RiskPerTrade,
        MaxDailyLoss = a.MaxDailyLoss,
        Timeframes = a.Timeframes,
        MinConfidenceScore = a.MinConfidenceScore,
        MinRiskReward = a.MinRiskReward,
        EnableWyckoff = a.EnableWyckoff,
        EnableMacroFilter = a.EnableMacroFilter,
        EnableNewsFilter = a.EnableNewsFilter,
        EnableEconomicCalendar = a.EnableEconomicCalendar,
        TradingStartTime = a.TradingStartTime,
        TradingEndTime = a.TradingEndTime,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt
    };

    private sealed class CacheEntry(object value, DateTime expiresAt)
    {
        public object Value { get; } = value;
        public DateTime ExpiresAt { get; set; } = expiresAt;
    }
}
