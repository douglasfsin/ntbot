using Marketdata.Database.Client;
using Marketdata.Database.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketdata.Database;

public static class DependencyInjection
{
    public static IServiceCollection AddMarketDataPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<QuestDbOptions>(configuration.GetSection(QuestDbOptions.SectionName));
        services.AddSingleton<IQuestDbClient, QuestDbPublisher>();
        
        // Registro do DataLoader Histórico
        services.AddSingleton<Marketdata.Database.Services.HistoricalDataLoader>();
        
        return services;
    }
}
