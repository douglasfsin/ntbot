using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NtBot.Api.Hubs;
using NtBot.Domain.Entities;
using System.Text.Json;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MT5Controller : ControllerBase
{
    private readonly IHubContext<TradingHub> _tradingHub;
    private readonly IHubContext<MarketHub> _marketHub;
    private readonly ILogger<MT5Controller> _logger;

    public MT5Controller(
        IHubContext<TradingHub> tradingHub,
        IHubContext<MarketHub> marketHub,
        ILogger<MT5Controller> logger)
    {
        _tradingHub = tradingHub;
        _marketHub = marketHub;
        _logger = logger;
    }

    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] MT5ConnectRequest request)
    {
        _logger.LogInformation("MT5 EA connected: {Symbol}", request.Symbol);

        await _tradingHub.Clients.All.SendAsync("MT5Connected", new
        {
            symbol = request.Symbol,
            timestamp = DateTime.UtcNow,
            status = "connected"
        });

        return Ok(new { status = "connected", timestamp = DateTime.UtcNow });
    }

    [HttpPost("update")]
    public async Task<IActionResult> Update([FromBody] MT5UpdateRequest request)
    {
        _logger.LogInformation("MT5 Update: {Type} - {Message}", request.Type, request.Message);

        // Route to appropriate hub based on type
        switch (request.Type.ToUpper())
        {
            case "TRADE":
            case "POSITION":
                await _tradingHub.Clients.All.SendAsync("MT5TradeUpdate", request);
                break;
            case "GRID":
                await _tradingHub.Clients.All.SendAsync("MT5GridUpdate", request);
                break;
            case "HEARTBEAT":
                await _tradingHub.Clients.All.SendAsync("MT5Heartbeat", request);
                break;
            default:
                await _tradingHub.Clients.All.SendAsync("MT5Message", request);
                break;
        }

        return Ok(new { received = true, timestamp = DateTime.UtcNow });
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] MT5HeartbeatRequest request)
    {
        _logger.LogDebug("MT5 Heartbeat from {Symbol}", request.Symbol);

        await _tradingHub.Clients.All.SendAsync("MT5Status", new
        {
            symbol = request.Symbol,
            status = request.Status,
            timestamp = DateTime.UtcNow
        });

        return Ok(new { status = "alive", timestamp = DateTime.UtcNow });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            status = "MT5 Bridge Active",
            timestamp = DateTime.UtcNow,
            endpoints = new[]
            {
                "/api/mt5/connect",
                "/api/mt5/update",
                "/api/mt5/heartbeat"
            }
        });
    }
}

// Request models
public class MT5ConnectRequest
{
    public string Symbol { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public class MT5UpdateRequest
{
    public string Type { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public class MT5HeartbeatRequest
{
    public string Symbol { get; set; } = "";
    public string Status { get; set; } = "alive";
    public DateTime Timestamp { get; set; }
}