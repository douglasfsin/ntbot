namespace NtBot.Billing.Dtos;

public class PlanDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal? YearlyPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int MaxStrategies { get; set; }
    public int MaxBrokers { get; set; }
    public int MaxActivePositions { get; set; }
    public bool IsFree => MonthlyPrice <= 0;
}

public class SubscriptionDto
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public string PlanSlug { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? TrialEnd { get; set; }
    public bool IsActive { get; set; }
}

public class CheckoutRequest
{
    public string PlanSlug { get; set; } = string.Empty;
}

public class CheckoutResponse
{
    public bool Success { get; set; }
    public string? CheckoutUrl { get; set; }
    public Guid? SubscriptionId { get; set; }
    public string? Message { get; set; }
}

public class BillingConfigDto
{
    public bool StripeConfigured { get; set; }
    public string? PublishableKey { get; set; }
    public bool TestMode { get; set; }
}
