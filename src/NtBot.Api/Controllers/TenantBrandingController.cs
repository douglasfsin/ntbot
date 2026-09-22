using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NtBot.Api.Services.WhiteLabel;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/tenants/branding")]
public sealed class TenantBrandingController : ControllerBase
{
    private readonly ITenantBrandingService _branding;
    private readonly ITenantFeatureService _features;

    public TenantBrandingController(ITenantBrandingService branding, ITenantFeatureService features)
    {
        _branding = branding;
        _features = features;
    }

    /// <summary>Público — resolve branding por slug para login branded.</summary>
    [HttpGet("by-slug/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        var dto = await _branding.GetBySlugAsync(slug, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();
        var dto = await _branding.GetForTenantAsync(tenantId, ct);
        var features = await _features.GetSnapshotAsync(tenantId, ct);
        return Ok(new { branding = dto, features });
    }

    [HttpPut("me")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> UpsertMine([FromBody] UpdateTenantBrandingRequest request, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();
        try
        {
            var dto = await _branding.UpsertAsync(tenantId, request, ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetTenantId()
    {
        var claim = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
