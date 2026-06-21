using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NtBot.Macro.Cache;
using NtBot.Macro.Configuration;
using NtBot.Macro.Engine;
using NtBot.Macro.Providers;
using NtBot.Macro.Providers.Calendar;
using NtBot.Macro.Providers.Fred;
using NtBot.Macro.Rules;
using NtBot.Macro.Services;

namespace NtBot.Macro;

public static class DependencyInjection
{
    public static IServiceCollection AddMacro(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MacroOptions>(configuration.GetSection(MacroOptions.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<IMacroCacheService, MacroCacheService>();

        services.AddHttpClient<FredApiClient>();
        services.AddHttpClient<IEconomicCalendarSyncService, FredEconomicCalendarSyncService>();
        services.AddHttpClient<BcbMacroProvider>();
        services.AddHttpClient<YahooFinanceMacroProvider>();

        services.AddScoped<IFredApiKeyResolver, FredApiKeyResolver>();

        services.AddScoped<IMacroProvider, FredMacroProvider>();
        services.AddScoped<IMacroProvider, Mt5CalendarMacroProvider>();
        services.AddScoped<IMacroProvider, BcbMacroProvider>();
        services.AddScoped<IMacroProvider, YahooFinanceMacroProvider>();
        services.AddScoped<IMacroProvider, MockMacroProvider>();

        services.AddScoped<IMacroEngine, MacroEngine>();
        services.AddScoped<IMacroRecommendationEngine, MacroRecommendationEngine>();
        services.AddScoped<IMacroIntelligenceService, MacroIntelligenceService>();
        services.AddHostedService<MacroRefreshWorker>();
        services.AddHostedService<EconomicCalendarSyncWorker>();

        return services;
    }
}
