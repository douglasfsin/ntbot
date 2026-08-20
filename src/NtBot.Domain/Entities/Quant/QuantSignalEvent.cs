namespace NtBot.Domain.Entities.Quant;

public class QuantSignalEvent
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;

    public decimal Price { get; set; }
    public decimal? StopPrice { get; set; }
    public decimal? TargetPrice { get; set; }

    public decimal? Score { get; set; }
    public decimal? Confidence { get; set; }

    public int? TrendScore { get; set; }
    public int? VolumeScore { get; set; }
    public int? DeltaScore { get; set; }
    public int? AggressionScore { get; set; }
    public int? BookScore { get; set; }
    public int? VwapScore { get; set; }
    public int? VolatilityScore { get; set; }
    public int? AuctionScore { get; set; }
    public int? MtfScore { get; set; }
    public int? CorrelationScore { get; set; }

    public string? MarketRegime { get; set; }
    public string Session { get; set; } = string.Empty;
    public string? TraceId { get; set; }
    public Guid? FeatureId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public QuantSignalOutcome? Outcome { get; set; }
}
