using Microsoft.Extensions.Configuration;
using NtBot.Mentor.Configuration;
using NtBot.Mentor.Engine;
using NtBot.Mentor.Persistence;
using NtBot.Mentor.Services;

namespace NtBot.Mentor;

public static class DependencyInjection
{
    public static IServiceCollection AddMentor(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MentorOptions>(configuration.GetSection(MentorOptions.SectionName));

        services.AddScoped<ITradeHistoryRepository, TradeHistoryRepository>();
        services.AddScoped<ITradeAnalyticsEngine, TradeAnalyticsEngine>();
        services.AddScoped<IPersonalRecommendationEngine, PersonalRecommendationEngine>();
        services.AddScoped<IPerformanceScoreEngine, PerformanceScoreEngine>();
        services.AddScoped<IMentorService, MentorService>();

        return services;
    }
}
