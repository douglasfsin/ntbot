namespace NtBot.Observability;

public static class ObservabilityResourceFactory
{
    public static IReadOnlyDictionary<string, object> CreateAttributes(
        OpenTelemetryOptions options,
        string environmentName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        return new Dictionary<string, object>
        {
            ["service.name"] = options.ServiceName,
            ["service.namespace"] = options.Project,
            ["service.version"] = options.ServiceVersion,
            ["deployment.environment"] = environmentName,
            ["project"] = options.Project
        };
    }
}
