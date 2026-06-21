namespace NtBot.Web.Services;

public static class MacroDisplay
{
    public static string Level(string level) => level switch
    {
        "VeryLow" => "Muito Baixa",
        "Low" => "Baixa",
        "Neutral" => "Neutra",
        "High" => "Alta",
        "VeryHigh" => "Muito Alta",
        _ => level
    };

    public static string Dollar(string level) => level switch
    {
        "VeryLow" => "Muito Fraco",
        "Low" => "Fraco",
        "Neutral" => "Neutro",
        "High" => "Forte",
        "VeryHigh" => "Muito Forte",
        _ => level
    };

    public static string Volatility(string level) => level switch
    {
        "VeryLow" => "Muito Baixa",
        "Low" => "Baixa",
        "Neutral" => "Neutra",
        "High" => "Alta",
        "VeryHigh" => "Muito Alta",
        _ => level
    };

    public static string Interest(string level) => level switch
    {
        "VeryLow" => "Em Queda",
        "Low" => "Acomodando",
        "Neutral" => "Estáveis",
        "High" => "Em Alta",
        "VeryHigh" => "Restritivos",
        _ => level
    };

    public static string Inflation(string level) => level switch
    {
        "VeryLow" => "Desinflação",
        "Low" => "Controlada",
        "Neutral" => "Moderada",
        "High" => "Elevada",
        "VeryHigh" => "Crítica",
        _ => level
    };

    public static string Regime(string regime) => regime switch
    {
        "Bullish" => "Bullish",
        "Bearish" => "Tendência de baixa",
        "Neutral" => "Neutro",
        _ => regime
    };

    public static string Action(string action) => action switch
    {
        "StrongBuy" => "Compra Forte",
        "ModerateBuy" => "Compra Moderada",
        "Neutral" => "Neutro",
        "ModerateSell" => "Venda Moderada",
        "StrongSell" => "Venda Forte",
        _ => action
    };

    public static string ActionClass(string action) => action switch
    {
        "StrongBuy" or "ModerateBuy" => "text-bull",
        "StrongSell" or "ModerateSell" => "text-bear",
        _ => ""
    };

    public static string Health(string status) => status switch
    {
        "Healthy" => "Saudável",
        "Degraded" => "Degradado",
        "Unhealthy" => "Indisponível",
        "Disabled" => "Desativado",
        _ => status
    };

    public static string HealthBadgeClass(string status) => status switch
    {
        "Healthy" => "nt-badge-success",
        "Degraded" => "",
        "Unhealthy" => "nt-badge-danger",
        _ => ""
    };
}
