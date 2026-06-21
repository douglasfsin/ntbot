namespace NtBot.Macro.DTO;

public static class MacroRegimeDisplay
{
    public static string ToLabel(MacroRegimeLabel regime) => regime switch
    {
        MacroRegimeLabel.Bullish => "Bullish",
        MacroRegimeLabel.Bearish => "Tendência de baixa",
        MacroRegimeLabel.Neutral => "Neutro",
        _ => regime.ToString()
    };

    public static string ToLabel(string? regime) => regime switch
    {
        "Bullish" => "Bullish",
        "Bearish" => "Tendência de baixa",
        "Neutral" => "Neutro",
        _ => regime ?? string.Empty
    };
}
