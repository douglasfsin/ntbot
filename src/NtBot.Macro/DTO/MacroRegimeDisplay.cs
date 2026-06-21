namespace NtBot.Macro.DTO;

public static class MacroRegimeDisplay
{
    public static string ToLabel(MacroRegimeLabel regime) => regime switch
    {
        MacroRegimeLabel.Bullish => "Tendência de alta",
        MacroRegimeLabel.Bearish => "Tendência de baixa",
        MacroRegimeLabel.Neutral => "Neutro",
        _ => regime.ToString()
    };

    public static string ToLabel(string? regime) => regime switch
    {
        "Bullish" => "Tendência de alta",
        "Bearish" => "Tendência de baixa",
        "Neutral" => "Neutro",
        _ => regime ?? string.Empty
    };
}
