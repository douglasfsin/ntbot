using System.ComponentModel.DataAnnotations;

namespace NtBot.Domain.Entities;

public class TradeExecution
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
    public string Broker { get; set; } = "MT5";

    [Required]
    public ExecutionType Type { get; set; }

    [Required]
    public decimal Volume { get; set; }

    [Required]
    public decimal Price { get; set; }

    public decimal? StopLoss { get; set; }

    public decimal? TakeProfit { get; set; }

    [Required]
    public DateTime ExecutionTime { get; set; }

    [StringLength(100)]
    public string Comment { get; set; } = string.Empty;

    [StringLength(50)]
    public string OrderId { get; set; } = string.Empty;

    [StringLength(50)]
    public string Ticket { get; set; } = string.Empty;

    // Navigation properties
    public Tenant? Tenant { get; set; }
}

public enum ExecutionType
{
    MarketBuy,
    MarketSell,
    LimitBuy,
    LimitSell,
    StopBuy,
    StopSell,
    ClosePosition
}