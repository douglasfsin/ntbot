using NtBot.MarketDrivers.Configuration;
using NtBot.MarketDrivers.Models;
using NtBot.MarketIntelligence.Models;
using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Models;
using NtBot.Macro.DTO;

namespace NtBot.TradingIntelligence.Services;

internal static class InstitutionalEngineComposer
{
    public static EngineAnalysisResult BuildMacro(MacroSnapshot macro)
    {
        if (macro.MacroScore == MacroRegimeLabel.Unknown && macro.Confidence <= 0)
        {
            return EngineAnalysisResult.Unknown(
                "Macro",
                InstitutionalWeights.Macro,
                "Dados macro indisponíveis.",
                "macro-service");
        }

        var score = macro.MacroScore switch
        {
            MacroRegimeLabel.Bullish => (int)Math.Clamp(55m + macro.Confidence * 0.45m, 0, 100),
            MacroRegimeLabel.Bearish => (int)Math.Clamp(45m - macro.Confidence * 0.45m, 0, 100),
            _ => (int)Math.Clamp(macro.Confidence * 0.4m + 30, 0, 100)
        };

        var bias = macro.MacroScore switch
        {
            MacroRegimeLabel.Bullish => EngineMarketBias.Bullish,
            MacroRegimeLabel.Bearish => EngineMarketBias.Bearish,
            _ => EngineMarketBias.Sideways
        };

        var signals = new List<string> { $"regime {macro.MacroScore}" };
        if (macro.Recommendations.Count > 0)
            signals.Add(macro.Recommendations[0].Reason);

        return EngineAnalysisResult.Known(
            "Macro",
            score,
            Math.Clamp(macro.Confidence, 25, 95),
            InstitutionalWeights.Macro,
            bias,
            signals,
            "macro-service");
    }

    public static EngineAnalysisResult BuildDrivers(MarketDriversSnapshot driverSnapshot)
    {
        var available = driverSnapshot.Drivers
            .Where(d => !d.Description.Contains("indisponível", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (available.Count < 3)
        {
            return EngineAnalysisResult.Unknown(
                "Drivers",
                InstitutionalWeights.Drivers,
                $"Drivers insuficientes ({available.Count}/3 mínimo).",
                "market-drivers");
        }

        var signals = available
            .OrderByDescending(d => d.Weight)
            .Take(4)
            .Select(d => $"{d.Name} {d.Direction}")
            .ToList();

        var bias = driverSnapshot.Score.Score switch
        {
            >= 58 => EngineMarketBias.Bullish,
            <= 42 => EngineMarketBias.Bearish,
            _ => EngineMarketBias.Sideways
        };

        return EngineAnalysisResult.Known(
            "Drivers",
            driverSnapshot.Score.Score,
            Math.Clamp(driverSnapshot.Score.Confidence * 100, 30, 95),
            InstitutionalWeights.Drivers,
            bias,
            signals,
            "market-drivers");
    }

    public static EngineAnalysisResult BuildCorrelation(AssetImpactResult? assetImpact) =>
        BuildCorrelationForAsset(string.Empty, assetImpact, null);

    /// <summary>
    /// Correlação + drivers do ativo. Para XAUUSD reforça pesos de DXY / yields / VIX.
    /// </summary>
    public static EngineAnalysisResult BuildCorrelationForAsset(
        string asset,
        AssetImpactResult? assetImpact,
        MarketDriversSnapshot? drivers)
    {
        var hasCorr = assetImpact is not null && assetImpact.Factors.Count > 0;
        var driverScore = drivers?.Score.Score;
        var availableDrivers = drivers?.Drivers
            .Where(d => !d.Description.Contains("indisponível", StringComparison.OrdinalIgnoreCase))
            .ToList() ?? [];

        if (!hasCorr && availableDrivers.Count < 2)
        {
            return EngineAnalysisResult.Unknown(
                "Correlação",
                InstitutionalWeights.Correlation,
                "Correlação/drivers indisponíveis.",
                "market-intelligence");
        }

        var score = 50m;
        var signals = new List<string>();
        var confidence = 50m;

        if (hasCorr)
        {
            score = (decimal)((assetImpact!.ImpactScore + 1) / 2 * 100);
            signals.AddRange(assetImpact.Factors
                .OrderByDescending(f => Math.Abs(f.Correlation) * f.Weight)
                .Take(3)
                .Select(f => $"{f.Label} corr {f.Correlation:0.00}"));
            confidence = (decimal)Math.Clamp(
                assetImpact.Factors.Average(f => Math.Abs(f.Correlation)) * 100,
                35, 90);
        }

        if (driverScore is int ds && availableDrivers.Count >= 2)
        {
            // Blend 55% correlação / 45% drivers (ouro: drivers DXY/yields são críticos)
            var driverWeight = IsGold(asset) ? 0.50m : 0.45m;
            var corrWeight = 1m - driverWeight;
            score = hasCorr ? score * corrWeight + ds * driverWeight : ds;
            confidence = Math.Clamp((confidence + drivers!.Score.Confidence * 100) / 2, 30, 92);
            signals.AddRange(availableDrivers
                .OrderByDescending(d => AdjustGoldDriverWeight(asset, d))
                .Take(3)
                .Select(d => $"{d.Name} {d.Direction}"));
        }

        var bias = (int)score switch
        {
            >= 58 => EngineMarketBias.Bullish,
            <= 42 => EngineMarketBias.Bearish,
            _ => EngineMarketBias.Sideways
        };

        return EngineAnalysisResult.Known(
            "Correlação",
            (int)Math.Clamp(Math.Round(score), 0, 100),
            confidence,
            InstitutionalWeights.Correlation,
            bias,
            signals.Distinct().Take(5).ToList(),
            "market-intelligence+drivers");
    }

    private static decimal AdjustGoldDriverWeight(string asset, MarketDriver driver)
    {
        if (!IsGold(asset))
            return driver.Weight;

        // XAUUSD: DXY e real yields dominam; VIX e Gold futures reforçam
        var name = driver.Name ?? string.Empty;
        if (name.Contains("Dollar", StringComparison.OrdinalIgnoreCase)
            || name.Contains("DXY", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Yield", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Treasury", StringComparison.OrdinalIgnoreCase))
            return driver.Weight * 1.35m;
        if (name.Contains("VIX", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Gold", StringComparison.OrdinalIgnoreCase))
            return driver.Weight * 1.15m;
        return driver.Weight;
    }

    private static bool IsGold(string asset) =>
        asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase)
        || asset.Equals("XAU", StringComparison.OrdinalIgnoreCase)
        || asset.Equals("GOLD", StringComparison.OrdinalIgnoreCase);

    public static EngineAnalysisResult BuildMomentumFromDriver(MarketDriver? momentumDriver, int quantScore)
    {
        if (momentumDriver is not null &&
            !momentumDriver.Description.Contains("indisponível", StringComparison.OrdinalIgnoreCase))
        {
            var score = (int)Math.Clamp(50 + momentumDriver.Variation * 15, 0, 100);
            return EngineAnalysisResult.Known(
                "Momentum",
                score,
                Math.Clamp(momentumDriver.Confidence * 100, 35, 90),
                InstitutionalWeights.Momentum,
                score >= 58 ? EngineMarketBias.Bullish : score <= 42 ? EngineMarketBias.Bearish : EngineMarketBias.Sideways,
                [$"driver momentum {momentumDriver.Variation:F2}%"],
                "market-drivers");
        }

        if (quantScore is > 0 and not 50)
        {
            return EngineAnalysisResult.Known(
                "Momentum",
                quantScore,
                55,
                InstitutionalWeights.Momentum,
                quantScore >= 58 ? EngineMarketBias.Bullish : quantScore <= 42 ? EngineMarketBias.Bearish : EngineMarketBias.Sideways,
                ["quant score de mercado"],
                "market-intelligence");
        }

        return EngineAnalysisResult.Unknown(
            "Momentum",
            InstitutionalWeights.Momentum,
            "Momentum indisponível — aguardando driver ou quant score.",
            "market-drivers");
    }

    public static bool IsLowLiquiditySession() => IsLowLiquiditySession(string.Empty);

    /// <summary>
    /// Sessão de baixa liquidez — B3 para índices/ações BR; UTC Asia/late para ouro/FX.
    /// </summary>
    public static bool IsLowLiquiditySession(string asset)
    {
        if (IsGold(asset)
            || asset.Equals("EURUSD", StringComparison.OrdinalIgnoreCase)
            || asset.Equals("GBPUSD", StringComparison.OrdinalIgnoreCase)
            || asset.Equals("USDJPY", StringComparison.OrdinalIgnoreCase))
        {
            var hour = DateTime.UtcNow.Hour;
            return hour is >= 21 or < 7; // late + Asia
        }

        var brt = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"));

        var minutes = brt.Hour * 60 + brt.Minute;
        return minutes is < 9 * 60 + 15 or > 17 * 60 + 30;
    }
}
