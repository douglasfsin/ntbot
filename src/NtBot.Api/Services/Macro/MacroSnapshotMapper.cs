using NtBot.Macro.DTO;
using NtBot.Macro.Providers.Fred;

namespace NtBot.Api.Services.Macro;

public static class MacroSnapshotMapper
{
    public static MacroContextResult ToContextResult(MacroSnapshot snapshot, Dictionary<string, decimal>? correlations = null)
    {
        var vix = snapshot.Indicators
            .FirstOrDefault(i => i.SeriesId.Equals(FredSeries.Vix, StringComparison.OrdinalIgnoreCase))?.Value ?? 0m;

        var riskMode = MapRiskMode(snapshot, vix);
        var volatility = MapVolatility(snapshot.Volatility, vix);

        return new MacroContextResult
        {
            AnalysisTime = snapshot.Timestamp,
            Bias = MapBias(snapshot.MacroScore),
            RiskMode = riskMode,
            ConfidenceScore = snapshot.Confidence,
            Correlations = correlations ?? new Dictionary<string, decimal>(),
            VolatilityRegime = volatility,
            VIXLevel = vix,
            IsRiskOn = snapshot.RiskSentiment == MacroRegimeLabel.Bullish,
            Observations =
            [
                $"Regime: {MacroRegimeDisplay.ToLabel(snapshot.MacroScore)}",
                $"Liquidez: {snapshot.Liquidity}",
                $"Dólar: {snapshot.DollarStrength}",
                $"Volatilidade: {snapshot.Volatility}",
                $"Provider: {snapshot.Provider}",
                ..snapshot.UpcomingEvents.Take(3).Select(e => $"Evento: {e.EventName} ({e.Impact})")
            ]
        };
    }

    private static MacroBias MapBias(MacroRegimeLabel score) => score switch
    {
        MacroRegimeLabel.Bullish => MacroBias.BULLISH,
        MacroRegimeLabel.Bearish => MacroBias.BEARISH,
        _ => MacroBias.NEUTRAL
    };

    private static RiskMode MapRiskMode(MacroSnapshot snapshot, decimal vix)
    {
        if (vix >= 30 || snapshot.Volatility is MacroLevel.VeryHigh)
            return RiskMode.BLOCKED;

        if (vix >= 20 || snapshot.Volatility is MacroLevel.High)
            return RiskMode.REDUCED;

        if (snapshot.UpcomingEvents.Any(e =>
                e.Impact.Equals("HIGH", StringComparison.OrdinalIgnoreCase) &&
                Math.Abs((e.EventTime - DateTime.UtcNow).TotalMinutes) <= 30))
            return RiskMode.BLOCKED;

        return RiskMode.NORMAL;
    }

    private static VolatilityRegime MapVolatility(MacroLevel level, decimal vix)
    {
        if (vix >= 35 || level is MacroLevel.VeryHigh) return VolatilityRegime.EXTREME;
        if (vix >= 25 || level is MacroLevel.High) return VolatilityRegime.HIGH;
        if (vix <= 12 || level is MacroLevel.Low or MacroLevel.VeryLow) return VolatilityRegime.LOW;
        return VolatilityRegime.NORMAL;
    }
}
