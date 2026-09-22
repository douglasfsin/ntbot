using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine;

/// <summary>
/// Confluência institucional: pesos Structure 30% / Liquidity 20% / Volume 15% /
/// Wyckoff 10% / Correlation 10% / Macro 10% / Volatility 5%.
/// Confiança Baixa/Muito Baixa → força AGUARDAR / Neutro.
/// </summary>
public sealed class ConfluenceEngine : IConfluenceEngine
{
    public ConfluenceScoreResult Calculate(InstitutionalConfluenceInput input) =>
        CalculateInternal(input);

    public ConfluenceScoreResult Calculate(EngineScoreInput legacy) =>
        CalculateInternal(MapLegacy(legacy));

    private static InstitutionalConfluenceInput MapLegacy(EngineScoreInput input)
    {
        // Estrutura: média SMC + Momentum (Quant só entra se informado)
        var structureParts = new List<int> { input.SmcScore };
        if (input.MomentumScore is > 0) structureParts.Add(input.MomentumScore);
        else if (input.QuantScore is > 0) structureParts.Add(input.QuantScore);
        var structureScore = (int)Math.Round(structureParts.Average());

        var corrScore = input.CorrelationScore;
        if (input.DriverScore is > 0)
            corrScore = (int)Math.Round((input.CorrelationScore + input.DriverScore) / 2.0);

        return new InstitutionalConfluenceInput
        {
            Asset = input.Asset,
            Engines =
            [
                EngineAnalysisResult.Known("Structure", structureScore, 70, InstitutionalWeights.Structure, BiasFromScore(structureScore)),
                EngineAnalysisResult.Known("Liquidity", input.LiquidityScore > 0 ? input.LiquidityScore : 50, 65, InstitutionalWeights.Liquidity, BiasFromScore(input.LiquidityScore > 0 ? input.LiquidityScore : 50)),
                EngineAnalysisResult.Known("Volume", input.VolumeScore, 70, InstitutionalWeights.Volume, BiasFromScore(input.VolumeScore)),
                EngineAnalysisResult.Known("Wyckoff", input.WyckoffScore, 70, InstitutionalWeights.Wyckoff, BiasFromScore(input.WyckoffScore)),
                EngineAnalysisResult.Known("Correlação", corrScore, 65, InstitutionalWeights.Correlation, BiasFromScore(corrScore)),
                EngineAnalysisResult.Known("Macro", input.MacroScore, 70, InstitutionalWeights.Macro, BiasFromScore(input.MacroScore)),
                EngineAnalysisResult.Known("Volatility", 50, 60, InstitutionalWeights.Volatility, EngineMarketBias.Sideways)
            ]
        };
    }

    private static ConfluenceScoreResult CalculateInternal(InstitutionalConfluenceInput input)
    {
        var directional = input.Engines
            .Where(e => e.Engine != "Risk" && e.Status == EngineDataStatus.Known && e.Score.HasValue)
            .ToList();

        var components = input.Engines.Select(ToComponent).ToList();
        var knownCount = directional.Count;
        var totalCount = input.Engines.Count(e => e.Engine != "Risk");

        int score;
        string dataQuality;
        if (knownCount == 0)
        {
            score = 50;
            dataQuality = "Insuficiente";
        }
        else
        {
            var weightSum = directional.Sum(e => e.EffectiveWeight);
            score = weightSum > 0
                ? (int)Math.Clamp(Math.Round(directional.Sum(e => e.Score!.Value * e.EffectiveWeight) / weightSum), 0, 100)
                : 50;
            dataQuality = knownCount >= totalCount * 0.75 ? "Alta" : knownCount >= totalCount * 0.5 ? "Parcial" : "Baixa";
        }

        var engineConfidence = directional.Count > 0
            ? directional.Average(e => (double)e.Confidence)
            : 0;

        var riskMultiplier = input.Risk?.Confidence is > 0
            ? input.Risk.Confidence / 100m
            : 1m;

        var confidence = (decimal)engineConfidence * riskMultiplier;
        if (knownCount < totalCount)
            confidence *= (decimal)knownCount / Math.Max(1, totalCount);

        // Penalidade de consenso multi-TF
        if (input.TimeframeConsensus?.HasConflict == true)
            confidence *= 0.82m;
        else if (input.TimeframeConsensus is { AgreementPercent: >= 70 })
            confidence = Math.Min(100, confidence * 1.05m);

        confidence = Math.Clamp(confidence, 0, 100);

        var confidenceLevel = ConfidenceLevels.FromScore(confidence);
        var classification = dataQuality == "Insuficiente"
            ? "Dados Insuficientes"
            : ConfluenceClassification.Classify(score);

        var rawRecommendation = dataQuality == "Insuficiente"
            ? "AGUARDAR DADOS"
            : ConfluenceClassification.ClassifyRecommendation(score);

        var bias = ResolveBias(directional, score, input.TimeframeConsensus);
        var riskLevel = ConfluenceClassification.ClassifyRiskLevel(confidence);

        var positive = directional
            .Where(e => e.Score >= 65)
            .Select(e => FormatFactor(e))
            .ToList();

        var negative = directional
            .Where(e => e.Score <= 35)
            .Select(e => FormatFactor(e))
            .ToList();

        var blocking = input.BlockingFactors.ToList();

        // Gate de confiança: nunca Compra/Venda em Baixa/Muito Baixa
        var recommendation = rawRecommendation;
        if (dataQuality != "Insuficiente" && ConfidenceLevels.BlocksDirectionalTrade(confidenceLevel))
        {
            recommendation = "AGUARDAR";
            if (!blocking.Contains("confiança insuficiente para direção"))
                blocking.Add($"confiança {confidenceLevel} — operação direcional bloqueada");
            bias = "Sideways";
            classification = classification is "Alta" or "Muito Alta" or "Confluência Extrema"
                ? "Neutra (conf. baixa)"
                : classification;
        }

        // Anti-loss já sinalizou bloqueio
        if (blocking.Count > 0 &&
            (recommendation.Contains("COMPRA", StringComparison.OrdinalIgnoreCase)
             || recommendation.Contains("VENDA", StringComparison.OrdinalIgnoreCase)))
        {
            recommendation = "AGUARDAR";
            bias = "Sideways";
        }

        var riskSuggestion = recommendation is "AGUARDAR" or "AGUARDAR DADOS" or "NEUTRO"
            ? null
            : input.RiskSuggestion;

        var explanation = BuildNarrativeExplanation(
            input, score, bias, confidence, confidenceLevel, riskLevel,
            recommendation, directional, positive, negative, blocking, riskSuggestion);

        return new ConfluenceScoreResult
        {
            Score = score,
            Classification = classification,
            Recommendation = recommendation,
            Confidence = confidence,
            ConfidenceLevel = confidenceLevel,
            Bias = bias,
            RiskLevel = riskLevel,
            DataQuality = dataQuality,
            KnownEngineCount = knownCount,
            TotalEngineCount = totalCount,
            Components = components,
            PositiveFactors = positive,
            NegativeFactors = negative,
            BlockingFactors = blocking,
            RiskSuggestion = riskSuggestion,
            Explanation = explanation
        };
    }

    private static EngineScoreComponent ToComponent(EngineAnalysisResult engine) =>
        new()
        {
            Engine = engine.Engine,
            Status = engine.Status,
            Score = engine.Score ?? 0,
            Confidence = engine.Confidence,
            Weight = engine.BaseWeight,
            EffectiveWeight = engine.EffectiveWeight,
            WeightedContribution = engine.Score.HasValue ? engine.Score.Value * engine.EffectiveWeight : 0,
            Bias = engine.Bias,
            Impact = engine.Status == EngineDataStatus.Unknown
                ? "Sem Dados"
                : ClassifyImpact(engine.Score ?? 50),
            Tooltip = engine.Status == EngineDataStatus.Unknown
                ? $"{engine.Engine}: sem dados · peso 0%"
                : $"{engine.Engine}: {engine.Score}/100 · confiança {engine.Confidence:F0}% · peso efetivo {(engine.EffectiveWeight * 100):F0}%"
        };

    private static string FormatFactor(EngineAnalysisResult engine)
    {
        var signal = engine.Signals.FirstOrDefault();
        var detail = signal is not null ? $" — {signal}" : string.Empty;
        return $"{engine.Engine} ({engine.Score}/100, conf {engine.Confidence:F0}%){detail}";
    }

    private static string ResolveBias(
        IReadOnlyList<EngineAnalysisResult> directional,
        int score,
        MultiTimeframeConsensus? consensus)
    {
        if (consensus is { HasConflict: false, Bias: "Bullish" or "Bearish" } && consensus.AgreementPercent >= 60)
            return consensus.Bias;

        var structure = directional.FirstOrDefault(e => e.Engine == "Structure" || e.Engine == "Trend");
        if (structure?.Bias is EngineMarketBias.Bullish or EngineMarketBias.Bearish)
            return structure.Bias.ToString();

        var bullish = directional.Count(e => e.Bias == EngineMarketBias.Bullish);
        var bearish = directional.Count(e => e.Bias == EngineMarketBias.Bearish);
        if (bullish > bearish + 1) return "Bullish";
        if (bearish > bullish + 1) return "Bearish";
        return ConfluenceClassification.ClassifyBias(score);
    }

    private static string BuildNarrativeExplanation(
        InstitutionalConfluenceInput input,
        int score,
        string bias,
        decimal confidence,
        string confidenceLevel,
        string riskLevel,
        string recommendation,
        IReadOnlyList<EngineAnalysisResult> directional,
        IReadOnlyList<string> positive,
        IReadOnlyList<string> negative,
        IReadOnlyList<string> blocking,
        TradeRiskSuggestion? risk)
    {
        var lines = new List<string>();

        if (directional.Count == 0)
        {
            lines.Add($"Não há dados suficientes para avaliar {input.Asset} com confiança institucional.");
            lines.Add("Os engines aguardam candles MT5 e drivers antes de emitir direção.");
            return string.Join("\n\n", lines);
        }

        lines.Add($"Decisão: {recommendation} · Bias {bias} · Confiança {confidenceLevel} ({confidence:F0}%).");

        if (input.Session is not null)
            lines.Add($"Sessão: {input.Session.Session} · regime {input.Session.Regime}. {input.Session.Description}");

        if (input.TimeframeConsensus is not null)
            lines.Add(input.TimeframeConsensus.Summary);

        foreach (var name in new[] { "Structure", "Liquidity", "Volume", "Wyckoff", "Macro", "Correlação" })
        {
            var engine = directional.FirstOrDefault(e => e.Engine == name);
            if (engine is not null && engine.Signals.Count > 0)
                lines.Add($"{name}: {string.Join(", ", engine.Signals.Take(3))}.");
        }

        if (positive.Count > 0)
            lines.Add($"Fatores de suporte: {string.Join("; ", positive.Take(3))}.");

        if (negative.Count > 0)
            lines.Add($"Fatores de risco: {string.Join("; ", negative.Take(3))}.");

        if (blocking.Count > 0)
            lines.Add($"Bloqueios anti-perda: {string.Join("; ", blocking.Take(4))}.");

        if (risk is not null)
            lines.Add($"Risco sugerido: {risk.Summary}");

        lines.Add($"Confluence {score}/100 · Risco {riskLevel} · Qualidade de dados considerada.");

        if (input.DailyChangePercent is not null)
            lines.Add($"Variação do período: {input.DailyChangePercent:F2}%.");

        return string.Join("\n\n", lines);
    }

    private static EngineMarketBias BiasFromScore(int score) => score switch
    {
        >= 58 => EngineMarketBias.Bullish,
        <= 42 => EngineMarketBias.Bearish,
        _ => EngineMarketBias.Sideways
    };

    private static string ClassifyImpact(int score) => score switch
    {
        >= 80 => "Muito Positivo",
        >= 60 => "Positivo",
        >= 45 => "Neutro",
        >= 25 => "Negativo",
        _ => "Muito Negativo"
    };
}
