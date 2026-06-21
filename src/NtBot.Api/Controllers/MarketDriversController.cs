using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NtBot.MarketDrivers.Services;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/market-drivers")]
[Authorize]
public class MarketDriversController : ControllerBase
{
    private readonly IMarketDriversService _drivers;

    public MarketDriversController(IMarketDriversService drivers) => _drivers = drivers;

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken) =>
        Ok(await _drivers.GetDashboardAsync(cancellationToken));

    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetSnapshot(string symbol, CancellationToken cancellationToken)
    {
        var snapshot = await _drivers.GetSnapshotAsync(symbol, cancellationToken);
        return snapshot is null ? NotFound() : Ok(snapshot);
    }

    [HttpGet("{symbol}/heatmap")]
    public async Task<IActionResult> GetHeatMap(string symbol, CancellationToken cancellationToken)
    {
        var snapshot = await _drivers.GetSnapshotAsync(symbol, cancellationToken);
        return snapshot is null ? NotFound() : Ok(snapshot.HeatMap);
    }

    [HttpGet("{symbol}/explanation")]
    public async Task<IActionResult> GetExplanation(string symbol, CancellationToken cancellationToken)
    {
        var snapshot = await _drivers.GetSnapshotAsync(symbol, cancellationToken);
        return snapshot is null
            ? NotFound()
            : Ok(new { snapshot.Explanation, snapshot.Score, snapshot.AiSummary });
    }

    [HttpPost("sync")]
    public async Task<IActionResult> ForceSync(CancellationToken cancellationToken)
    {
        await _drivers.ForceRefreshAsync(cancellationToken);
        return Ok(new { synced = true, timestamp = DateTime.UtcNow });
    }
}
