using Microsoft.EntityFrameworkCore;
using NtBot.Infrastructure.Cache;
using NtBot.Infrastructure.Persistence;
using NtBot.Macro.Cache;
using NtBot.Macro.Configuration;

namespace NtBot.Macro.Providers;

/// <summary>Mock provider for development/demo.</summary>
public sealed class MockMacroProvider : IMacroProvider
{
    private readonly IMacroCacheService _cache;
    private readonly IDbConfigurationCache _configCache;
    private readonly NtBotDbContext _db;

    public MockMacroProvider(IMacroCacheService cache, IDbConfigurationCache configCache, NtBotDbContext db)
    {
        _cache = cache;
        _configCache = configCache;
        _db = db;
    }

    public string Name => MacroProviderNames.Mock;
    public int Priority => 99;
    public IReadOnlyList<string> Capabilities { get; } = ["demo"];

    public async Task<MacroProviderRuntimeInfo> GetRuntimeInfoAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configCache.GetMacroProviderByNameAsync(Name, cancellationToken);
        return new MacroProviderRuntimeInfo
        {
            Name = Name,
            Enabled = config?.Enabled ?? false,
            Priority = config?.Priority ?? Priority,
            HealthStatus = config?.Enabled == true ? MacroProviderHealth.Healthy : MacroProviderHealth.Disabled,
            LastUpdate = config?.LastSync,
            Capabilities = Capabilities
        };
    }

    public async Task<MacroProviderPayload?> FetchAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configCache.GetMacroProviderByNameAsync(Name, cancellationToken);
        if (config is null || !config.Enabled) return null;

        return new MacroProviderPayload
        {
            ProviderName = Name,
            FetchedAt = DateTime.UtcNow,
            Indicators =
            [
                new MacroIndicatorValue { SeriesId = "MOCK_VIX", Label = "Mock VIX", Value = 14.2m },
                new MacroIndicatorValue { SeriesId = "MOCK_DGS10", Label = "Mock US10Y", Value = 4.25m }
            ]
        };
    }
}

