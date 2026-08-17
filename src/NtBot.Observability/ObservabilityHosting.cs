using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace NtBot.Observability;

public static class ObservabilityHosting
{
    public static ILogger CreateBootstrapLogger() =>
        new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateBootstrapLogger();

    public static IHostBuilder UseNtBotSerilog(this IHostBuilder host, string defaultServiceName)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host.UseSerilog((context, services, configuration) =>
            ConfigureSerilog(configuration, context.Configuration, services, context.HostingEnvironment, defaultServiceName));
    }

    public static IHostApplicationBuilder UseNtBotSerilog(this IHostApplicationBuilder builder, string defaultServiceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSerilog((services, configuration) =>
            ConfigureSerilog(configuration, builder.Configuration, services, builder.Environment, defaultServiceName));
        return builder;
    }

    public static IServiceCollection AddNtBotOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string defaultServiceName,
        ObservabilityHostKind hostKind = ObservabilityHostKind.AspNetCore)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var options = OpenTelemetryOptionsResolver.Resolve(configuration, defaultServiceName);
        services.AddSingleton(options);

        if (!options.IsOtlpConfigured)
            return services;

        var attributes = ObservabilityResourceFactory.CreateAttributes(options, environment.EnvironmentName);

        var otel = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: options.ServiceName,
                    serviceNamespace: options.Project,
                    serviceVersion: options.ServiceVersion)
                .AddAttributes(attributes));

        if (options.Traces)
        {
            otel.WithTracing(tracing =>
            {
                if (hostKind == ObservabilityHostKind.AspNetCore)
                {
                    tracing.AddAspNetCoreInstrumentation(o =>
                    {
                        o.RecordException = true;
                        o.Filter = context => !context.Request.Path.StartsWithSegments("/api/health");
                    });
                }

                tracing.AddHttpClientInstrumentation();
                tracing.AddOtlpExporter(exporter => ConfigureExporter(exporter, options));
            });
        }

        if (options.Metrics)
        {
            otel.WithMetrics(metrics =>
            {
                if (hostKind == ObservabilityHostKind.AspNetCore)
                    metrics.AddAspNetCoreInstrumentation();

                metrics.AddHttpClientInstrumentation();
                metrics.AddRuntimeInstrumentation();
                metrics.AddOtlpExporter(exporter => ConfigureExporter(exporter, options));
            });
        }

        return services;
    }

    public static IApplicationBuilder UseNtBotObservability(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseMiddleware<ObservabilityLogEnrichmentMiddleware>();
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (http, _, ex) =>
                ex is not null || http.Response.StatusCode >= 500
                    ? LogEventLevel.Error
                    : http.Response.StatusCode >= 400
                        ? LogEventLevel.Warning
                        : LogEventLevel.Information;

            options.EnrichDiagnosticContext = (diagnostic, http) =>
            {
                diagnostic.Set("http.request.method", http.Request.Method);
                diagnostic.Set("url.path", http.Request.Path.Value ?? "");
                diagnostic.Set("http.response.status_code", http.Response.StatusCode);
                diagnostic.Set("http.route", http.GetEndpoint()?.DisplayName ?? http.Request.Path.Value ?? "");
            };
        });
        return app;
    }

    internal static void ConfigureSerilog(
        LoggerConfiguration logger,
        IConfiguration configuration,
        IServiceProvider services,
        IHostEnvironment environment,
        string defaultServiceName)
    {
        var options = OpenTelemetryOptionsResolver.Resolve(configuration, defaultServiceName);

        logger
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("project", options.Project)
            .Enrich.WithProperty("service.name", options.ServiceName)
            .WriteTo.Console();

        if (options.WriteToFile)
        {
            logger.WriteTo.File(
                "logs/ntbot-.txt",
                rollingInterval: RollingInterval.Day);
        }

        if (options is { IsOtlpConfigured: true, Logs: true })
        {
            logger.WriteTo.OpenTelemetry(otlp =>
            {
                otlp.Endpoint = options.OtlpEndpoint!;
                otlp.Protocol = options.IsGrpc
                    ? Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc
                    : Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf;
                otlp.Headers = options.HeaderMap.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
                otlp.ResourceAttributes = ObservabilityResourceFactory
                    .CreateAttributes(options, environment.EnvironmentName)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
                otlp.IncludedData =
                    Serilog.Sinks.OpenTelemetry.IncludedData.TraceIdField
                    | Serilog.Sinks.OpenTelemetry.IncludedData.SpanIdField
                    | Serilog.Sinks.OpenTelemetry.IncludedData.SpecRequiredResourceAttributes;
            });
        }
    }

    private static void ConfigureExporter(OtlpExporterOptions exporter, OpenTelemetryOptions options)
    {
        exporter.Endpoint = new Uri(options.OtlpEndpoint!);
        exporter.Protocol = options.IsGrpc
            ? OtlpExportProtocol.Grpc
            : OtlpExportProtocol.HttpProtobuf;
        if (!string.IsNullOrWhiteSpace(options.Headers))
            exporter.Headers = options.Headers;
    }
}

public enum ObservabilityHostKind
{
    AspNetCore,
    Worker
}
