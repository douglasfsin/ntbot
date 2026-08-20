namespace NtBot.Domain.Entities.Quant;

/// <summary>
/// Rolling correlation between two symbols that actually exist in Candles.
/// </summary>
public class QuantAssetCorrelation
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string SymbolA { get; set; } = string.Empty;
    public string SymbolB { get; set; } = string.Empty;
    public string Window { get; set; } = "15m";

    public decimal? Pearson { get; set; }
    public decimal? ReturnA { get; set; }
    public decimal? ReturnB { get; set; }
    public decimal? VolatilityA { get; set; }
    public decimal? VolatilityB { get; set; }
    public decimal? Beta { get; set; }
    public int SampleCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
