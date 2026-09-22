namespace NtBot.Connector.Windows.Configuration;

/// <summary>
/// Fonte ativa de market data do Profit no Connector.
/// Mutuamente exclusiva para publicação de ticks.
/// </summary>
public enum ProfitMarketDataMode
{
    /// <summary>DDE nativo ProfitChart (profitchart|COT).</summary>
    Dde = 0,

    /// <summary>RTD COM (Interop.RTDTrading).</summary>
    Rtd = 1,

    /// <summary>ProfitDLL via MarketData.API (SignalR marketHub).</summary>
    ProfitDll = 2
}

public static class ProfitMarketDataModeExtensions
{
    public static string ToDisplayName(this ProfitMarketDataMode mode) => mode switch
    {
        ProfitMarketDataMode.Dde => "DDE (ProfitChart)",
        ProfitMarketDataMode.Rtd => "RTD (COM)",
        ProfitMarketDataMode.ProfitDll => "ProfitDLL (MarketData.API)",
        _ => mode.ToString()
    };

    public static bool TryParse(string? value, out ProfitMarketDataMode mode)
    {
        mode = ProfitMarketDataMode.Dde;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        if (Enum.TryParse(normalized, ignoreCase: true, out mode))
            return true;

        mode = normalized.ToUpperInvariant() switch
        {
            "DDE" or "PROFITDDE" or "PROFIT_DDE" => ProfitMarketDataMode.Dde,
            "RTD" or "PROFIT" or "PROFITRTD" or "COM" => ProfitMarketDataMode.Rtd,
            "DLL" or "PROFITDLL" or "PROFIT_DLL" or "MARKETDATA" or "MARKETDATAAPI" => ProfitMarketDataMode.ProfitDll,
            _ => ProfitMarketDataMode.Dde
        };

        return true;
    }
}
