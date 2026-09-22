using NtBot.Shared.MarketData;

namespace NtBot.Shared.Trading;

/// <summary>
/// Resolve o tamanho do tick (mínima variação de preço) para cálculo de stops técnicos.
/// Prefere o tick do broker (MT5 trade_tick_size / point); senão usa defaults conhecidos.
/// </summary>
public static class SymbolTickSize
{
    /// <summary>Timeframe padrão do SL técnico ScalpShort (candle anterior à entrada).</summary>
    public const string ScalpShortStopTimeframe = "M5";

    public static decimal? TryResolve(string? symbol, decimal? brokerTickSize = null)
    {
        if (brokerTickSize is > 0)
            return brokerTickSize.Value;

        var canonical = CandleSymbolAliases.Canonical(
            string.IsNullOrWhiteSpace(symbol) ? string.Empty : symbol.Trim());

        return canonical switch
        {
            "XAUUSD" => 0.01m,
            "WIN" => 5m,
            "WDO" => 0.5m,
            "USDJPY" => 0.001m,
            "EURUSD" or "GBPUSD" or "NZDUSD" => 0.00001m,
            _ => null
        };
    }

    public static decimal RoundToTick(decimal price, decimal tickSize)
    {
        if (tickSize <= 0)
            return Math.Round(price, 2, MidpointRounding.AwayFromZero);

        return Math.Round(price / tickSize, MidpointRounding.AwayFromZero) * tickSize;
    }
}
