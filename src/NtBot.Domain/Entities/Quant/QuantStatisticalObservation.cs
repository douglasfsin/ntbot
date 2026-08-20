namespace NtBot.Domain.Entities.Quant;

public class QuantStatisticalObservation
{
    public Guid Id { get; set; }

    public string Symbol { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public string Session { get; set; } = string.Empty;
    public string MarketRegime { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;

    public string FeatureGroup { get; set; } = string.Empty;
    public string FeatureName { get; set; } = string.Empty;
    public decimal? FeatureValue { get; set; }
    public string FeatureBucket { get; set; } = string.Empty;

    public int SampleCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string SampleClass { get; set; } = "INSUFFICIENT_SAMPLE";

    public decimal? SuccessProbability { get; set; }
    public decimal? ConfidenceLow { get; set; }
    public decimal? ConfidenceHigh { get; set; }
    public decimal ConfidenceLevel { get; set; } = 0.95m;

    public decimal? AverageReturn { get; set; }
    public decimal? MedianReturn { get; set; }
    public decimal? StdReturn { get; set; }
    public decimal? MinReturn { get; set; }
    public decimal? MaxReturn { get; set; }
    public decimal? P25Return { get; set; }
    public decimal? P50Return { get; set; }
    public decimal? P75Return { get; set; }
    public decimal? P90Return { get; set; }
    public decimal? P95Return { get; set; }

    public decimal? AverageMfe { get; set; }
    public decimal? AverageMae { get; set; }
    public decimal? AverageWin { get; set; }
    public decimal? AverageLoss { get; set; }
    public decimal? ProfitFactor { get; set; }
    public decimal? Expectancy { get; set; }
    public decimal? ExpectancyR { get; set; }
    public decimal? SharpeLike { get; set; }
    public decimal? SortinoLike { get; set; }
    public decimal? MaxDrawdown { get; set; }

    public string OutcomeHorizon { get; set; } = "5m";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
