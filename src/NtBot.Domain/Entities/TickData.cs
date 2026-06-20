using System.ComponentModel.DataAnnotations;

namespace NtBot.Domain.Entities;

public class TickData
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(20)]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string Source { get; set; } = "MT5";

    [Required]
    public decimal Bid { get; set; }

    [Required]
    public decimal Ask { get; set; }

    public int Spread { get; set; }

    [Required]
    public DateTime Timestamp { get; set; }

    public decimal MidPrice => (Bid + Ask) / 2;

    // For high-frequency storage optimization
    public long TimestampTicks => Timestamp.Ticks;
}