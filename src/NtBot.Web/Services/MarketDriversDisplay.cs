using NtBot.Web.Models;

namespace NtBot.Web.Services;

public static class MarketDriversDisplay
{
    public static string ImpactClass(string? impact) => impact switch
    {
        "VeryPositive" or "Muito Positivo" => "text-bull",
        "Positive" or "Levemente Positivo" or "Positivo" => "text-bull",
        "VeryNegative" or "Muito Negativo" => "text-bear",
        "Negative" or "Levemente Negativo" or "Negativo" => "text-bear",
        _ => "app-muted"
    };

    public static string ScoreBadgeClass(int score) => score switch
    {
        >= 70 => "nt-badge-success",
        <= 30 => "nt-badge-danger",
        _ => ""
    };

    public static string HeatClass(int score) => score switch
    {
        >= 75 => "md-heat-strong-bull",
        >= 55 => "md-heat-bull",
        <= 25 => "md-heat-strong-bear",
        <= 45 => "md-heat-bear",
        _ => "md-heat-neutral"
    };

    public static string ChangeArrow(decimal variation) => variation switch
    {
        > 0 => "▲",
        < 0 => "▼",
        _ => "—"
    };

    public static string FormatImpactLabel(MarketDriverModel driver)
    {
        if (!string.IsNullOrWhiteSpace(driver.Recommendation) && !LooksLikeEnumToken(driver.Recommendation))
            return driver.Recommendation;

        return FormatImpactToken(driver.Impact);
    }

    public static string FormatImpactToken(string? impact) => impact switch
    {
        "VeryPositive" or "Muito Positivo" => "Muito Positivo",
        "Positive" or "Positivo" => "Positivo",
        "SlightlyPositive" or "Levemente Positivo" => "Levemente Positivo",
        "VeryNegative" or "Muito Negativo" => "Muito Negativo",
        "Negative" or "Negativo" => "Negativo",
        "SlightlyNegative" or "Levemente Negativo" => "Levemente Negativo",
        "Neutral" or "Neutro" => "Neutro",
        _ => string.IsNullOrWhiteSpace(impact) ? "Neutro" : impact
    };

    private static bool LooksLikeEnumToken(string value) =>
        value.All(c => char.IsLetterOrDigit(c)) && value.Any(char.IsUpper);

    public static string GaugeColor(int score) => score switch
    {
        >= 80 => "#22c55e",
        >= 60 => "#84cc16",
        >= 40 => "#94a3b8",
        >= 20 => "#f97316",
        _ => "#ef4444"
    };
}
