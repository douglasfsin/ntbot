using Microsoft.Extensions.Options;
using NtBot.Infrastructure.Cache;
using NtBot.Macro.Configuration;

namespace NtBot.Macro.Providers.Fred;

public interface IFredApiKeyResolver
{
    Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default);
}

public sealed class FredApiKeyResolver : IFredApiKeyResolver
{
    private readonly IDbConfigurationCache _configCache;
    private readonly MacroOptions _options;

    public FredApiKeyResolver(IDbConfigurationCache configCache, IOptions<MacroOptions> options)
    {
        _configCache = configCache;
        _options = options.Value;
    }

    public async Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var provider = await _configCache.GetMacroProviderByNameAsync(MacroProviderNames.Fred, cancellationToken);
        var providerKey = provider?.ApiKey;

        if (!string.IsNullOrWhiteSpace(providerKey))
            return providerKey.Trim();

        return string.IsNullOrWhiteSpace(_options.FredApiKey) ? null : _options.FredApiKey.Trim();
    }
}
