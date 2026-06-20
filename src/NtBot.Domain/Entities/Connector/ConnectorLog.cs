namespace NtBot.Domain.Entities;

public class ConnectorLog
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? ConnectorKeyId { get; set; }
    public Guid? ConnectorSessionId { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public ConnectorKey? ConnectorKey { get; set; }
    public ConnectorSession? ConnectorSession { get; set; }
}
