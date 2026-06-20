using System.ComponentModel.DataAnnotations;

namespace NtBot.Domain.Entities;

public class TradePosition
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(20)]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string Broker { get; set; } = "MT5"; // MT5, NinjaTrader, etc.

    [Required]
    public PositionType Type { get; set; }

    [Required]
    public decimal Volume { get; set; }

    [Required]
    public decimal OpenPrice { get; set; }

    public decimal? ClosePrice { get; set; }

    public decimal? StopLoss { get; set; }

    public decimal? TakeProfit { get; set; }

    public decimal CurrentProfit { get; set; }

    public decimal Swap { get; set; }

    public decimal Commission { get; set; }

    [Required]
    public DateTime OpenTime { get; set; }

    public DateTime? CloseTime { get; set; }

    [StringLength(50)]
    public string Comment { get; set; } = string.Empty;

    [StringLength(50)]
    public string MagicNumber { get; set; } = string.Empty;

    public bool IsOpen() => CloseTime == null;

    public decimal NetProfit() => CurrentProfit + Swap - Commission;

    // Navigation properties
    public Tenant? Tenant { get; set; }
}

public enum PositionType
{
    Buy,
    Sell
}