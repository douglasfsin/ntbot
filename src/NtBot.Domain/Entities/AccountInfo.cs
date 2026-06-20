using System.ComponentModel.DataAnnotations;

namespace NtBot.Domain.Entities;

public class AccountInfo
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(10)]
    public string Broker { get; set; } = "MT5";

    [Required]
    [StringLength(50)]
    public string AccountNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string AccountCurrency { get; set; } = "USD";

    // Balance information
    public decimal Balance { get; set; }

    public decimal Equity { get; set; }

    public decimal Margin { get; set; }

    public decimal FreeMargin { get; set; }

    public decimal MarginLevel { get; set; }

    // Daily P&L
    public decimal DailyProfit { get; set; }

    public decimal DailyLoss { get; set; }

    public decimal DailyNetProfit => DailyProfit - DailyLoss;

    // Account type
    public AccountType Type { get; set; } = AccountType.Demo;

    // Leverage
    public int Leverage { get; set; } = 100;

    // Trading permissions
    public bool AllowTrading { get; set; } = true;

    public bool AllowExpertAdvisors { get; set; } = true;

    // Last update
    [Required]
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant? Tenant { get; set; }

    // Calculated properties
    public decimal UsedMarginPercentage => Margin > 0 ? (Margin / Equity) * 100 : 0;

    public bool IsMarginCall => MarginLevel > 0 && MarginLevel < 100;

    public decimal Drawdown => Balance > 0 ? ((Balance - Equity) / Balance) * 100 : 0;
}

public enum AccountType
{
    Demo,
    Real
}