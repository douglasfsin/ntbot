namespace NtBot.Domain.Entities;

public class BillingHistory
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubscriptionId { get; set; }
    public string? StripeInvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "open";
    public DateTime? PaidAt { get; set; }
    public string? InvoiceUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Subscription Subscription { get; set; } = null!;
}
