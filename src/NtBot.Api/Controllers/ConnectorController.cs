using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NtBot.Api.Filters;
using NtBot.Connector.Dtos;
using NtBot.Connector.Services;
using NtBot.Shared.Normalized;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/connector")]
public class ConnectorController : ControllerBase
{
    private readonly IConnectorService _connector;
    private readonly IConnectorIngestService _ingest;
    private readonly IConnectorLiveState _liveState;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ConnectorController> _logger;

    public ConnectorController(
        IConnectorService connector,
        IConnectorIngestService ingest,
        IConnectorLiveState liveState,
        IWebHostEnvironment env,
        ILogger<ConnectorController> logger)
    {
        _connector = connector;
        _ingest = ingest;
        _liveState = liveState;
        _env = env;
        _logger = logger;
    }

    [HttpGet("status")]
    [Authorize]
    public async Task<ActionResult<ConnectorStatusDto>> GetStatus(CancellationToken ct)
    {
        var tenantId = GetTenantIdFromClaims();
        if (tenantId == Guid.Empty) return Unauthorized();
        return Ok(await _connector.GetStatusAsync(tenantId, ct));
    }

    [HttpPost("keys")]
    [Authorize]
    public async Task<ActionResult<ConnectorKeyCreatedDto>> GenerateKey(CancellationToken ct)
    {
        var tenantId = GetTenantIdFromClaims();
        if (tenantId == Guid.Empty) return Unauthorized();
        return Ok(await _connector.GenerateKeyAsync(tenantId, ct: ct));
    }

    [HttpPost("keys/{keyId:guid}/rotate")]
    [Authorize]
    public async Task<ActionResult<ConnectorKeyCreatedDto>> RotateKey(Guid keyId, CancellationToken ct)
    {
        var tenantId = GetTenantIdFromClaims();
        if (tenantId == Guid.Empty) return Unauthorized();
        return Ok(await _connector.RotateKeyAsync(tenantId, keyId, ct));
    }

    [HttpDelete("keys/{keyId:guid}")]
    [Authorize]
    public async Task<IActionResult> RevokeKey(Guid keyId, CancellationToken ct)
    {
        var tenantId = GetTenantIdFromClaims();
        if (tenantId == Guid.Empty) return Unauthorized();
        if (!await _connector.RevokeKeyAsync(tenantId, keyId, ct)) return NotFound();
        return NoContent();
    }

    [HttpPost("session")]
    [ConnectorApiKey]
    public async Task<ActionResult<ConnectorSessionDto>> StartSession([FromBody] StartSessionRequest request, CancellationToken ct)
    {
        var tenantId = (Guid)HttpContext.Items[Filters.ConnectorApiKeyAttribute.HttpContextTenantIdKey]!;
        var keyId = (Guid)HttpContext.Items[Filters.ConnectorApiKeyAttribute.HttpContextKeyIdKey]!;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        return Ok(await _connector.StartSessionAsync(
            tenantId, keyId, request.Version, request.MachineName, request.OsVersion, ip, ct));
    }

    [HttpGet("live")]
    [Authorize]
    public ActionResult<ConnectorLiveSnapshot> GetLive()
    {
        var tenantId = GetTenantIdFromClaims();
        if (tenantId == Guid.Empty) return Unauthorized();

        var snapshot = _liveState.GetSnapshot(tenantId);
        if (snapshot == null)
            return Ok(new ConnectorLiveSnapshot());

        return Ok(snapshot);
    }

    [HttpPost("ingest")]
    [ConnectorApiKey]
    public async Task<IActionResult> Ingest([FromBody] NormalizedIngestBatch batch, CancellationToken ct)
    {
        var apiKey = Request.Headers["X-Connector-Api-Key"].FirstOrDefault() ?? string.Empty;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _ingest.IngestAsync(apiKey, batch, ip, ct);
        if (!result.Success) return Unauthorized(new { message = result.Error });
        return Ok(new { received = true, tenantId = result.TenantId });
    }

    [HttpGet("version")]
    [ConnectorApiKey]
    public async Task<ActionResult<ConnectorVersionDto>> CheckVersion([FromQuery] string current, CancellationToken ct)
    {
        var tenantId = (Guid)HttpContext.Items[Filters.ConnectorApiKeyAttribute.HttpContextTenantIdKey]!;
        var auth = await _connector.ValidateApiKeyAsync(
            Request.Headers["X-Connector-Api-Key"].FirstOrDefault() ?? string.Empty,
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct);

        if (!auth.Success) return Unauthorized();

        var plan = Enum.Parse<NtBot.Domain.Entities.SubscriptionPlan>(auth.Plan, true);
        var version = await _connector.CheckVersionAsync(current, plan, ct);
        if (version == null) return NotFound();
        return Ok(version);
    }

    [HttpGet("download/{version}")]
    [ConnectorApiKey]
    public async Task<IActionResult> Download(string version, CancellationToken ct)
    {
        var tenantId = (Guid)HttpContext.Items[Filters.ConnectorApiKeyAttribute.HttpContextTenantIdKey]!;
        var keyId = (Guid)HttpContext.Items[Filters.ConnectorApiKeyAttribute.HttpContextKeyIdKey]!;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();

        var artifact = await _connector.ResolveDownloadAsync(tenantId, keyId, version, ip, ua, ct);
        if (artifact == null) return Forbid();

        var path = Path.Combine(_env.ContentRootPath, "connector-downloads", artifact.FileName);
        if (!System.IO.File.Exists(path))
        {
            _logger.LogWarning("Connector package missing: {Path}", path);
            return NotFound(new { message = "Pacote não disponível no servidor." });
        }

        return PhysicalFile(path, "application/octet-stream", artifact.FileName);
    }

    private Guid GetTenantIdFromClaims()
    {
        var claim = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claim, out var tenantId) ? tenantId : Guid.Empty;
    }
}

public class StartSessionRequest
{
    public string Version { get; set; } = "1.0.0";
    public string MachineName { get; set; } = Environment.MachineName;
    public string? OsVersion { get; set; }
}
