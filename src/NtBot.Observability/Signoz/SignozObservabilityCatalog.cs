using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace NtBot.Observability.Signoz;

public static class SignozObservabilityCatalog
{
    public static IReadOnlyList<JsonObject> CreateDashboards() =>
        ObservabilityProjects.All.Select(CreateLogsDashboard).ToList();

    public static IReadOnlyList<JsonObject> CreateViews() =>
        ObservabilityProjects.All.SelectMany(CreateProjectViews).ToList();

    public static JsonObject CreateLogsDashboard(string project)
    {
        EnsureProject(project);

        var volumeId = PanelId(project, "log-volume");
        var errorId = PanelId(project, "error-volume");
        var warnId = PanelId(project, "warn-volume");
        var errorCountId = PanelId(project, "error-count");
        var bySeverityId = PanelId(project, "by-severity");
        var byServiceId = PanelId(project, "by-service");

        var nsFilter = $"service.namespace = '{project}'";
        var errorFilter = $"{nsFilter} AND severity_text IN ['ERROR', 'Fatal', 'FATAL']";
        var warnFilter = $"{nsFilter} AND severity_text IN ['WARN', 'Warning', 'WARNING']";

        return new JsonObject
        {
            ["schemaVersion"] = "v6",
            ["image"] = "/assets/Icons/bar-chart",
            ["tags"] = new JsonArray
            {
                Tag("project", project),
                Tag("signal", "logs"),
                Tag("managed-by", "ntbot")
            },
            ["spec"] = new JsonObject
            {
                ["display"] = new JsonObject
                {
                    ["name"] = $"{project} — Logs",
                    ["description"] = $"Volume, erros e breakdown de logs OpenTelemetry do projeto {project}."
                },
                ["variables"] = new JsonArray
                {
                    DynamicVariable("service_name", "service.name", "Service"),
                    DynamicVariable("deployment_environment", "deployment.environment", "Environment")
                },
                ["panels"] = new JsonObject
                {
                    [volumeId] = TimeSeriesPanel("Log volume", nsFilter, "count()", groupBy: null),
                    [errorId] = TimeSeriesPanel("Error logs", errorFilter, "count()", groupBy: null),
                    [warnId] = TimeSeriesPanel("Warning logs", warnFilter, "count()", groupBy: null),
                    [errorCountId] = ValuePanel("Error count", errorFilter, "count()"),
                    [bySeverityId] = TimeSeriesPanel("Logs by severity", nsFilter, "count()", groupBy: "severity_text"),
                    [byServiceId] = TimeSeriesPanel("Logs by service", nsFilter, "count()", groupBy: "service.name")
                },
                ["layouts"] = new JsonArray
                {
                    Grid("Overview",
                    [
                        Item(0, 0, 3, 4, errorCountId),
                        Item(3, 0, 9, 4, volumeId),
                        Item(0, 4, 6, 6, errorId),
                        Item(6, 4, 6, 6, warnId)
                    ]),
                    Grid("Breakdown",
                    [
                        Item(0, 0, 6, 6, bySeverityId),
                        Item(6, 0, 6, 6, byServiceId)
                    ])
                }
            }
        };
    }

    public static IReadOnlyList<JsonObject> CreateProjectViews(string project)
    {
        EnsureProject(project);
        var nsFilter = $"service.namespace = '{project}'";

        return
        [
            LogsView($"{project} — All logs", project, nsFilter, "#3b82f6"),
            LogsView(
                $"{project} — Errors",
                project,
                $"{nsFilter} AND severity_text IN ['ERROR', 'Fatal', 'FATAL']",
                "#e5484d"),
            LogsView(
                $"{project} — Warnings",
                project,
                $"{nsFilter} AND severity_text IN ['WARN', 'Warning', 'WARNING']",
                "#f5a524")
        ];
    }

    public static string ProjectFilter(string project)
    {
        EnsureProject(project);
        return $"service.namespace = '{project}'";
    }

    private static JsonObject LogsView(string name, string project, string filter, string color) =>
        new()
        {
            ["name"] = name,
            ["category"] = project,
            ["sourcePage"] = "logs",
            ["tags"] = new JsonArray(project.ToLowerInvariant(), "logs", "ntbot-managed"),
            ["extraData"] = $$"""{"color":"{{color}}","version":1,"format":"table","maxLines":1,"fontSize":"small"}""",
            ["compositeQuery"] = new JsonObject
            {
                ["queryType"] = "builder",
                ["panelType"] = "list",
                ["queries"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "builder_query",
                        ["spec"] = new JsonObject
                        {
                            ["name"] = "A",
                            ["signal"] = "logs",
                            ["stepInterval"] = 0,
                            ["disabled"] = false,
                            ["limit"] = 100,
                            ["filter"] = new JsonObject { ["expression"] = filter },
                            ["having"] = new JsonObject { ["expression"] = "" },
                            ["order"] = new JsonArray
                            {
                                Order("timestamp", "desc"),
                                Order("id", "desc")
                            }
                        }
                    }
                }
            }
        };

    private static JsonObject TimeSeriesPanel(string title, string filter, string aggregation, string? groupBy)
    {
        var spec = BaseLogQuerySpec(filter, aggregation, "time_series");
        if (!string.IsNullOrWhiteSpace(groupBy))
            spec["groupBy"] = new JsonArray(new JsonObject { ["name"] = groupBy });

        return new JsonObject
        {
            ["kind"] = "Panel",
            ["spec"] = new JsonObject
            {
                ["display"] = new JsonObject { ["name"] = title },
                ["plugin"] = new JsonObject
                {
                    ["kind"] = "signoz/TimeSeriesPanel",
                    ["spec"] = new JsonObject
                    {
                        ["visualization"] = new JsonObject { ["timePreference"] = "global_time" },
                        ["legend"] = new JsonObject { ["position"] = "bottom" },
                        ["chartAppearance"] = new JsonObject
                        {
                            ["lineStyle"] = "solid",
                            ["lineInterpolation"] = "spline",
                            ["fillMode"] = "none"
                        }
                    }
                },
                ["queries"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["kind"] = "time_series",
                        ["spec"] = new JsonObject
                        {
                            ["plugin"] = new JsonObject
                            {
                                ["kind"] = "signoz/CompositeQuery",
                                ["spec"] = new JsonObject { ["queries"] = new JsonArray(LogBuilderQuery(spec)) }
                            }
                        }
                    }
                }
            }
        };
    }

    private static JsonObject ValuePanel(string title, string filter, string aggregation)
    {
        var spec = BaseLogQuerySpec(filter, aggregation, "scalar");
        return new JsonObject
        {
            ["kind"] = "Panel",
            ["spec"] = new JsonObject
            {
                ["display"] = new JsonObject { ["name"] = title },
                ["plugin"] = new JsonObject
                {
                    ["kind"] = "signoz/ValuePanel",
                    ["spec"] = new JsonObject
                    {
                        ["visualization"] = new JsonObject { ["timePreference"] = "global_time" }
                    }
                },
                ["queries"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["kind"] = "scalar",
                        ["spec"] = new JsonObject
                        {
                            ["plugin"] = new JsonObject
                            {
                                ["kind"] = "signoz/CompositeQuery",
                                ["spec"] = new JsonObject { ["queries"] = new JsonArray(LogBuilderQuery(spec)) }
                            }
                        }
                    }
                }
            }
        };
    }

    private static JsonObject BaseLogQuerySpec(string filter, string aggregation, string requestKind)
    {
        var spec = new JsonObject
        {
            ["name"] = "A",
            ["signal"] = "logs",
            ["source"] = "",
            ["disabled"] = false,
            ["filter"] = new JsonObject { ["expression"] = filter },
            ["having"] = new JsonObject { ["expression"] = "" },
            ["aggregations"] = new JsonArray(new JsonObject { ["expression"] = aggregation }),
            ["limit"] = 100,
            ["order"] = new JsonArray(Order("__result", "desc"))
        };

        if (requestKind == "time_series")
            spec["stepInterval"] = (JsonNode?)null;

        return spec;
    }

    private static JsonObject LogBuilderQuery(JsonObject spec) =>
        new()
        {
            ["type"] = "builder_query",
            ["spec"] = spec
        };

    private static JsonObject Grid(string title, JsonArray items) =>
        new()
        {
            ["kind"] = "Grid",
            ["spec"] = new JsonObject
            {
                ["display"] = new JsonObject { ["title"] = title },
                ["items"] = items
            }
        };

    private static JsonObject Item(int x, int y, int width, int height, string panelId) =>
        new()
        {
            ["x"] = x,
            ["y"] = y,
            ["width"] = width,
            ["height"] = height,
            ["content"] = new JsonObject { ["$ref"] = $"#/spec/panels/{panelId}" }
        };

    private static JsonObject DynamicVariable(string name, string attribute, string display) =>
        new()
        {
            ["kind"] = "ListVariable",
            ["spec"] = new JsonObject
            {
                ["name"] = name,
                ["display"] = new JsonObject
                {
                    ["name"] = display,
                    ["description"] = ""
                },
                ["allowMultiple"] = false,
                ["allowAllValue"] = true,
                ["sort"] = "none",
                ["plugin"] = new JsonObject
                {
                    ["kind"] = "signoz/DynamicVariable",
                    ["spec"] = new JsonObject
                    {
                        ["name"] = attribute,
                        ["signal"] = "logs"
                    }
                }
            }
        };

    private static JsonObject Tag(string key, string value) =>
        new() { ["key"] = key, ["value"] = value };

    private static JsonObject Order(string name, string direction) =>
        new()
        {
            ["key"] = new JsonObject { ["name"] = name },
            ["direction"] = direction
        };

    private static string PanelId(string project, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ntbot-signoz:{project}:{key}"));
        return new Guid(bytes.AsSpan(0, 16)).ToString();
    }

    private static void EnsureProject(string project)
    {
        if (!ObservabilityProjects.IsKnown(project))
            throw new ArgumentOutOfRangeException(nameof(project), project, "Unknown observability project.");
    }
}
