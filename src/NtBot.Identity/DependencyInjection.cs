using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NtBot.Identity.Options;
using NtBot.Identity.Services;

namespace NtBot.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SmtpSettings>(configuration.GetSection("Smtp"));
        services.Configure<JwtSettings>(options =>
        {
            configuration.GetSection("Jwt").Bind(options);
            var envKey = configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_SECRET");
            if (!string.IsNullOrWhiteSpace(envKey))
                options.Key = envKey;
        });

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IOtpVerificationService, OtpVerificationService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
