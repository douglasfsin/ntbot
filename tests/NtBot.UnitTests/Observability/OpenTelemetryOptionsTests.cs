using Microsoft.Extensions.Configuration;
using NtBot.Observability;
using NtBot.Observability.Signoz;
using System.Text.Json.Nodes;

namespace NtBot.UnitTests.Observability;

public class OtlpEndpointNormalizerTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("https://ingest.us.signoz.cloud:443", "https://ingest.us.signoz.cloud:443")]
    [InlineData("https://ingest.us.signoz.cloud:443/", "https://ingest.us.signoz.cloud:443")]
    [InlineData("https://ingest.us.signoz.cloud:443/v1/logs", "https://ingest.us.signoz.cloud:443")]
    [InlineData("https://ingest.us.signoz.cloud:443/v1/traces", "https://ingest.us.signoz.cloud:443")]
    [InlineData("http://localhost:4318/v1/metrics", "http://localhost:4318")]
    public void Normalize_StripsSignalPaths(string? input, string? expected)
    {
        Assert.Equal(expected, OtlpEndpointNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("http://collector:4318", "logs", "http://collector:4318/v1/logs")]
    [InlineData("http://collector:4318/v1/logs", "traces", "http://collector:4318/v1/traces")]
    [InlineData("http://otelcollectorhttp.example.sslip.io/", "metrics", "http://otelcollectorhttp.example.sslip.io/v1/metrics")]
    public void ForHttpSignal_AppendsOtlpPath(string input, string signal, string expected)
    {
        Assert.Equal(expected, OtlpEndpointNormalizer.ForHttpSignal(input, signal));
    }
}

public class OtlpHeaderParserTests
{
    [Fact]
    public void Merge_AddsIngestionKeyWhenMissing()
    {
        var merged = OtlpHeaderParser.Merge("foo=bar", "abc123");
        Assert.Equal("foo=bar,signoz-ingestion-key=abc123", merged);
    }

    [Fact]
    public void Merge_DoesNotDuplicateIngestionKey()
    {
        var merged = OtlpHeaderParser.Merge("signoz-ingestion-key=existing", "other");
        Assert.Equal("signoz-ingestion-key=existing", merged);
    }

    [Fact]
    public void ToDictionary_ParsesCommaSeparatedPairs()
    {
        var map = OtlpHeaderParser.ToDictionary("signoz-ingestion-key=k1,authorization=Bearer x");
        Assert.Equal("k1", map["signoz-ingestion-key"]);
        Assert.Equal("Bearer x", map["authorization"]);
    }
}

public class OpenTelemetryOptionsResolverTests
{
    [Fact]
    public void Resolve_UsesDefaultsWhenUnset()
    {
        using var env = new EnvScope();
        env.ClearOtel();

        var options = OpenTelemetryOptionsResolver.Resolve(EmptyConfig(), "ntbot-api");

        Assert.Equal("ntbot-api", options.ServiceName);
        Assert.Equal(ObservabilityProjects.NtBot, options.Project);
        Assert.Equal("http/protobuf", options.Protocol);
        Assert.False(options.IsOtlpConfigured);
    }

    [Fact]
    public void Resolve_PrefersEnvironmentOverConfiguration()
    {
        using var env = new EnvScope();
        env.ClearOtel();
        env.Set("OTEL_SERVICE_NAME", "from-env");
        env.Set("OTEL_EXPORTER_OTLP_ENDPOINT", "https://ingest.us.signoz.cloud:443/v1/logs");
        env.Set("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc");
        env.Set("SIGNOZ_INGESTION_KEY", "ingest-key");
        env.Set("OTEL_RESOURCE_ATTRIBUTES", "service.namespace=Orbital,service.version=9.9.9");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:ServiceName"] = "from-config",
                ["OpenTelemetry:Project"] = "NtBot",
                ["OpenTelemetry:OtlpEndpoint"] = "http://localhost:4318"
            })
            .Build();

        var options = OpenTelemetryOptionsResolver.Resolve(config, "ntbot-api");

        Assert.Equal("from-env", options.ServiceName);
        Assert.Equal("Orbital", options.Project);
        Assert.Equal("9.9.9", options.ServiceVersion);
        Assert.Equal("https://ingest.us.signoz.cloud:443", options.OtlpEndpoint);
        Assert.Equal("grpc", options.Protocol);
        Assert.True(options.IsGrpc);
        Assert.Contains("signoz-ingestion-key=ingest-key", options.Headers);
        Assert.True(options.IsOtlpConfigured);
    }

    [Fact]
    public void Resolve_DisablesSdkWhenOtelSdkDisabled()
    {
        using var env = new EnvScope();
        env.ClearOtel();
        env.Set("OTEL_SDK_DISABLED", "true");
        env.Set("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4318");

        var options = OpenTelemetryOptionsResolver.Resolve(EmptyConfig(), "ntbot-api");
        Assert.False(options.Enabled);
        Assert.False(options.IsOtlpConfigured);
    }

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection().Build();

    private sealed class EnvScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = new();
        private static readonly string[] Keys =
        [
            "OTEL_SERVICE_NAME",
            "OTEL_EXPORTER_OTLP_ENDPOINT",
            "OTEL_EXPORTER_OTLP_PROTOCOL",
            "OTEL_EXPORTER_OTLP_HEADERS",
            "OTEL_RESOURCE_ATTRIBUTES",
            "OTEL_SDK_DISABLED",
            "OTEL_PROJECT",
            "SIGNOZ_OTLP_ENDPOINT",
            "SIGNOZ_INGESTION_KEY"
        ];

        public void ClearOtel()
        {
            foreach (var key in Keys)
                Set(key, null);
        }

        public void Set(string key, string? value)
        {
            if (!_previous.ContainsKey(key))
                _previous[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }

        public void Dispose()
        {
            foreach (var (key, value) in _previous)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}

public class ObservabilityResourceFactoryTests
{
    [Fact]
    public void CreateAttributes_IncludesProjectNamespaceAndEnvironment()
    {
        var options = new OpenTelemetryOptions
        {
            ServiceName = "ntbot-api",
            Project = ObservabilityProjects.NtBot,
            ServiceVersion = "3.0.0"
        };

        var attrs = ObservabilityResourceFactory.CreateAttributes(options, "Production");

        Assert.Equal("ntbot-api", attrs["service.name"]);
        Assert.Equal("NtBot", attrs["service.namespace"]);
        Assert.Equal("NtBot", attrs["project"]);
        Assert.Equal("Production", attrs["deployment.environment"]);
        Assert.Equal("3.0.0", attrs["service.version"]);
    }
}

public class SignozObservabilityCatalogTests
{
    [Fact]
    public void CreatesDashboardAndViewsForEachProject()
    {
        Assert.Equal(3, SignozObservabilityCatalog.CreateDashboards().Count);
        Assert.Equal(9, SignozObservabilityCatalog.CreateViews().Count);
        Assert.Equal(new[] { "NtBot", "Orbital", "Montescar" }, ObservabilityProjects.All);
    }

    [Theory]
    [InlineData("NtBot")]
    [InlineData("Orbital")]
    [InlineData("Montescar")]
    public void Dashboard_FiltersByServiceNamespace(string project)
    {
        var dashboard = SignozObservabilityCatalog.CreateLogsDashboard(project);

        Assert.Equal("v6", dashboard["schemaVersion"]?.GetValue<string>());
        Assert.Equal($"{project} — Logs", dashboard["spec"]?["display"]?["name"]?.GetValue<string>());
        Assert.True(ContainsString(dashboard, $"service.namespace = '{project}'"));
        Assert.False(ContainsString(dashboard, "service.namespace = 'Other'"));

        var panels = dashboard["spec"]?["panels"]?.AsObject();
        var layouts = dashboard["spec"]?["layouts"]?.AsArray();
        Assert.NotNull(panels);
        Assert.Equal(6, panels.Count);
        Assert.Equal(2, layouts!.Count);

        var refs = layouts
            .SelectMany(grid => grid!["spec"]!["items"]!.AsArray())
            .Select(item => item!["content"]!["$ref"]!.GetValue<string>())
            .ToHashSet();
        Assert.Equal(panels.Count, refs.Count);
        foreach (var id in panels.Select(p => p.Key))
            Assert.Contains($"#/spec/panels/{id}", refs);
    }

    [Fact]
    public void Views_AreLogsExplorerQueries()
    {
        var views = SignozObservabilityCatalog.CreateProjectViews("NtBot");
        Assert.Equal(3, views.Count);
        Assert.All(views, view =>
        {
            Assert.Equal("logs", view["sourcePage"]?.GetValue<string>());
            Assert.Equal("builder", view["compositeQuery"]?["queryType"]?.GetValue<string>());
            Assert.Equal("list", view["compositeQuery"]?["panelType"]?.GetValue<string>());
            Assert.True(ContainsString(view, "service.namespace = 'NtBot'"));
        });
    }

    [Fact]
    public void UnknownProject_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SignozObservabilityCatalog.CreateLogsDashboard("Unknown"));
    }

    private static bool ContainsString(JsonNode? node, string expected)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                return text.Contains(expected, StringComparison.Ordinal);
            case JsonObject obj:
                return obj.Select(p => p.Value).Any(child => ContainsString(child, expected));
            case JsonArray array:
                return array.Any(child => ContainsString(child, expected));
            default:
                return false;
        }
    }
}
