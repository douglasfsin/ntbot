using Microsoft.EntityFrameworkCore;
using NtBot.Infrastructure.Persistence;
using NtBot.Macro.Cache;
using NtBot.Macro.Configuration;

namespace NtBot.Macro.Providers;

/// <summary>Mock provider for development/demo.</summary>
public sealed class MockMacroProvider : IMacroProvider
{
    private readonly IMacroCacheService _cache;
    private readonly NtBotDbContext _db;

    public MockMacroProvider(IMacroCacheService cache, NtBotDbContext db)
    {
        _cache = cache;
        _db = db;
    }

    public string Name => MacroProviderNames.Mock;
    public int Priority => 99;
    public IReadOnlyList<string> Capabilities { get; } = ["demo"];

    public async Task<MacroProviderRuntimeInfo> GetRuntimeInfoAsync(CancellationToken cancellationToken = default)
    {
        var config = await _db.MacroProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Name == Name, cancellationToken);
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
        var config = await _db.MacroProviders.FirstOrDefaultAsync(p => p.Name == Name, cancellationToken);
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
