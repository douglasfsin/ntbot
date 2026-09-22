namespace NtBot.Domain.Entities.Portfolio;

/// <summary>
/// Consentimento Open Finance (Fase 4 foundation).
/// Sem integração XP/BTG real no v1 — apenas persistência / UI shell.
/// </summary>
public class OpenFinanceConsent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ClientUserId { get; set; }

    public string Provider { get; set; } = string.Empty; // XP | BTG | Safra | Stub
    public string? ScopeJson { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Status { get; set; } = "Draft"; // Draft | Active | Revoked | Expired
    public string? AuditLogRef { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
