using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Cache;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Macro.Providers;

/// <summary>
/// Atualiza LastSync/Status sem exigir entidade tracked — evita FirstOrDefault
/// no caminho quente e SaveChanges após fetch HTTP.
/// </summary>
internal static class MacroProviderSync
{
    public static async Task MarkAsync(
        NtBotDbContext db,
        IDbConfigurationCache cache,
        MacroProvider config,
        string status,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        config.LastSync = now;
        config.Status = status;
        config.UpdatedAt = now;
        cache.UpdateMacroProvider(config);

        try
        {
            await db.MacroProviders
                .Where(p => p.Id == config.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.LastSync, now)
                    .SetProperty(p => p.Status, status)
                    .SetProperty(p => p.UpdatedAt, now), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Persistência best-effort — o payload já foi cacheado.
        }
    }
}
