using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NtBot.Billing.Dtos;
using NtBot.Billing.Services;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billing;
    private readonly ILogger<BillingController> _logger;

    public BillingController(IBillingService billing, ILogger<BillingController> logger)
    {
        _billing = billing;
        _logger = logger;
    }

    [HttpGet("config")]
    [AllowAnonymous]
    public async Task<ActionResult<BillingConfigDto>> GetConfig(CancellationToken ct) =>
        Ok(await _billing.GetConfigAsync(ct));

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<ActionResult<List<PlanDto>>> GetPlans(CancellationToken ct) =>
        Ok(await _billing.GetPlansAsync(ct));

    [HttpGet("subscription")]
    [Authorize]
    public async Task<ActionResult<SubscriptionDto>> GetSubscription(CancellationToken ct)
    {
        var tenantId = GetTenantIdFromClaims();
        if (tenantId == Guid.Empty)
            return Unauthorized(new { message = "Tenant não identificado." });

        var subscription = await _billing.GetTenantSubscriptionAsync(tenantId, ct);
        if (subscription == null)
            return NotFound(new { message = "Nenhuma assinatura encontrada." });

        return Ok(subscription);
    }

    [HttpPost("checkout")]
    [Authorize]
    public async Task<ActionResult<CheckoutResponse>> CreateCheckout(
        [FromBody] CheckoutRequest request,
        CancellationToken ct)
    {
        var tenantId = GetTenantIdFromClaims();
        if (tenantId == Guid.Empty)
            return Unauthorized(new { message = "Tenant não identificado." });

        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        var name = User.FindFirst(ClaimTypes.Name)?.Value;

        var result = await _billing.CreateCheckoutAsync(tenantId, email, name, request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("confirm")]
    [Authorize]
    public async Task<ActionResult<CheckoutResponse>> ConfirmCheckout(
        [FromBody] ConfirmCheckoutRequest request,
        CancellationToken ct)
    {
        var tenantId = GetTenantIdFromClaims();
        if (tenantId == Guid.Empty)
            return Unauthorized(new { message = "Tenant não identificado." });

        if (string.IsNullOrWhiteSpace(request.SessionId))
            return BadRequest(new { message = "session_id obrigatório." });

        var result = await _billing.ConfirmCheckoutSessionAsync(tenantId, request.SessionId, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("cancel")]
    [Authorize]
    public async Task<IActionResult> CancelSubscription(CancellationToken ct)
    {
        var tenantId = GetTenantIdFromClaims();
        if (tenantId == Guid.Empty)
            return Unauthorized(new { message = "Tenant não identificado." });

        var cancelled = await _billing.CancelSubscriptionAsync(tenantId, ct);
        if (!cancelled)
            return NotFound(new { message = "Nenhuma assinatura ativa para cancelar." });

        _logger.LogInformation("Assinatura cancelada pelo usuário | TenantId={TenantId}", tenantId);
        return Ok(new { success = true, message = "Assinatura será cancelada ao fim do período." });
    }

    private Guid GetTenantIdFromClaims()
    {
        var claim = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claim, out var tenantId) ? tenantId : Guid.Empty;
    }
}

public class ConfirmCheckoutRequest
{
    public string SessionId { get; set; } = string.Empty;
}
