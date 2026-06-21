using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NtBot.MarketIntelligence.Cache;
using NtBot.MarketIntelligence.Configuration;
using NtBot.MarketIntelligence.Engine;
using NtBot.MarketIntelligence.Providers;
using NtBot.MarketIntelligence.Providers.Yahoo;
using NtBot.MarketIntelligence.Services;

namespace NtBot.MarketIntelligence;

public static class DependencyInjection
{
    public static IServiceCollection AddMarketIntelligence(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MarketIntelligenceOptions>(configuration.GetSection(MarketIntelligenceOptions.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<IMarketIntelligenceCacheService, MarketIntelligenceCacheService>();

        services.AddHttpClient<YahooFinanceClient>(client => client.Timeout = TimeSpan.FromSeconds(30));

        services.AddScoped<IMarketDataProvider, YahooFinanceProvider>();
        services.AddScoped<IMarketIntelligenceEngine, MarketIntelligenceEngine>();
        services.AddScoped<CorrelationEngine>();
        services.AddScoped<QuantScoreEngine>();
        services.AddScoped<IMarketIntelligenceService, MarketIntelligenceService>();
        services.AddHostedService<MarketRefreshWorker>();

        return services;
    }
}
