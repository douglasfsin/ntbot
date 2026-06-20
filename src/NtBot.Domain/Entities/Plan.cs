namespace NtBot.Domain.Entities;

public class Plan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal? YearlyPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public string? StripePriceId { get; set; }
    public string? StripeProductId { get; set; }
    public int MaxStrategies { get; set; }
    public int MaxBrokers { get; set; }
    public int MaxTradingAccounts { get; set; }
    public int MaxActivePositions { get; set; }
    public string? FeaturesJson { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
