namespace NtBot.Observability;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    public bool Enabled { get; set; } = true;

    /// <summary>SigNoz project partition — maps to <c>service.namespace</c>.</summary>
    public string Project { get; set; } = ObservabilityProjects.NtBot;

    public string ServiceName { get; set; } = "";

    public string ServiceVersion { get; set; } = "3.0.0";

    public string? OtlpEndpoint { get; set; }

    /// <summary><c>http/protobuf</c> (SigNoz Cloud default) or <c>grpc</c>.</summary>
    public string Protocol { get; set; } = "http/protobuf";

    /// <summary>Raw OTLP headers, e.g. <c>signoz-ingestion-key=...</c>.</summary>
    public string? Headers { get; set; }

    public string? IngestionKey { get; set; }

    public bool Traces { get; set; } = true;

    public bool Metrics { get; set; } = true;

    public bool Logs { get; set; } = true;

    public bool WriteToFile { get; set; } = true;

    public bool IsOtlpConfigured =>
        Enabled && !string.IsNullOrWhiteSpace(OtlpEndpoint);

    public bool IsGrpc =>
        Protocol.Contains("grpc", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> HeaderMap =>
        OtlpHeaderParser.ToDictionary(Headers);
}
