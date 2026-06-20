namespace NtBot.Domain.Entities;

public class ConnectorDownload
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ConnectorKeyId { get; set; }
    public Guid ConnectorVersionId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ConnectorKey ConnectorKey { get; set; } = null!;
    public ConnectorVersion ConnectorVersion { get; set; } = null!;
}
