using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NtBot.Api.Hubs;
using NtBot.Api.Services.MarketData;
using NtBot.Api.Services.Mt5;
using NtBot.Shared.MarketData;
using NtBot.TradingIntelligence.Cache;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Services;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MT5Controller : ControllerBase
{
    private readonly IHubContext<TradingHub> _tradingHub;
    private readonly IHubContext<MarketHub> _marketHub;
    private readonly ILogger<MT5Controller> _logger;
    private readonly ITradingIntelligenceService _ti;
    private readonly ITradingIntelligenceCacheService _tiCache;
    private readonly IChartPriceService _prices;
    private readonly IMt5ZonesFileBridge _zonesFileBridge;

    public MT5Controller(
        IHubContext<TradingHub> tradingHub,
        IHubContext<MarketHub> marketHub,
        ILogger<MT5Controller> logger,
        ITradingIntelligenceService ti,
        ITradingIntelligenceCacheService tiCache,
        IChartPriceService prices,
        IMt5ZonesFileBridge zonesFileBridge)
    {
        _tradingHub = tradingHub;
        _marketHub = marketHub;
        _logger = logger;
        _ti = ti;
        _tiCache = tiCache;
        _prices = prices;
        _zonesFileBridge = zonesFileBridge;
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
                "/api/mt5/heartbeat",
                "/api/mt5/zones"
            }
        });
    }

    /// <summary>
    /// Compact operational-zone markings for MT5 chart objects (indicator NTBot_OperationalZones).
    /// No JWT — same open bridge pattern as connect/heartbeat (EA WebRequest).
    /// </summary>
    [HttpGet("zones")]
    [AllowAnonymous]
    public async Task<IActionResult> GetZones(
        [FromQuery] string symbol = "XAUUSD",
        [FromQuery] string timeframe = "60",
        [FromQuery] int max = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return BadRequest(new { message = "symbol required" });

        var normalized = CandleSymbolAliases.Canonical(symbol);

        // Prefer warm cache / last-known so MT5 WebRequest (≤25s) does not wait on a cold TI build.
        var snapshot = await _tiCache.GetSnapshotAsync(normalized, cancellationToken: cancellationToken)
                       ?? _tiCache.GetLastKnownSnapshot(normalized);
        if (snapshot is null)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(20));
            try
            {
                snapshot = await _ti.GetSnapshotAsync(normalized, cancellationToken: cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("MT5 zones: TI snapshot timeout for {Symbol}", normalized);
            }
        }

        if (snapshot is null)
            return NotFound(new { message = $"Snapshot TI indisponível para {normalized}." });

        decimal? lastPrice = null;
        try
        {
            var px = await _prices.GetPriceAsync(normalized, tenantId: null, cancellationToken);
            if (px?.Price > 0)
                lastPrice = px.Price;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Preço live indisponível para zonas MT5 {Symbol}", normalized);
        }

        var zones = Mt5ZoneMarkupBuilder.Build(
            normalized,
            snapshot,
            preferredTimeframe: ChartTimeframe.ToChartKey(timeframe),
            lastPrice: lastPrice,
            maxZones: max > 0 ? Math.Clamp(max, 1, 12) : null);

        var tfKey = ChartTimeframe.ToChartKey(timeframe);
        var lines = zones.Select(Mt5ZonesFileBridge.FormatDelimLine).ToList();
        var delim = string.Join('\n', lines);

        // Side-effect: dump for MT5 FileOpen fallback when WebRequest returns 4014.
        var filePath = _zonesFileBridge.WriteZonesFile(
            normalized, tfKey, zones, snapshot.Timestamp);

        return Ok(new
        {
            symbol = normalized,
            timeframe = tfKey,
            updatedAt = snapshot.Timestamp,
            count = zones.Count,
            zones,
            // One zone per line — easy WebRequest parse without a JSON library.
            delim,
            fileBridge = filePath is null
                ? null
                : new { path = filePath, directory = _zonesFileBridge.ResolvedOutputDirectory }
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