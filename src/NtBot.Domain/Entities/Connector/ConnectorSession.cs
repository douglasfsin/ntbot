namespace NtBot.Domain.Entities;

public class ConnectorSession
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ConnectorKeyId { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public string ConnectorVersion { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Status { get; set; } = ConnectorSessionStatuses.Connected;
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastHeartbeatAt { get; set; } = DateTime.UtcNow;
    public DateTime? DisconnectedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ConnectorKey ConnectorKey { get; set; } = null!;
    public ICollection<ConnectorLog> Logs { get; set; } = new List<ConnectorLog>();
}

public static class ConnectorSessionStatuses
{
    public const string Connected = "connected";
    public const string Disconnected = "disconnected";
}
