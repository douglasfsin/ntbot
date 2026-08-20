using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NtBot.Analytics.Configuration;
using NtBot.Analytics.Engines;
using NtBot.Analytics.Services;
using NtBot.Analytics.Workers;

namespace NtBot.Analytics;

public static class DependencyInjection
{
    public static IServiceCollection AddQuantAnalytics(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<QuantStatisticsOptions>(configuration.GetSection(QuantStatisticsOptions.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<IFeatureEngine, FeatureEngine>();
        services.AddSingleton<IOutcomeEngine, OutcomeEngine>();
        services.AddSingleton<IStatisticalEngine, StatisticalEngine>();
        services.AddSingleton<IBacktestEngine, BacktestEngineStub>();
        services.AddSingleton<IQuantTickAggregator, QuantTickBarAggregator>();
        services.AddScoped<IQuantRepository, QuantRepository>();
        services.AddScoped<IQuantSignalRecorder, QuantSignalRecorder>();
        services.AddScoped<IQuantFeaturePipeline, QuantFeaturePipeline>();
        services.AddScoped<IQuantOutcomeProcessor, QuantOutcomeProcessor>();
        services.AddScoped<IQuantAggregationService, QuantAggregationService>();
        services.AddScoped<IQuantQueryService, QuantQueryService>();
        services.AddHostedService<QuantFeatureWorker>();
        services.AddHostedService<QuantOutcomeWorker>();
        services.AddHostedService<QuantAggregationWorker>();
        return services;
    }
}
