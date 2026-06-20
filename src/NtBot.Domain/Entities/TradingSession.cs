using System.ComponentModel.DataAnnotations;

namespace NtBot.Domain.Entities;

public class TradingSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(50)]
    public string SessionName { get; set; } = string.Empty;

    [Required]
    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Required]
    [StringLength(10)]
    public string Broker { get; set; } = "MT5";

    // Session configuration
    public List<string> Symbols { get; set; } = new();

    public List<string> Strategies { get; set; } = new();

    // Mapped property so EF can create indexes on it
    public bool IsActive { get; set; } = true;

    // Statistics
    public int TotalTrades { get; set; }

    public decimal TotalVolume { get; set; }

    public decimal GrossProfit { get; set; }

    public decimal GrossLoss { get; set; }

    public decimal NetProfit() => GrossProfit - GrossLoss;

    public decimal MaxDrawdown { get; set; }

    // Risk metrics
    public decimal MaxRiskPerTrade { get; set; }

    public decimal MaxDailyLoss { get; set; }

    // Session settings
    public bool EnableRiskManagement { get; set; } = true;

    public bool EnableGridTrading { get; set; } = false;

    public bool EnableScalping { get; set; } = false;

    // Navigation
    public Tenant? Tenant { get; set; }

    public List<TradeExecution> Executions { get; set; } = new();

    // Methods
    public void Start()
    {
        StartTime = DateTime.UtcNow;
        EndTime = null;
        IsActive = true;
    }

    public void Stop()
    {
        EndTime = DateTime.UtcNow;
        IsActive = false;
    }

    public void AddTrade(TradeExecution execution)
    {
        Executions.Add(execution);
        TotalTrades++;
        TotalVolume += execution.Volume;

        if (execution.Volume > 0)
            GrossProfit += execution.Volume; // Simplified
        else
            GrossLoss += Math.Abs(execution.Volume);
    }
}