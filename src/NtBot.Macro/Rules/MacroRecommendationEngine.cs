using NtBot.Macro.Configuration;
using NtBot.Macro.Engine;

namespace NtBot.Macro.Rules;

public sealed class MacroRecommendationEngine : IMacroRecommendationEngine
{
    private static readonly Dictionary<string, AssetProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WIN"] = new(AssetClass.IndexBrazil, MacroSensitivity.RiskOn),
        ["WDO"] = new(AssetClass.FxBrazil, MacroSensitivity.DollarInverse),
        ["XAUUSD"] = new(AssetClass.Commodity, MacroSensitivity.SafeHaven),
        ["ES"] = new(AssetClass.IndexUs, MacroSensitivity.RiskOn),
        ["NQ"] = new(AssetClass.IndexUs, MacroSensitivity.RiskOn),
        ["PETR4"] = new(AssetClass.EquityBrazil, MacroSensitivity.OilBeta)
    };

    public IReadOnlyList<MacroRecommendation> GetRecommendations(MacroSnapshot snapshot, params string[] tickers)
    {
        return tickers.Select(t => GetRecommendation(snapshot, t)).ToList();
    }

    public MacroRecommendation GetRecommendation(MacroSnapshot snapshot, string ticker)
    {
        var key = MacroSymbolAliases.Normalize(ticker);
        Profiles.TryGetValue(key, out var profile);
        profile ??= new AssetProfile(AssetClass.Generic, MacroSensitivity.RiskOn);

        var score = ComputeAssetScore(snapshot, profile);
        var action = score switch
        {
            >= 2 => MacroRecommendationAction.StrongBuy,
            >= 1 => MacroRecommendationAction.ModerateBuy,
            <= -2 => MacroRecommendationAction.StrongSell,
            <= -1 => MacroRecommendationAction.ModerateSell,
            _ => MacroRecommendationAction.Neutral
        };

        var risk = snapshot.Volatility switch
        {
            MacroLevel.High or MacroLevel.VeryHigh => MacroRiskLevel.High,
            MacroLevel.Low or MacroLevel.VeryLow => MacroRiskLevel.Low,
            _ => MacroRiskLevel.Medium
        };

        return new MacroRecommendation
        {
            Ticker = key,
            Action = action,
            Confidence = snapshot.Confidence,
            RiskLevel = risk,
            Reason = BuildReason(snapshot, profile, action)
        };
    }

    private static int ComputeAssetScore(MacroSnapshot snapshot, AssetProfile profile)
    {
        var score = snapshot.MacroScore switch
        {
            MacroRegimeLabel.Bullish => 1,
            MacroRegimeLabel.Bearish => -1,
            _ => 0
        };

        score += profile.Sensitivity switch
        {
            MacroSensitivity.DollarInverse => snapshot.DollarStrength switch
            {
                MacroLevel.High => -1,
                MacroLevel.Low => 1,
                _ => 0
            },
            MacroSensitivity.SafeHaven => snapshot.RiskSentiment switch
            {
                MacroRegimeLabel.Bearish => 1,
                MacroRegimeLabel.Bullish => -1,
                _ => 0
            },
            _ => snapshot.Liquidity switch
            {
                MacroLevel.High => 1,
                MacroLevel.Low => -1,
                _ => 0
            }
        };

        if (snapshot.Volatility is MacroLevel.High && profile.Class is AssetClass.IndexBrazil or AssetClass.IndexUs)
        {
            score -= 1;
        }

        return score;
    }

    private static string BuildReason(MacroSnapshot snapshot, AssetProfile profile, MacroRecommendationAction action)
    {
        return action switch
        {
            MacroRecommendationAction.StrongBuy or MacroRecommendationAction.ModerateBuy =>
                $"Regime {snapshot.MacroScore}, liquidez {snapshot.Liquidity}, volatilidade {snapshot.Volatility}. Perfil {profile.Class}.",
            MacroRecommendationAction.StrongSell or MacroRecommendationAction.ModerateSell =>
                $"Pressão macro ({snapshot.MacroScore}), dólar {snapshot.DollarStrength}, risco {snapshot.RiskSentiment}.",
            _ => $"Ambiente neutro para {profile.Class}; aguardar confirmação."
        };
    }

    private enum AssetClass { Generic, IndexBrazil, IndexUs, FxBrazil, Commodity, EquityBrazil }
    private enum MacroSensitivity { RiskOn, DollarInverse, SafeHaven, OilBeta }
    private sealed record AssetProfile(AssetClass Class, MacroSensitivity Sensitivity);
}
