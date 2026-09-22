namespace NtBot.TradingIntelligence.Models;

using NtBot.TradingIntelligence.Configuration;

public enum EngineDataStatus
{
    Known,
    Unknown
}

public enum EngineMarketBias
{
    Unknown,
    Bullish,
    Bearish,
    Sideways
}

public sealed class EngineAnalysisResult
{
    public string Engine { get; init; } = string.Empty;
    public EngineDataStatus Status { get; init; } = EngineDataStatus.Unknown;
    public int? Score { get; init; }
    public decimal Confidence { get; init; }
    public decimal BaseWeight { get; init; }
    public EngineMarketBias Bias { get; init; } = EngineMarketBias.Unknown;
    public IReadOnlyList<string> Signals { get; init; } = [];
    public string? DataSource { get; init; }
    public long ProcessingMs { get; init; }

    public decimal EffectiveWeight =>
        Status == EngineDataStatus.Known && Score.HasValue
            ? BaseWeight * Math.Clamp(Confidence / 100m, 0.15m, 1m)
            : 0m;

    public static EngineAnalysisResult Unknown(
        string engine,
        decimal baseWeight,
        string reason,
        string? dataSource = null) =>
        new()
        {
            Engine = engine,
            Status = EngineDataStatus.Unknown,
            Score = null,
            Confidence = 0,
            BaseWeight = baseWeight,
            Bias = EngineMarketBias.Unknown,
            Signals = string.IsNullOrWhiteSpace(reason) ? [] : [reason],
            DataSource = dataSource
        };

    public static EngineAnalysisResult Known(
        string engine,
        int score,
        decimal confidence,
        decimal baseWeight,
        EngineMarketBias bias,
        IReadOnlyList<string>? signals = null,
        string? dataSource = null,
        long processingMs = 0) =>
        new()
        {
            Engine = engine,
            Status = EngineDataStatus.Known,
            Score = Math.Clamp(score, 0, 100),
            Confidence = Math.Clamp(confidence, 0, 100),
            BaseWeight = baseWeight,
            Bias = bias,
            Signals = signals ?? [],
            DataSource = dataSource,
            ProcessingMs = processingMs
        };

    /// <summary>Risk engine — confidence only, no directional score.</summary>
    public static EngineAnalysisResult RiskOnly(
        decimal confidence,
        IReadOnlyList<string>? signals = null,
        long processingMs = 0) =>
        new()
        {
            Engine = "Risk",
            Status = EngineDataStatus.Known,
            Score = null,
            Confidence = Math.Clamp(confidence, 0, 100),
            BaseWeight = InstitutionalWeights.Risk,
            Bias = EngineMarketBias.Unknown,
            Signals = signals ?? [],
            DataSource = "risk-assessment",
            ProcessingMs = processingMs
        };
}

public sealed class InstitutionalConfluenceInput
{
    public string Asset { get; init; } = string.Empty;
    public IReadOnlyList<EngineAnalysisResult> Engines { get; init; } = [];
    public EngineAnalysisResult? Risk { get; init; }
    public decimal? LastPrice { get; init; }
    public decimal? DailyChangePercent { get; init; }
    public IReadOnlyList<string> BlockingFactors { get; init; } = [];
    public TradeRiskSuggestion? RiskSuggestion { get; init; }
    public MarketSessionContext? Session { get; init; }
    public MultiTimeframeConsensus? TimeframeConsensus { get; init; }
}

public sealed class InstitutionalRiskInput
{
    public string Asset { get; init; } = string.Empty;
    public int KnownEngineCount { get; init; }
    public int TotalEngineCount { get; init; }
    public bool HasHighImpactCalendarEvent { get; init; }
    public bool HasMediumImpactCalendarEvent { get; init; }
    public MacroLiquidityLevel Liquidity { get; init; } = MacroLiquidityLevel.Normal;
    public decimal? AtrPercent { get; init; }
    public bool IsLowLiquiditySession { get; init; }
    public decimal? SpreadPoints { get; init; }
    public bool HasTimeframeConflict { get; init; }
    public bool IsRanging { get; init; }
    public bool StructureUndefined { get; init; }
    public bool WeakVolume { get; init; }
}

/// <summary>Detecção SMC pontuada (Score/Weight/Confidence/Timestamp/Explanation).</summary>
public sealed class SmcDetection
{
    public string Type { get; init; } = string.Empty;
    public int Score { get; init; }
    public decimal Weight { get; init; }
    public decimal Confidence { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Bias { get; init; } = "Neutral";
    public string Explanation { get; init; } = string.Empty;
    public decimal? PriceLow { get; init; }
    public decimal? PriceHigh { get; init; }
}

public sealed class MarketSessionContext
{
    public string Session { get; init; } = "Unknown";
    public bool IsOverlap { get; init; }
    public bool IsLowLiquidity { get; init; }
    public string Description { get; init; } = string.Empty;
    public int ContextBarsUsed { get; init; }
    public string Regime { get; init; } = "Undefined";
    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed class MultiTimeframeConsensus
{
    public string Bias { get; init; } = "Sideways";
    public int AgreementPercent { get; init; }
    public bool HasConflict { get; init; }
    public IReadOnlyList<string> BullishTimeframes { get; init; } = [];
    public IReadOnlyList<string> BearishTimeframes { get; init; } = [];
    public IReadOnlyList<string> SidewaysTimeframes { get; init; } = [];
    public string Summary { get; init; } = string.Empty;
}

public sealed class AntiLossFilterResult
{
    public bool ShouldBlock { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = [];
    public decimal ConfidencePenalty { get; init; }
}

public sealed class AntiLossFilterInput
{
    public string Asset { get; init; } = string.Empty;
    public decimal? SpreadPoints { get; init; }
    public decimal? MaxAcceptableSpread { get; init; }
    public decimal? AtrPercent { get; init; }
    public bool IsRanging { get; init; }
    public bool HasTimeframeConflict { get; init; }
    public bool WeakVolume { get; init; }
    public bool StructureUndefined { get; init; }
    public bool IsLowLiquiditySession { get; init; }
    public bool HasHighImpactCalendar { get; init; }
    public int StructureScore { get; init; } = 50;
    public int VolumeScore { get; init; } = 50;
}

public enum MacroLiquidityLevel
{
    Low,
    Normal,
    High
}

public enum InstitutionalRiskLevel
{
    Low,
    Moderate,
    High,
    Extreme
}
