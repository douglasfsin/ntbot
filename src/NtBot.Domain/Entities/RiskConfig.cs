using System.ComponentModel.DataAnnotations;

namespace NtBot.Domain.Entities;

public class RiskConfig
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(20)]
    public string Symbol { get; set; } = string.Empty;

    // Daily limits
    public decimal DailyLossLimit { get; set; } = 1000;

    public decimal DailyProfitTarget { get; set; } = 500;

    public int MaxDailyTrades { get; set; } = 50;

    // Position limits
    public decimal MaxPositionSize { get; set; } = 1.0m;

    public decimal MaxExposure { get; set; } = 10.0m;

    // Risk per trade
    public decimal MaxRiskPerTrade { get; set; } = 50; // In account currency

    public decimal MaxRiskPercentage { get; set; } = 2.0m; // % of account

    // Drawdown limits
    public decimal MaxDrawdown { get; set; } = 1000;

    public decimal MaxDrawdownPercentage { get; set; } = 10.0m;

    // Time restrictions
    public TimeSpan TradingStartTime { get; set; } = new TimeSpan(9, 0, 0); // 9:00

    public TimeSpan TradingEndTime { get; set; } = new TimeSpan(17, 0, 0); // 17:00

    public bool AllowWeekendTrading { get; set; } = false;

    // Volatility filters
    public decimal MaxSpread { get; set; } = 50; // Max spread in points

    public bool CheckVolatility { get; set; } = true;

    // Correlation limits
    public decimal MaxCorrelationExposure { get; set; } = 5.0m;

    // Navigation
    public Tenant? Tenant { get; set; }
}