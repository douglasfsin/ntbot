using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NtBot.Api.Hubs;
using System.Text.Json;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketDataController : ControllerBase
{
    private readonly IHubContext<MarketHub> _marketHub;
    private readonly ILogger<MarketDataController> _logger;

    public MarketDataController(
        IHubContext<MarketHub> marketHub,
        ILogger<MarketDataController> logger)
    {
        _marketHub = marketHub;
        _logger = logger;
    }

    [HttpPost("tick")]
    public async Task<IActionResult> ReceiveTick([FromBody] TickData tick)
    {
        _logger.LogDebug("Tick received: {Source} {Symbol} {Bid}/{Ask}", tick.Source, tick.Symbol, tick.Bid, tick.Ask);

        // Broadcast to all subscribers of this symbol
        await _marketHub.Clients.Group($"ticks_{tick.Symbol}").SendAsync("TickUpdate", tick);

        // Broadcast to all subscribers of this source
        await _marketHub.Clients.Group($"market_{tick.Source}").SendAsync("MarketTick", tick);

        // Also broadcast to general market updates
        await _marketHub.Clients.All.SendAsync("MarketUpdate", tick);

        return Ok(new { received = true, timestamp = DateTime.UtcNow });
    }

    [HttpPost("ticks")]
    public async Task<IActionResult> ReceiveTicks([FromBody] TickData[] ticks)
    {
        _logger.LogInformation("Bulk ticks received: {Count} ticks", ticks.Length);

        foreach (var tick in ticks)
        {
            await _marketHub.Clients.Group($"ticks_{tick.Symbol}").SendAsync("TickUpdate", tick);
            await _marketHub.Clients.Group($"market_{tick.Source}").SendAsync("MarketTick", tick);
        }

        await _marketHub.Clients.All.SendAsync("BulkMarketUpdate", ticks);

        return Ok(new { received = ticks.Length, timestamp = DateTime.UtcNow });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            status = "Market Data Service Active",
            timestamp = DateTime.UtcNow,
            supportedSources = new[] { "MT5", "NinjaTrader", "ProfitChart", "Simulator" }
        });
    }
}

// DTO for tick data
public class TickData
{
    public string Source { get; set; } = "";
    public string Symbol { get; set; } = "";
    public double Bid { get; set; }
    public double Ask { get; set; }
    public int Spread { get; set; }
    public DateTime Timestamp { get; set; }
}