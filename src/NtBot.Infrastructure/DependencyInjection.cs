using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<NtBotDbContext>(options =>
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
                    || connectionString.Contains("Username=", StringComparison.OrdinalIgnoreCase))
                {
                    options.UseNpgsql(connectionString, npgsql =>
                        npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null));
                }
                else
                {
                    options.UseSqlServer(connectionString, sql =>
                        sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null));
                }
            }
            else
            {
                options.UseSqlite("Data Source=ntbot.db");
            }
        });

        return services;
    }
}
