using Microsoft.Extensions.Configuration;

namespace NtBot.Observability;

public static class OpenTelemetryOptionsResolver
{
    public static OpenTelemetryOptions Resolve(IConfiguration configuration, string defaultServiceName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultServiceName);

        var options = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
            ?? new OpenTelemetryOptions();

        var resourceAttributes = ParseResourceAttributes(
            Environment.GetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES"));

        options.ServiceName = FirstNonEmpty(
            Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME"),
            options.ServiceName,
            defaultServiceName)!;

        options.Project = FirstNonEmpty(
            Get(resourceAttributes, "service.namespace"),
            Get(resourceAttributes, "project"),
            Environment.GetEnvironmentVariable("OTEL_PROJECT"),
            options.Project,
            ObservabilityProjects.NtBot)!;

        options.ServiceVersion = FirstNonEmpty(
            Get(resourceAttributes, "service.version"),
            options.ServiceVersion,
            "3.0.0")!;

        options.OtlpEndpoint = OtlpEndpointNormalizer.Normalize(FirstNonEmpty(
            Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"),
            Environment.GetEnvironmentVariable("SIGNOZ_OTLP_ENDPOINT"),
            options.OtlpEndpoint));

        options.Protocol = FirstNonEmpty(
            Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL"),
            options.Protocol,
            "http/protobuf")!;

        options.Headers = OtlpHeaderParser.Merge(
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS"),
                options.Headers),
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("SIGNOZ_INGESTION_KEY"),
                options.IngestionKey));

        if (string.Equals(Environment.GetEnvironmentVariable("OTEL_SDK_DISABLED"), "true", StringComparison.OrdinalIgnoreCase))
            options.Enabled = false;

        return options;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static Dictionary<string, string> ParseResourceAttributes(string? raw)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
            return map;

        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            map[part[..eq].Trim()] = part[(eq + 1)..].Trim();
        }

        return map;
    }

    private static string? Get(IReadOnlyDictionary<string, string> map, string key) =>
        map.TryGetValue(key, out var value) ? value : null;
}
