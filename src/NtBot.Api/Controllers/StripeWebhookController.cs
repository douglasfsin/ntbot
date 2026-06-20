using Microsoft.AspNetCore.Mvc;
using NtBot.Billing.Services;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public class StripeWebhookController : ControllerBase
{
    private readonly IBillingService _billing;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(IBillingService billing, ILogger<StripeWebhookController> logger)
    {
        _billing = billing;
        _logger = logger;
    }

    [HttpPost("stripe")]
    public async Task<IActionResult> HandleStripeWebhook(CancellationToken ct)
    {
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;

        string json;
        using (var reader = new StreamReader(HttpContext.Request.Body))
            json = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(json))
            return BadRequest(new { message = "Payload vazio." });

        var ok = await _billing.ProcessStripeWebhookAsync(json, signature, ct);
        if (!ok)
            return BadRequest(new { message = "Webhook não processado." });

        return Ok(new { received = true });
    }
}
