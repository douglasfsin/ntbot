using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NtBot.Infrastructure.Persistence;
using NtBot.Macro.Configuration;

namespace NtBot.Macro.Providers.Fred;

public interface IFredApiKeyResolver
{
    Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default);
}

public sealed class FredApiKeyResolver : IFredApiKeyResolver
{
    private readonly NtBotDbContext _db;
    private readonly MacroOptions _options;

    public FredApiKeyResolver(NtBotDbContext db, IOptions<MacroOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var providerKey = await _db.MacroProviders
            .AsNoTracking()
            .Where(p => p.Name == MacroProviderNames.Fred)
            .Select(p => p.ApiKey)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(providerKey))
            return providerKey.Trim();

        return string.IsNullOrWhiteSpace(_options.FredApiKey) ? null : _options.FredApiKey.Trim();
    }
}
