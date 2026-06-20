namespace NtBot.Domain.Entities;

public class ConnectorKey
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? LastUsedIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RotatedFromKeyId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ConnectorKey? RotatedFromKey { get; set; }
    public ICollection<ConnectorSession> Sessions { get; set; } = new List<ConnectorSession>();
    public ICollection<ConnectorLog> Logs { get; set; } = new List<ConnectorLog>();
    public ICollection<ConnectorDownload> Downloads { get; set; } = new List<ConnectorDownload>();
}
