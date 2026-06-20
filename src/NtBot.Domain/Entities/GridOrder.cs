using System.ComponentModel.DataAnnotations;

namespace NtBot.Domain.Entities;

public class GridOrder
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
    public GridType Type { get; set; }

    [Required]
    public decimal BasePrice { get; set; }

    [Required]
    public decimal StepSize { get; set; }

    [Required]
    public int MaxLevels { get; set; }

    public decimal LotSize { get; set; } = 0.01m;

    public bool UseMartingale { get; set; } = false;

    public decimal MartingaleMultiplier { get; set; } = 1.5m;

    public decimal ProfitTarget { get; set; }

    public decimal StopLossAmount { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    public bool IsClosed { get; set; } = false;

    public DateTime? ClosedAt { get; set; }

    public string? CloseReason { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeactivatedAt { get; set; }

    // Grid levels
    public List<GridLevel> Levels { get; set; } = new();

    // Navigation
    public Tenant? Tenant { get; set; }
}

public class GridLevel
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GridOrderId { get; set; }

    [Required]
    public int Level { get; set; }

    [Required]
    public decimal Price { get; set; }

    [Required]
    public decimal Volume { get; set; }

    public bool IsFilled { get; set; } = false;

    public bool IsClosed { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? FilledAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    [StringLength(50)]
    public string OrderId { get; set; } = string.Empty;

    public Guid? TradeExecutionId { get; set; }

    public TradeDirection Direction { get; set; }

    // Navigation
    public GridOrder? GridOrder { get; set; }
}

public enum GridType
{
    Fixed,
    Arithmetic,
    Geometric,
    Custom
}