using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NtBot.MarketDrivers.Engine;
using NtBot.MarketDrivers.Providers;
using NtBot.MarketDrivers.Rules;
using NtBot.MarketDrivers.Services;

namespace NtBot.MarketDrivers;

public static class DependencyInjection
{
    public static IServiceCollection AddMarketDrivers(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Configuration.MarketDriversOptions>(
            configuration.GetSection(Configuration.MarketDriversOptions.SectionName));

        services.AddScoped<IMarketDriverRule, AssetDriverRule>();
        services.AddScoped<IMarketDriverRule, CalendarDriverRule>();
        services.AddScoped<IMarketDriverProvider, CompositeMarketDriverProvider>();
        services.AddScoped<MarketDriverContextBuilder>();

        services.AddScoped<DriverScoreEngine>();
        services.AddScoped<DriverExplanationEngine>();
        services.AddScoped<MarketDriversHeatMapEngine>();
        services.AddScoped<MarketDriversAIService>();
        services.AddScoped<IMarketDriverEngine, MarketDriverEngine>();
        services.AddScoped<IMarketDriversService, MarketDriversService>();
        services.AddHostedService<MarketDriversRefreshWorker>();

        return services;
    }
}
