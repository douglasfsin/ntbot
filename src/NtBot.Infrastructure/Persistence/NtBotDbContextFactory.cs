using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace NtBot.Infrastructure.Persistence;

public class NtBotDbContextFactory : IDesignTimeDbContextFactory<NtBotDbContext>
{
    public NtBotDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var apiPath = ResolveApiProjectPath();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        var optionsBuilder = new DbContextOptionsBuilder<NtBotDbContext>();

        if (!string.IsNullOrEmpty(connectionString))
        {
            if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
                || connectionString.Contains("Username=", StringComparison.OrdinalIgnoreCase))
            {
                optionsBuilder.UseNpgsql(connectionString);
            }
            else
            {
                optionsBuilder.UseSqlServer(connectionString);
            }
        }
        else
        {
            optionsBuilder.UseSqlite("Data Source=ntbot.db");
        }

        return new NtBotDbContext(optionsBuilder.Options);
    }

    private static string ResolveApiProjectPath()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var apiSettings = Path.Combine(dir.FullName, "appsettings.json");
            if (File.Exists(apiSettings) && dir.Name.Equals("NtBot.Api", StringComparison.OrdinalIgnoreCase))
            {
                return dir.FullName;
            }

            var nestedApi = Path.Combine(dir.FullName, "NtBot.Api", "appsettings.json");
            if (File.Exists(nestedApi))
            {
                return Path.Combine(dir.FullName, "NtBot.Api");
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
