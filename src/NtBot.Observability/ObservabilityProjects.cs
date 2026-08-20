namespace NtBot.Observability;

/// <summary>
/// Projects that share the SigNoz workspace. Telemetry is partitioned by
/// the OpenTelemetry <c>service.namespace</c> resource attribute.
/// </summary>
public static class ObservabilityProjects
{
    public const string NtBot = "NtBot";
    public const string Orbital = "Orbital";
    public const string Montescar = "Montescar";

    public static readonly IReadOnlyList<string> All = [NtBot, Orbital, Montescar];

    public static bool IsKnown(string? project) =>
        !string.IsNullOrWhiteSpace(project)
        && All.Any(p => string.Equals(p, project, StringComparison.OrdinalIgnoreCase));
}
