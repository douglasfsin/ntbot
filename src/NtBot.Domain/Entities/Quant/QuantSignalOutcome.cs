namespace NtBot.Domain.Entities.Quant;

public class QuantSignalOutcome
{
    public Guid Id { get; set; }
    public Guid SignalId { get; set; }
    public QuantSignalEvent? Signal { get; set; }

    public string Symbol { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public decimal SignalPrice { get; set; }

    public decimal? Return15s { get; set; }
    public decimal? Return30s { get; set; }
    public decimal? Return1m { get; set; }
    public decimal? Return5m { get; set; }
    public decimal? Return15m { get; set; }
    public decimal? Return30m { get; set; }
    public decimal? Return60m { get; set; }

    public decimal? ReturnPoints { get; set; }
    public decimal? ReturnPercent { get; set; }
    public decimal? ReturnR { get; set; }

    public decimal? Mfe15s { get; set; }
    public decimal? Mae15s { get; set; }
    public decimal? Mfe30s { get; set; }
    public decimal? Mae30s { get; set; }
    public decimal? Mfe1m { get; set; }
    public decimal? Mae1m { get; set; }
    public decimal? Mfe5m { get; set; }
    public decimal? Mae5m { get; set; }
    public decimal? Mfe15m { get; set; }
    public decimal? Mae15m { get; set; }
    public decimal? Mfe30m { get; set; }
    public decimal? Mae30m { get; set; }
    public decimal? Mfe60m { get; set; }
    public decimal? Mae60m { get; set; }

    public decimal? MaxPrice { get; set; }
    public decimal? MinPrice { get; set; }

    public bool? TargetHit { get; set; }
    public bool? StopHit { get; set; }
    public bool? Success { get; set; }
    public string? OutcomeClass { get; set; }
    public bool Complete { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
