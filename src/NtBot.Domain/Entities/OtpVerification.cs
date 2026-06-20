namespace NtBot.Domain.Entities;

public class OtpVerification
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string OtpCode { get; set; } = string.Empty;
    public string VerificationType { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public User? User { get; set; }
    public Tenant? Tenant { get; set; }

    public bool IsValid() => !IsUsed && ExpiresAt > DateTime.UtcNow;

    public void MarkAsUsed()
    {
        IsUsed = true;
        UsedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

public static class OtpVerificationTypes
{
    public const string ForgotPassword = "forgot_password";
    public const string Registration = "registration";
    public const string Login = "login";
}
