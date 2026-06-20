using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NtBot.Connector.Services;

namespace NtBot.Connector;

public static class DependencyInjection
{
    public static IServiceCollection AddConnector(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ConnectorSettings>(configuration.GetSection("Connector"));
        services.AddScoped<IConnectorService, ConnectorService>();
        services.AddScoped<IConnectorIngestService, ConnectorIngestService>();
        return services;
    }
}
