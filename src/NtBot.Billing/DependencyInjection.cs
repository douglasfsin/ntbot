using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NtBot.Billing.Options;
using NtBot.Billing.Services;

namespace NtBot.Billing;

public static class DependencyInjection
{
    public static IServiceCollection AddBilling(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StripeSettings>(options =>
        {
            configuration.GetSection("Stripe").Bind(options);
            options.SecretKey = FirstNonEmpty(
                Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY"),
                configuration["Stripe:SecretKey"]) ?? options.SecretKey;
            options.PublishableKey = FirstNonEmpty(
                Environment.GetEnvironmentVariable("STRIPE_PUBLISHABLE_KEY"),
                configuration["Stripe:PublishableKey"]) ?? options.PublishableKey;
            options.WebhookSecret = FirstNonEmpty(
                Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET"),
                configuration["Stripe:WebhookSecret"]) ?? options.WebhookSecret;
            options.BackUrl = FirstNonEmpty(
                Environment.GetEnvironmentVariable("STRIPE_BACK_URL"),
                configuration["Stripe:BackUrl"]) ?? options.BackUrl;
        });

        services.Configure<SubscriptionSettings>(configuration.GetSection("Subscription"));

        services.AddScoped<IStripeGatewayService, StripeGatewayService>();
        services.AddScoped<IBillingService, BillingService>();

        return services;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
