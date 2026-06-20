using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.Connector.Dtos;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Connector.Services;

public interface IConnectorService
{
    Task<ConnectorKeyCreatedDto> GenerateKeyAsync(Guid tenantId, string? name = null, CancellationToken ct = default);
    Task<ConnectorKeyCreatedDto> RotateKeyAsync(Guid tenantId, Guid keyId, CancellationToken ct = default);
    Task<bool> RevokeKeyAsync(Guid tenantId, Guid keyId, CancellationToken ct = default);
    Task<ConnectorAuthResult> ValidateApiKeyAsync(string apiKey, string? ipAddress, CancellationToken ct = default);
    Task<ConnectorStatusDto> GetStatusAsync(Guid tenantId, CancellationToken ct = default);
    Task<ConnectorSessionDto> StartSessionAsync(Guid tenantId, Guid keyId, string version, string machineName, string? osVersion, string? ip, CancellationToken ct = default);
    Task HeartbeatAsync(Guid sessionId, string? ip, CancellationToken ct = default);
    Task DisconnectSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task LogAsync(Guid? tenantId, Guid? keyId, Guid? sessionId, string level, string message, string? payloadJson, string? ip, CancellationToken ct = default);
    Task<ConnectorVersionDto?> CheckVersionAsync(string currentVersion, SubscriptionPlan tenantPlan, CancellationToken ct = default);
    Task<ConnectorVersion?> ResolveDownloadAsync(Guid tenantId, Guid keyId, string version, string? ip, string? userAgent, CancellationToken ct = default);
}

public class ConnectorSettings
{
    public string KeyPrefix { get; set; } = "ntbot_live_";
    public int KeySecretLength { get; set; } = 32;
    public int DefaultKeyValidityDays { get; set; } = 365;
    public string DownloadsPath { get; set; } = "connector-downloads";
}

public class ConnectorService : IConnectorService
{
    private readonly NtBotDbContext _db;
    private readonly ConnectorSettings _settings;
    private readonly ILogger<ConnectorService> _logger;

    public ConnectorService(NtBotDbContext db, IOptions<ConnectorSettings> settings, ILogger<ConnectorService> logger)
    {
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ConnectorKeyCreatedDto> GenerateKeyAsync(Guid tenantId, string? name = null, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive, ct)
            ?? throw new InvalidOperationException("Tenant não encontrado.");

        var plainKey = GeneratePlainKey();
        var entity = new ConnectorKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name ?? "Windows Connector",
            KeyPrefix = plainKey[..Math.Min(20, plainKey.Length)],
            KeyHash = BCrypt.Net.BCrypt.HashPassword(plainKey),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.DefaultKeyValidityDays)
        };

        _db.ConnectorKeys.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Connector key created tenant={TenantId} key={KeyId}", tenantId, entity.Id);

        return new ConnectorKeyCreatedDto
        {
            KeyId = entity.Id,
            ApiKey = plainKey,
            KeyPrefix = entity.KeyPrefix,
            ExpiresAt = entity.ExpiresAt
        };
    }

    public async Task<ConnectorKeyCreatedDto> RotateKeyAsync(Guid tenantId, Guid keyId, CancellationToken ct = default)
    {
        var existing = await _db.ConnectorKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Chave não encontrada.");

        existing.IsActive = false;
        existing.RevokedAt = DateTime.UtcNow;

        var rotated = await GenerateKeyAsync(tenantId, existing.Name + " (rotated)", ct);
        var newEntity = await _db.ConnectorKeys.FirstAsync(k => k.Id == rotated.KeyId, ct);
        newEntity.RotatedFromKeyId = keyId;
        await _db.SaveChangesAsync(ct);

        return rotated;
    }

    public async Task<bool> RevokeKeyAsync(Guid tenantId, Guid keyId, CancellationToken ct = default)
    {
        var key = await _db.ConnectorKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.TenantId == tenantId, ct);
        if (key == null) return false;

        key.IsActive = false;
        key.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ConnectorAuthResult> ValidateApiKeyAsync(string apiKey, string? ipAddress, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || !apiKey.StartsWith(_settings.KeyPrefix, StringComparison.Ordinal))
            return Fail("Formato de ApiKey inválido.");

        var prefix = apiKey.Length >= 20 ? apiKey[..20] : apiKey;
        var candidates = await _db.ConnectorKeys
            .Include(k => k.Tenant)
            .Where(k => k.IsActive && k.KeyPrefix == prefix)
            .ToListAsync(ct);

        var key = candidates.FirstOrDefault(k => BCrypt.Net.BCrypt.Verify(apiKey, k.KeyHash));
        if (key == null)
            return Fail("ApiKey inválida ou revogada.");

        if (key.ExpiresAt.HasValue && key.ExpiresAt < DateTime.UtcNow)
            return Fail("ApiKey expirada.");

        if (!key.Tenant.IsActive)
            return Fail("Tenant inativo.");

        var licenseActive = IsLicenseActive(key.Tenant);

        key.LastUsedAt = DateTime.UtcNow;
        key.LastUsedIp = ipAddress;
        await _db.SaveChangesAsync(ct);

        return new ConnectorAuthResult
        {
            Success = true,
            TenantId = key.TenantId,
            KeyId = key.Id,
            Plan = key.Tenant.Plan.ToString(),
            LicenseActive = licenseActive
        };
    }

    public async Task<ConnectorStatusDto> GetStatusAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        var activeKey = await _db.ConnectorKeys.AsNoTracking()
            .Where(k => k.TenantId == tenantId && k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var session = await _db.ConnectorSessions.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.Status == ConnectorSessionStatuses.Connected)
            .OrderByDescending(s => s.LastHeartbeatAt)
            .FirstOrDefaultAsync(ct);

        ConnectorVersion? latestVersion = null;
        if (tenant != null)
        {
            latestVersion = await _db.ConnectorVersions.AsNoTracking()
                .Where(v => v.IsPublished && (int)v.MinPlan <= (int)tenant.Plan)
                .OrderByDescending(v => v.PublishedAt)
                .FirstOrDefaultAsync(ct);
        }

        return new ConnectorStatusDto
        {
            HasActiveKey = activeKey != null,
            KeyPrefix = activeKey?.KeyPrefix,
            KeyExpiresAt = activeKey?.ExpiresAt,
            LastUsedAt = activeKey?.LastUsedAt,
            LastUsedIp = activeKey?.LastUsedIp,
            IsConnected = session != null,
            LastConnectionAt = session?.LastHeartbeatAt ?? session?.ConnectedAt,
            ConnectedVersion = session?.ConnectorVersion,
            MachineName = session?.MachineName,
            Plan = tenant?.Plan.ToString() ?? "FREE",
            LicenseActive = tenant != null && IsLicenseActive(tenant),
            LatestVersion = latestVersion?.Version,
            ReleaseNotes = latestVersion?.ReleaseNotes,
            DownloadPath = latestVersion != null ? $"/api/connector/download/{latestVersion.Version}" : null,
            DownloadSizeBytes = latestVersion?.FileSizeBytes
        };
    }

    public async Task<ConnectorSessionDto> StartSessionAsync(
        Guid tenantId, Guid keyId, string version, string machineName, string? osVersion, string? ip, CancellationToken ct = default)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var session = new ConnectorSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConnectorKeyId = keyId,
            SessionToken = token,
            ConnectorVersion = version,
            MachineName = machineName,
            OsVersion = osVersion,
            IpAddress = ip,
            Status = ConnectorSessionStatuses.Connected,
            ConnectedAt = DateTime.UtcNow,
            LastHeartbeatAt = DateTime.UtcNow
        };

        _db.ConnectorSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return new ConnectorSessionDto { SessionId = session.Id, SessionToken = token };
    }

    public async Task HeartbeatAsync(Guid sessionId, string? ip, CancellationToken ct = default)
    {
        var session = await _db.ConnectorSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null) return;

        session.LastHeartbeatAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(ip))
            session.IpAddress = ip;

        await _db.SaveChangesAsync(ct);
    }

    public async Task DisconnectSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _db.ConnectorSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null) return;

        session.Status = ConnectorSessionStatuses.Disconnected;
        session.DisconnectedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task LogAsync(Guid? tenantId, Guid? keyId, Guid? sessionId, string level, string message, string? payloadJson, string? ip, CancellationToken ct = default)
    {
        _db.ConnectorLogs.Add(new ConnectorLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConnectorKeyId = keyId,
            ConnectorSessionId = sessionId,
            Level = level,
            Message = message,
            PayloadJson = payloadJson,
            IpAddress = ip,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ConnectorVersionDto?> CheckVersionAsync(string currentVersion, SubscriptionPlan tenantPlan, CancellationToken ct = default)
    {
        var latest = await _db.ConnectorVersions.AsNoTracking()
            .Where(v => v.IsPublished && (int)v.MinPlan <= (int)tenantPlan)
            .OrderByDescending(v => v.PublishedAt)
            .FirstOrDefaultAsync(ct);

        if (latest == null) return null;

        var isNewer = CompareSemVer(latest.Version, currentVersion) > 0;

        return new ConnectorVersionDto
        {
            Version = latest.Version,
            Channel = latest.Channel,
            ReleaseNotes = latest.ReleaseNotes,
            Sha256Hash = latest.Sha256Hash,
            FileSizeBytes = latest.FileSizeBytes,
            IsUpdateAvailable = isNewer,
            PublishedAt = latest.PublishedAt
        };
    }

    public async Task<ConnectorVersion?> ResolveDownloadAsync(
        Guid tenantId, Guid keyId, string version, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var auth = await _db.ConnectorKeys.Include(k => k.Tenant)
            .FirstOrDefaultAsync(k => k.Id == keyId && k.TenantId == tenantId && k.IsActive, ct);

        if (auth == null || !IsLicenseActive(auth.Tenant))
            return null;

        var connectorVersion = await _db.ConnectorVersions.FirstOrDefaultAsync(
            v => v.Version == version && v.IsPublished && (int)v.MinPlan <= (int)auth.Tenant.Plan, ct);

        if (connectorVersion == null)
            return null;

        _db.ConnectorDownloads.Add(new ConnectorDownload
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConnectorKeyId = keyId,
            ConnectorVersionId = connectorVersion.Id,
            IpAddress = ip,
            UserAgent = userAgent,
            DownloadedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        return connectorVersion;
    }

    private string GeneratePlainKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(_settings.KeySecretLength);
        var secret = Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").TrimEnd('=');
        return $"{_settings.KeyPrefix}{secret[..Math.Min(24, secret.Length)]}";
    }

    private static bool IsLicenseActive(Tenant tenant) =>
        tenant.IsActive && (!tenant.SubscriptionEnd.HasValue || tenant.SubscriptionEnd > DateTime.UtcNow);

    private static ConnectorAuthResult Fail(string error) => new() { Success = false, Error = error };

    private static int CompareSemVer(string a, string b)
    {
        static int[] Parse(string v) => v.Trim().Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var pa = Parse(a);
        var pb = Parse(b);
        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var da = i < pa.Length ? pa[i] : 0;
            var db = i < pb.Length ? pb[i] : 0;
            if (da != db) return da.CompareTo(db);
        }
        return 0;
    }
}
