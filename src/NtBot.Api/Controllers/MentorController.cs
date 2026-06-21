using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NtBot.Mentor.Services;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/mentor")]
[Authorize]
public class MentorController : ControllerBase
{
    private readonly IMentorService _mentor;

    public MentorController(IMentorService mentor) => _mentor = mentor;

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantIdFromClaims();
        if (tenantId == Guid.Empty)
            return Unauthorized();

        return Ok(await _mentor.GetSnapshotAsync(tenantId, cancellationToken));
    }

    private Guid GetTenantIdFromClaims()
    {
        var claim = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claim, out var tenantId) ? tenantId : Guid.Empty;
    }
}
