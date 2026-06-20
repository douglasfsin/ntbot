namespace NtBot.Domain.Entities;

public class Subscription
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PlanId { get; set; }
    public string Status { get; set; } = SubscriptionStatuses.Active;
    public string PaymentStatus { get; set; } = PaymentStatuses.Pending;
    public string PaymentGateway { get; set; } = "stripe";
    public string? StripeSubscriptionId { get; set; }
    public string? StripeCustomerId { get; set; }
    public string? StripeCheckoutSessionId { get; set; }
    public decimal MonthlyPrice { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? TrialEnd { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public DateTime? NextPaymentDate { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public bool AutoRenew { get; set; } = true;
    public int GracePeriodDays { get; set; } = 3;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Plan Plan { get; set; } = null!;
    public ICollection<BillingHistory> BillingHistory { get; set; } = new List<BillingHistory>();

    public bool IsActive() =>
        Status == SubscriptionStatuses.Active
        && (PaymentStatus == PaymentStatuses.Paid || PaymentStatus == PaymentStatuses.Trialing);
}

public static class SubscriptionStatuses
{
    public const string Active = "active";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";
    public const string Suspended = "suspended";
    public const string Trialing = "trialing";
}

public static class PaymentStatuses
{
    public const string Pending = "pending";
    public const string Paid = "paid";
    public const string Trialing = "trialing";
    public const string Overdue = "overdue";
    public const string Cancelled = "cancelled";
}
