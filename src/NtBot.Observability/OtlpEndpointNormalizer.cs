namespace NtBot.Observability;

public static class OtlpEndpointNormalizer
{
    private static readonly string[] SignalSuffixes =
    [
        "/v1/logs",
        "/v1/traces",
        "/v1/metrics"
    ];

    /// <summary>
    /// Returns a base OTLP endpoint (scheme + host + port). Signal-specific
    /// paths are stripped so exporters can append <c>/v1/{signal}</c>.
    /// </summary>
    public static string? Normalize(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return null;

        var value = endpoint.Trim();
        foreach (var suffix in SignalSuffixes)
        {
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[..^suffix.Length];
                break;
            }
        }

        return value.TrimEnd('/');
    }

    public static string ForHttpSignal(string endpoint, string signal)
    {
        var normalized = Normalize(endpoint)
            ?? throw new ArgumentException("OTLP endpoint is required.", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(signal))
            throw new ArgumentException("Signal is required.", nameof(signal));
        return $"{normalized}/v1/{signal.Trim().Trim('/')}";
    }
}
