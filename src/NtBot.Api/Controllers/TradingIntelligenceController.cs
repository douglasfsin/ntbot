using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using NtBot.Api.Services.MarketData;
using NtBot.TradingIntelligence.Commands;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Models;
using NtBot.TradingIntelligence.Services;
using NtBot.Shared.MarketData;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/trading-intelligence")]
[Authorize]
public class TradingIntelligenceController : ControllerBase
{
    private readonly ITradingIntelligenceService _service;
    private readonly IMarketCandleService _candles;
    private readonly ISmcEngine _smc;
    private readonly IMediator _mediator;

    public TradingIntelligenceController(
        ITradingIntelligenceService service,
        IMarketCandleService candles,
        ISmcEngine smc,
        IMediator mediator)
    {
        _service = service;
        _candles = candles;
        _smc = smc;
        _mediator = mediator;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken) =>
        Ok(await _service.GetDashboardAsync(cancellationToken));

    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(_service.GetStatus());

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromQuery] string? symbol = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new RefreshTradingIntelligenceCommand(symbol), cancellationToken);
        return Ok(new { refreshed = result.Refreshed, snapshots = result.Snapshots });
    }

    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetSnapshot(string symbol, CancellationToken cancellationToken)
    {
        var snapshot = await _service.GetSnapshotAsync(symbol, cancellationToken: cancellationToken);
        return snapshot is null ? NotFound() : Ok(snapshot);
    }

    [HttpGet("{symbol}/candles")]
    public async Task<IActionResult> GetChartCandles(
        string symbol,
        [FromQuery] string timeframe = "60",
        [FromQuery] int count = 80,
        CancellationToken cancellationToken = default)
    {
        var result = await _candles.GetCandlesAsync(symbol, count, timeframe, cancellationToken);
        var candles = result.Candles;

        if (!result.HasSufficientData(5))
        {
            var snapshot = await _service.GetSnapshotAsync(symbol, cancellationToken: cancellationToken);
            var tfKey = ChartTimeframe.ToChartKey(timeframe);
            var analysis = snapshot?.TimeframeAnalyses
                .FirstOrDefault(t => t.Timeframe == tfKey || t.Timeframe == timeframe);

            var zones = snapshot?.OperationalZones ?? [];
            var priceLow = analysis?.Low ?? (zones.Count > 0 ? zones.Min(z => z.PriceLow) : 0);
            var priceHigh = analysis?.High ?? (zones.Count > 0 ? zones.Max(z => z.PriceHigh) : 0);

            if (priceLow > 0 || priceHigh > 0)
            {
                candles = SyntheticCandleBuilder.Build(
                    MarketCandleService.NormalizeSymbol(symbol),
                    timeframe,
                    priceLow > 0 ? priceLow : priceHigh * 0.99m,
                    priceHigh > 0 ? priceHigh : priceLow * 1.01m,
                    count);
                result = new CandleFetchResult { Candles = candles, Source = "synthetic" };
            }
        }

        if (!result.HasSufficientData(5))
            return NotFound(new { message = "Candles indisponíveis. Configure Quant:Mt5ApiUrl ou aguarde sync OHLCV." });

        var payload = candles
            .OrderBy(c => c.OpenTime)
            .Select(c => new ChartCandleDto
            {
                Time = new DateTimeOffset(c.OpenTime).ToUnixTimeSeconds(),
                Open = c.Open,
                High = c.High,
                Low = c.Low,
                Close = c.Close
            })
            .ToList();

        return Ok(new
        {
            symbol,
            timeframe = ChartTimeframe.ToChartKey(timeframe),
            source = result.Source,
            candles = payload
        });
    }

    [HttpGet("{symbol}/smc-overlays")]
    public async Task<IActionResult> GetSmcOverlays(
        string symbol,
        [FromQuery] string timeframe = "60",
        [FromQuery] int count = 120,
        CancellationToken cancellationToken = default)
    {
        var result = await _candles.GetCandlesAsync(symbol, count, timeframe, cancellationToken);
        if (!result.HasSufficientData(20))
            return NotFound(new { message = "Candles insuficientes para SMC." });

        var analysis = _smc.Analyze(result.Candles.OrderBy(c => c.OpenTime).ToList());
        var overlays = analysis.Overlays.Select(z => new SmcChartZoneDto
        {
            Type = z.Type,
            PriceLow = z.PriceLow,
            PriceHigh = z.PriceHigh,
            Label = z.Label
        }).ToList();

        return Ok(new
        {
            symbol,
            timeframe,
            score = analysis.Score,
            bias = analysis.Bias.ToString(),
            summary = analysis.Summary,
            overlays
        });
    }
}

public sealed class ChartCandleDto
{
    public long Time { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
}

[ApiController]
[Route("api/driver-compositions")]
[Authorize]
public class DriverCompositionController : ControllerBase
{
    private readonly IDriverCompositionAdminService _admin;

    public DriverCompositionController(IDriverCompositionAdminService admin) => _admin = admin;

    [HttpGet("{targetAsset}")]
    public async Task<IActionResult> List(string targetAsset, CancellationToken cancellationToken) =>
        Ok(await _admin.ListAsync(targetAsset, cancellationToken: cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DriverCompositionUpsertRequest request, CancellationToken cancellationToken)
    {
        var created = await _admin.CreateAsync(request, cancellationToken: cancellationToken);
        return created is null ? BadRequest() : Ok(created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DriverCompositionUpsertRequest request, CancellationToken cancellationToken)
    {
        var updated = await _admin.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await _admin.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("duplicate")]
    public async Task<IActionResult> Duplicate([FromBody] DuplicateCompositionRequest request, CancellationToken cancellationToken) =>
        Ok(new { copied = await _admin.DuplicateAsync(request.SourceAsset, request.TargetAsset, cancellationToken: cancellationToken) });

    [HttpGet("{targetAsset}/export")]
    public async Task<IActionResult> Export(string targetAsset, CancellationToken cancellationToken) =>
        Ok(await _admin.ExportAsync(targetAsset, cancellationToken: cancellationToken));

    [HttpPost("{targetAsset}/import")]
    public async Task<IActionResult> Import(string targetAsset, [FromBody] List<DriverCompositionUpsertRequest> items, CancellationToken cancellationToken) =>
        Ok(new { imported = await _admin.ImportAsync(targetAsset, items, cancellationToken: cancellationToken) });

    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderCompositionRequest request, CancellationToken cancellationToken)
    {
        await _admin.ReorderAsync(request.TargetAsset, request.OrderedIds, cancellationToken: cancellationToken);
        return Ok(new { reordered = true });
    }
}
