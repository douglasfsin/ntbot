using Microsoft.AspNetCore.HttpOverrides;

namespace NtBot.Api.Hosting;

/// <summary>
/// Padrão BarberAI: <c>PORT</c> (Coolify) + forwarded headers para Traefik/Cloudflare.
/// </summary>
public static class CoolifyHostingExtensions
{
    public static WebApplicationBuilder AddCoolifyHosting(this WebApplicationBuilder builder)
    {
        var portEnv = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrWhiteSpace(portEnv) && int.TryParse(portEnv, out var port) && port > 0)
        {
            builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(port));
        }

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                       | ForwardedHeaders.XForwardedProto
                                       | ForwardedHeaders.XForwardedHost;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return builder;
    }

    public static IApplicationBuilder UseCoolifyForwardedHeaders(this IApplicationBuilder app)
    {
        app.UseForwardedHeaders();
        return app;
    }

    public static bool IsBehindCoolifyProxy()
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PORT"))
           || string.Equals(
               Environment.GetEnvironmentVariable("ASPNETCORE_FORWARDEDHEADERS_ENABLED"),
               "true",
               StringComparison.OrdinalIgnoreCase);
}
