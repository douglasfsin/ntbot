using NtBot.Macro.Configuration;
using NtBot.Macro.Providers.Fred;

namespace NtBot.Macro.Engine;

public sealed class MacroEngine : IMacroEngine
{
    private readonly IMacroRecommendationEngine _recommendations;

    public MacroEngine(IMacroRecommendationEngine recommendations)
    {
        _recommendations = recommendations;
    }

    public MacroSnapshot BuildSnapshot(IReadOnlyList<MacroProviderPayload> payloads, string? symbol = null)
    {
        var indicators = payloads.SelectMany(p => p.Indicators).ToList();
        var events = payloads.SelectMany(p => p.Events).OrderBy(e => e.EventTime).Take(50).ToList();

        var vix = GetValue(indicators, FredSeries.Vix);
        var fedFunds = GetValue(indicators, FredSeries.FedFunds);
        var us10y = GetValue(indicators, FredSeries.Us10Y);
        var us2y = GetValue(indicators, FredSeries.Us2Y);
        var unemployment = GetValue(indicators, FredSeries.Unemployment);

        var liquidity = ScoreLiquidity(vix, fedFunds, us10y);
        var dollar = ScoreDollar(us10y, us2y);
        var volatility = ScoreVolatility(vix);
        var interest = ScoreInterest(fedFunds, us10y, us2y);
        var inflation = ScoreInflation(indicators);
        var risk = ScoreRisk(vix, unemployment);
        var macroScore = ScoreMacro(liquidity, dollar, volatility, interest, inflation, risk);
        var confidence = ComputeConfidence(indicators.Count, events.Count);

        var tickers = string.IsNullOrWhiteSpace(symbol)
            ? new[] { "WIN", "WDO", "XAUUSD", "ES", "NQ" }
            : new[] { MacroSymbolAliases.Normalize(symbol) };

        var snapshot = new MacroSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Liquidity = liquidity,
            DollarStrength = dollar,
            Volatility = volatility,
            InterestRate = interest,
            Inflation = inflation,
            RiskSentiment = risk,
            MacroScore = macroScore,
            Confidence = confidence,
            UpcomingEvents = events,
            Provider = "aggregate",
            Indicators = indicators,
            Recommendations = _recommendations.GetRecommendations(
                new MacroSnapshot
                {
                    Liquidity = liquidity,
                    DollarStrength = dollar,
                    Volatility = volatility,
                    InterestRate = interest,
                    Inflation = inflation,
                    RiskSentiment = risk,
                    MacroScore = macroScore,
                    Confidence = confidence
                },
                tickers)
        };

        return snapshot;
    }

    private static decimal? GetValue(IEnumerable<MacroIndicatorValue> indicators, string seriesId)
    {
        return indicators.FirstOrDefault(i => i.SeriesId.Equals(seriesId, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static MacroLevel ScoreLiquidity(decimal? vix, decimal? fedFunds, decimal? us10y)
    {
        if (vix is null) return MacroLevel.Neutral;
        if (vix <= 16 && fedFunds is <= 5.5m && us10y is <= 5m) return MacroLevel.High;
        if (vix >= 25) return MacroLevel.Low;
        return MacroLevel.Neutral;
    }

    private static MacroLevel ScoreDollar(decimal? us10y, decimal? us2y)
    {
        if (us10y is null && us2y is null) return MacroLevel.Neutral;
        var spread = (us10y ?? 0) - (us2y ?? 0);
        if (spread >= 0 && (us10y ?? 0) >= 4m) return MacroLevel.High;
        if (spread < 0) return MacroLevel.Low;
        return MacroLevel.Neutral;
    }

    private static MacroLevel ScoreVolatility(decimal? vix)
    {
        if (vix is null) return MacroLevel.Neutral;
        if (vix <= 16) return MacroLevel.Low;
        if (vix >= 25) return MacroLevel.High;
        return MacroLevel.Neutral;
    }

    private static MacroLevel ScoreInterest(decimal? fedFunds, decimal? us10y, decimal? us2y)
    {
        if (fedFunds is null && us10y is null) return MacroLevel.Neutral;
        var stable = fedFunds is >= 4m and <= 5.5m && us10y is >= 3.5m and <= 5m;
        if (stable && Math.Abs((us10y ?? 0) - (us2y ?? 0)) < 0.5m) return MacroLevel.Neutral;
        if ((us10y ?? 0) - (us2y ?? 0) > 1m) return MacroLevel.High;
        return MacroLevel.Neutral;
    }

    private static MacroLevel ScoreInflation(IReadOnlyList<MacroIndicatorValue> indicators)
    {
        var cpi = indicators.FirstOrDefault(i => i.SeriesId == FredSeries.Cpi)?.Value;
        var pce = indicators.FirstOrDefault(i => i.SeriesId == FredSeries.Pce)?.Value;
        if (cpi is null && pce is null) return MacroLevel.Neutral;
        if ((cpi ?? 300) < 320 && (pce ?? 120) < 130) return MacroLevel.Low;
        return MacroLevel.Neutral;
    }

    private static MacroRegimeLabel ScoreRisk(decimal? vix, decimal? unemployment)
    {
        if (vix is <= 18 && unemployment is <= 5m) return MacroRegimeLabel.Bullish;
        if (vix is >= 28 || unemployment is >= 6m) return MacroRegimeLabel.Bearish;
        return MacroRegimeLabel.Neutral;
    }

    private static MacroRegimeLabel ScoreMacro(
        MacroLevel liquidity, MacroLevel dollar, MacroLevel volatility,
        MacroLevel interest, MacroLevel inflation, MacroRegimeLabel risk)
    {
        var bullishSignals =
            (liquidity is MacroLevel.High or MacroLevel.VeryHigh ? 1 : 0) +
            (dollar is MacroLevel.High or MacroLevel.VeryHigh ? 1 : 0) +
            (volatility is MacroLevel.Low or MacroLevel.VeryLow ? 1 : 0) +
            (risk == MacroRegimeLabel.Bullish ? 1 : 0);

        if (bullishSignals >= 3) return MacroRegimeLabel.Bullish;
        if (bullishSignals <= 1) return MacroRegimeLabel.Bearish;
        return MacroRegimeLabel.Neutral;
    }

    private static decimal ComputeConfidence(int indicatorCount, int eventCount)
    {
        var baseScore = Math.Min(95m, 50m + indicatorCount * 5m + Math.Min(eventCount, 5) * 2m);
        return Math.Round(baseScore, 0);
    }
}
