using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NtBot.Api.Services.Connector;
using NtBot.MarketIntelligence.Configuration;
using NtBot.MarketIntelligence.Services;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/market")]
[Authorize]
public class MarketController : ControllerBase
{
    private readonly IMarketIntelligenceService _market;
    private readonly ConnectorLiveMarketOverlay _mt5Overlay;

    public MarketController(
        IMarketIntelligenceService market,
        ConnectorLiveMarketOverlay mt5Overlay)
    {
        _market = market;
        _mt5Overlay = mt5Overlay;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var overview = await _market.GetOverviewAsync(cancellationToken);
        var tenantId = GetTenantId();
        if (tenantId != Guid.Empty)
            overview = _mt5Overlay.Apply(tenantId, overview);
        return Ok(overview);
    }

    [HttpGet("commodities")]
    public async Task<IActionResult> GetCommodities(CancellationToken cancellationToken) =>
        Ok(await _market.GetByCategoryAsync(MarketCategory.Commodity, cancellationToken));

    [HttpGet("indexes")]
    public async Task<IActionResult> GetIndexes(CancellationToken cancellationToken) =>
        Ok(await _market.GetByCategoryAsync(MarketCategory.Index, cancellationToken));

    [HttpGet("currencies")]
    public async Task<IActionResult> GetCurrencies(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var mt5 = tenantId != Guid.Empty
            ? _mt5Overlay.BuildMt5Currencies(tenantId)
            : [];

        if (mt5.Count > 0)
            return Ok(mt5);

        return Ok(await _market.GetByCategoryAsync(MarketCategory.Currency, cancellationToken));
    }

    [HttpGet("vix")]
    public async Task<IActionResult> GetVix(CancellationToken cancellationToken) =>
        Ok(await _market.GetVixAsync(cancellationToken));

    [HttpGet("treasury")]
    public async Task<IActionResult> GetTreasury(CancellationToken cancellationToken) =>
        Ok(await _market.GetByCategoryAsync(MarketCategory.Treasury, cancellationToken));

    [HttpGet("sectors")]
    public async Task<IActionResult> GetSectors(CancellationToken cancellationToken) =>
        Ok(await _market.GetByCategoryAsync(MarketCategory.Sector, cancellationToken));

    [HttpGet("correlation")]
    public async Task<IActionResult> GetCorrelation(CancellationToken cancellationToken) =>
        Ok(await _market.GetCorrelationAsync(cancellationToken));

    [HttpGet("quantscore")]
    public async Task<IActionResult> GetQuantScore(CancellationToken cancellationToken) =>
        Ok(await _market.GetQuantScoreAsync(cancellationToken));

    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders(CancellationToken cancellationToken) =>
        Ok(await _market.GetProvidersAsync(cancellationToken));

    [HttpPost("provider/{id:guid}/enable")]
    public async Task<IActionResult> EnableProvider(Guid id, CancellationToken cancellationToken)
    {
        await _market.EnableProviderAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("provider/{id:guid}/disable")]
    public async Task<IActionResult> DisableProvider(Guid id, CancellationToken cancellationToken)
    {
        await _market.DisableProviderAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("sync")]
    public async Task<IActionResult> ForceSync(CancellationToken cancellationToken)
    {
        await _market.ForceSyncAsync(cancellationToken);
        return Ok(new { synced = true, timestamp = DateTime.UtcNow });
    }

    private Guid GetTenantId()
    {
        var claim = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claim, out var tenantId) ? tenantId : Guid.Empty;
    }
}
