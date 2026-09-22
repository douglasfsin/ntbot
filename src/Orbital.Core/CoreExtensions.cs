using System;
using Microsoft.Extensions.DependencyInjection;
using Orbital.Core.Services;

namespace Orbital.Core;

public static class CoreExtensions
{
    public static IServiceCollection AddOrbitalCore(this IServiceCollection services, string questDbConnectionString)
    {
        // Registro em Singleton pois precisa reter o estado de _activeZones entre as threads
        services.AddSingleton<ZoneInterestManager>();

        services.AddSingleton<SMCQuestDbClient>(provider => 
        {
            var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SMCQuestDbClient>>();
            return new SMCQuestDbClient(questDbConnectionString, logger);
        });

        // Tape Speed Registrations
        services.AddSingleton<TapeSpeedAnalyzer>();
        services.AddSingleton<TapeSpeedDbClient>(provider => 
        {
            var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TapeSpeedDbClient>>();
            return new TapeSpeedDbClient(questDbConnectionString, logger);
        });

        // Registrar o TapeSpeedEngine como Singleton e HostedService
        services.AddSingleton<TapeSpeedEngine>();
        services.AddHostedService<TapeSpeedEngine>(provider => provider.GetRequiredService<TapeSpeedEngine>());

        // Registrar o SMCEngine como BackgroundService
        services.AddHostedService<SMCEngine>();

        return services;
    }
}
