using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NtBot.MarketData.Clients;
using NtBot.MarketData.Configuration;
using NtBot.MarketData.Services;

namespace NtBot.MarketData;

public static class DependencyInjection
{
    public static IServiceCollection AddNtBotMarketData(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MarketDataApiOptions>(configuration.GetSection(MarketDataApiOptions.SectionName));

        services.AddHttpClient<IMarketDataApiClient, MarketDataApiClient>(client =>
        {
            // Enrichment B3 não pode segurar o overview por 2 minutos.
            client.Timeout = TimeSpan.FromSeconds(8);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        services.AddSingleton<IB3EquitySnapshotProvider, B3EquitySnapshotProvider>();

        return services;
    }
}
