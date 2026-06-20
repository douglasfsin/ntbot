namespace NtBot.Connector.Dtos;

public class ConnectorKeyCreatedDto
{
    public Guid KeyId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public string Message { get; set; } = "Guarde esta chave — ela não será exibida novamente.";
}

public class ConnectorStatusDto
{
    public bool HasActiveKey { get; set; }
    public string? KeyPrefix { get; set; }
    public DateTime? KeyExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? LastUsedIp { get; set; }
    public bool IsConnected { get; set; }
    public DateTime? LastConnectionAt { get; set; }
    public string? ConnectedVersion { get; set; }
    public string? MachineName { get; set; }
    public string Plan { get; set; } = "FREE";
    public bool LicenseActive { get; set; }
    public string? LatestVersion { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? DownloadPath { get; set; }
    public long? DownloadSizeBytes { get; set; }
}

public class ConnectorVersionDto
{
    public string Version { get; set; } = string.Empty;
    public string Channel { get; set; } = "stable";
    public string? ReleaseNotes { get; set; }
    public string? Sha256Hash { get; set; }
    public long FileSizeBytes { get; set; }
    public bool IsUpdateAvailable { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public class ConnectorDownloadInfoDto
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string Sha256Hash { get; set; } = string.Empty;
    public string? ReleaseNotes { get; set; }
}

public class ConnectorAuthResult
{
    public bool Success { get; set; }
    public Guid TenantId { get; set; }
    public Guid KeyId { get; set; }
    public string Plan { get; set; } = "FREE";
    public bool LicenseActive { get; set; }
    public string? Error { get; set; }
}

public class ConnectorSessionDto
{
    public Guid SessionId { get; set; }
    public string SessionToken { get; set; } = string.Empty;
}
