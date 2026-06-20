namespace NtBot.Billing.Options;

public class StripeSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string BackUrl { get; set; } = "http://localhost:5001";
}

public class SubscriptionSettings
{
    public int TrialDays { get; set; } = 7;
    public int GracePeriodDays { get; set; } = 3;
}
