namespace NtBot.Domain.Entities;

public class ConnectorVersion
{
    public Guid Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Channel { get; set; } = ConnectorChannels.Stable;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string Sha256Hash { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public SubscriptionPlan MinPlan { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ConnectorDownload> Downloads { get; set; } = new List<ConnectorDownload>();
}

public static class ConnectorChannels
{
    public const string Stable = "stable";
    public const string Beta = "beta";
}
