using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NtBot.Macro.DTO;
using NtBot.Macro.Services;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/macro")]
[Authorize]
public class MacroController : ControllerBase
{
    private readonly IMacroIntelligenceService _macro;

    public MacroController(IMacroIntelligenceService macro)
    {
        _macro = macro;
    }

    [HttpGet("current")]
    public async Task<ActionResult<MacroSnapshot>> GetCurrent([FromQuery] string? symbol, CancellationToken cancellationToken)
    {
        var snapshot = await _macro.GetCurrentSnapshotAsync(symbol, cancellationToken);
        return Ok(snapshot);
    }

    [HttpGet("regime")]
    public async Task<ActionResult<object>> GetRegime([FromQuery] string? symbol, CancellationToken cancellationToken)
    {
        var snapshot = await _macro.GetCurrentSnapshotAsync(symbol, cancellationToken);
        return Ok(new
        {
            snapshot.Timestamp,
            snapshot.Liquidity,
            snapshot.DollarStrength,
            snapshot.Volatility,
            snapshot.InterestRate,
            snapshot.Inflation,
            snapshot.RiskSentiment,
            snapshot.MacroScore,
            snapshot.Confidence
        });
    }

    [HttpGet("recommendations")]
    public async Task<ActionResult<IReadOnlyList<MacroRecommendation>>> GetRecommendations(
        [FromQuery] string? symbol,
        CancellationToken cancellationToken)
    {
        var snapshot = await _macro.GetCurrentSnapshotAsync(symbol, cancellationToken);
        return Ok(snapshot.Recommendations);
    }

    [HttpGet("calendar")]
    public async Task<ActionResult<IReadOnlyList<MacroCalendarEventDto>>> GetCalendar(CancellationToken cancellationToken)
    {
        var events = await _macro.GetCalendarAsync(cancellationToken);
        return Ok(events);
    }

    [HttpGet("providers")]
    public async Task<ActionResult<IReadOnlyList<MacroProviderStatusDto>>> GetProviders(CancellationToken cancellationToken)
    {
        var providers = await _macro.GetProvidersAsync(cancellationToken);
        return Ok(providers);
    }

    [HttpPost("provider/{id:guid}/enable")]
    public async Task<IActionResult> EnableProvider(Guid id, CancellationToken cancellationToken)
    {
        await _macro.EnableProviderAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("provider/{id:guid}/disable")]
    public async Task<IActionResult> DisableProvider(Guid id, CancellationToken cancellationToken)
    {
        await _macro.DisableProviderAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("provider/{id:guid}/configure")]
    public async Task<IActionResult> ConfigureProvider(Guid id, [FromBody] MacroProviderConfigureRequest request, CancellationToken cancellationToken)
    {
        await _macro.ConfigureProviderAsync(id, request, cancellationToken);
        return NoContent();
    }
}
