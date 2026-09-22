using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NtBot.Api.Services.Boletagem;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/boletagem")]
[Authorize]
public sealed class BoletagemController : ControllerBase
{
    private readonly IBoletagemService _service;
    private readonly ILogger<BoletagemController> _logger;

    public BoletagemController(IBoletagemService service, ILogger<BoletagemController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("strategies")]
    public IActionResult Strategies() => Ok(_service.ListStrategies());

    [HttpGet("{symbol}")]
    public async Task<IActionResult> Get(string symbol, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();

        var session = await _service.GetActiveAsync(tenantId, symbol, ct);
        return Ok(session ?? new BoletaSessionDto { Symbol = symbol.ToUpperInvariant() });
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertBoletaRequest request, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();

        try
        {
            var session = await _service.UpsertSessionAsync(tenantId, request, ct);
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao salvar boleta");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] UpsertBoletaRequest request, CancellationToken ct)
    {
        var plan = await _service.PreviewPlanAsync(request, ct);
        return Ok(plan);
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] ExecuteBoletaRequest request, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();

        try
        {
            var session = await _service.ExecuteAsync(tenantId, request, ct);
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao executar boleta");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{symbol}/close")]
    public async Task<IActionResult> Close(string symbol, [FromQuery] string? reason, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();

        try
        {
            var session = await _service.CloseAllAsync(
                tenantId, symbol, reason ?? "Fechamento manual", ct);
            return Ok(session);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{symbol}/monitor")]
    public async Task<IActionResult> Monitor(string symbol, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();

        var session = await _service.MonitorAsync(tenantId, symbol, ct);
        return Ok(session);
    }

    private Guid GetTenantId()
    {
        var claim = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
