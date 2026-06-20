using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NtBot.Domain.Entities;

public class DailyResult
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Required]
    [StringLength(10)]
    public string Broker { get; set; } = "MT5";

    // P&L
    public decimal GrossProfit { get; set; }

    public decimal GrossLoss { get; set; }

    [NotMapped]
    public decimal NetProfit => GrossProfit - GrossLoss;

    // Trading statistics
    public int TotalTrades { get; set; }

    public int WinningTrades { get; set; }

    [NotMapped]
    public int LosingTrades => TotalTrades - WinningTrades;

    public decimal WinRate() => TotalTrades > 0 ? (decimal)WinningTrades / TotalTrades * 100 : 0;

    public decimal TotalAverageWin() => WinningTrades > 0 ? GrossProfit / WinningTrades : 0;

    public decimal TotalAverageLoss() => LosingTrades > 0 ? GrossLoss / LosingTrades : 0;

    public decimal ProfitFactor() => GrossLoss > 0 ? GrossProfit / GrossLoss : 0;

    // Risk metrics
    public decimal MaxDrawdown { get; set; }

    public decimal MaxDrawdownPercentage { get; set; }

    // Exposure
    public decimal MaxExposure { get; set; }

    public decimal AverageExposure { get; set; }
    public decimal AverageLoss { get; set; }
    // Time metrics
    public TimeSpan TotalTradingTime { get; set; }

    // Commissions and fees
    public decimal TotalCommissions { get; set; }

    public decimal TotalSwaps { get; set; }

    // Navigation
    public Tenant? Tenant { get; set; }

    // Helper method to update from trades
    public void UpdateFromTrades(IEnumerable<TradeExecution> trades)
    {
        var dayTrades = trades.Where(t => t.ExecutionTime.Date == Date);

        TotalTrades = dayTrades.Count();
        WinningTrades = dayTrades.Count(t => t.Type.ToString().Contains("Buy") && t.Volume > 0); // Simplified

        GrossProfit = dayTrades.Where(t => t.Volume > 0).Sum(t => Math.Max(0, (decimal)t.Volume)); // Simplified
        GrossLoss = Math.Abs(dayTrades.Where(t => t.Volume < 0).Sum(t => (decimal)t.Volume)); // Simplified
    }
}