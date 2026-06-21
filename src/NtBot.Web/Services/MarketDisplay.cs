namespace NtBot.Web.Services;

public static class MarketDisplay
{
    public static string ChangeArrow(decimal changePercent) => changePercent switch
    {
        > 0 => "▲",
        < 0 => "▼",
        _ => "—"
    };

    public static string ChangeClass(decimal changePercent) => changePercent switch
    {
        > 0 => "text-bull",
        < 0 => "text-bear",
        _ => "app-muted"
    };

    public static string HeatClass(decimal changePercent) => changePercent switch
    {
        >= 2 => "mi-heat-strong-bull",
        > 0 => "mi-heat-bull",
        <= -2 => "mi-heat-strong-bear",
        < 0 => "mi-heat-bear",
        _ => "mi-heat-neutral"
    };

    public static string HealthBadgeClass(string? health) => health switch
    {
        "Healthy" => "nt-badge-success",
        "Degraded" => "nt-badge-warning",
        "Unhealthy" => "nt-badge-danger",
        _ => ""
    };

    public static string Health(string? health) => health switch
    {
        "Healthy" => "Saudável",
        "Degraded" => "Degradado",
        "Unhealthy" => "Indisponível",
        "Disabled" => "Desativado",
        _ => health ?? "—"
    };
}
