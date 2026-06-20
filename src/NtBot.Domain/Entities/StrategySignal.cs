using System.ComponentModel.DataAnnotations;

namespace NtBot.Domain.Entities;

public class StrategySignal
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(50)]
    public string StrategyName { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    public SignalType Type { get; set; }

    [Required]
    public StrategyDirection Direction { get; set; }

    [Required]
    public decimal Confidence { get; set; } // 0-100

    [Required]
    public DateTime Timestamp { get; set; }

    public decimal EntryPrice { get; set; }

    public decimal StopLoss { get; set; }

    public decimal TakeProfit { get; set; }

    public decimal Volume { get; set; }

    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    public bool IsExecuted { get; set; } = false;

    public DateTime? ExecutedAt { get; set; }

    [StringLength(100)]
    public string ExecutionId { get; set; } = string.Empty;

    // Additional metadata
    public Dictionary<string, string> Parameters { get; set; } = new();

    // Navigation
    public Tenant? Tenant { get; set; }
}

public enum SignalType
{
    Entry,
    Exit,
    Modify,
    Cancel
}

public enum StrategyDirection
{
    Buy,
    Sell,
    Hold
}