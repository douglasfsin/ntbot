using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using NtBot.Api.Services.MarketData;
using NtBot.Api.Services.Redis;
using NtBot.TradingIntelligence.Cache;
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
    private readonly ITradingIntelligenceCacheService _tiCache;
    private readonly IMarketCandleService _candles;
    private readonly ISmcEngine _smc;
    private readonly ITradingEngineCacheService _engineCache;
    private readonly IMediator _mediator;
    private readonly IChartPriceService _prices;
    private readonly ICandleRedisStore _candleRedis;
    private readonly IRedisConnectionAccessor _redis;

    public TradingIntelligenceController(
        ITradingIntelligenceService service,
        ITradingIntelligenceCacheService tiCache,
        IMarketCandleService candles,
        ISmcEngine smc,
        ITradingEngineCacheService engineCache,
        IMediator mediator,
        IChartPriceService prices,
        ICandleRedisStore candleRedis,
        IRedisConnectionAccessor redis)
    {
        _service = service;
        _tiCache = tiCache;
        _candles = candles;
        _smc = smc;
        _engineCache = engineCache;
        _mediator = mediator;
        _prices = prices;
        _candleRedis = candleRedis;
        _redis = redis;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken) =>
        Ok(await _service.GetDashboardAsync(cancellationToken));

    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(_service.GetStatus());

    /// <summary>Diagnostics: Redis connectivity and candle key counts for a symbol.</summary>
    [HttpGet("{symbol}/candle-cache")]
    public async Task<IActionResult> GetCandleCacheStatus(
        string symbol,
        [FromQuery] string timeframe = "60",
        CancellationToken cancellationToken = default)
    {
        var normalized = MarketCandleService.NormalizeSymbol(symbol);
        var tf = ChartTimeframe.Normalize(timeframe);
        var redisUp = _redis.IsAvailable && _candleRedis.IsAvailable;

        IReadOnlyList<NtBot.Shared.MarketData.OhlcvBar> tfBars = [];
        IReadOnlyList<NtBot.Shared.MarketData.OhlcvBar> m1Bars = [];
        IReadOnlyList<NtBot.Shared.MarketData.OhlcvBar> aggregated = [];

        if (redisUp)
        {
            tfBars = await _candleRedis.GetTimeframeAsync(normalized, tf, 80, cancellationToken);
            m1Bars = await _candleRedis.GetM1Async(normalized, 100, cancellationToken);
            aggregated = await _candleRedis.GetAggregatedAsync(normalized, tf, 80, cancellationToken);
        }

        return Ok(new
        {
            symbol = normalized,
            timeframe = ChartTimeframe.ToChartKey(tf),
            redisConfigured = !string.IsNullOrWhiteSpace(_redis.InstancePrefix),
            redisConnected = redisUp,
            instancePrefix = _redis.InstancePrefix,
            timeframeBars = tfBars.Count,
            m1Bars = m1Bars.Count,
            aggregatedBars = aggregated.Count,
            lastTfClose = tfBars.Count > 0 ? tfBars[^1].Close : (decimal?)null,
            lastM1Close = m1Bars.Count > 0 ? m1Bars[^1].Close : (decimal?)null
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromQuery] string? symbol = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _mediator.Send(
                new RefreshTradingIntelligenceCommand(symbol), cancellationToken);
            return Ok(new { refreshed = result.Refreshed, snapshots = result.Snapshots });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client gone before/during refresh — not an unhandled server fault.
            return NoContent();
        }
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
        using var fetchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        fetchCts.CancelAfter(TimeSpan.FromSeconds(12));

        CandleFetchResult result;
        try
        {
            result = await _candles.GetCandlesAsync(symbol, count, timeframe, fetchCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new { message = "Candles timeout — tente novamente ou aguarde o SignalR." });
        }

        var candles = result.Candles;

        if (!result.HasSufficientData(5))
        {
            // Cache/last-known only — never block this endpoint on a cold TI build.
            var normalized = MarketCandleService.NormalizeSymbol(symbol);
            var snapshot = _tiCache.GetLastKnownSnapshot(normalized)
                ?? await _tiCache.GetSnapshotAsync(normalized, cancellationToken: cancellationToken);
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
            return NotFound(new { message = "Candles indisponíveis. Inicie a MarketData.API (ProfitDLL) ou configure MT5/OHLCV." });

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

    /// <summary>Last traded / mid price for chart real-time updates (Redis + connector + chart cache).</summary>
    [HttpGet("{symbol}/price")]
    public async Task<IActionResult> GetChartPrice(string symbol, CancellationToken cancellationToken = default)
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value;
        Guid? tenantId = Guid.TryParse(tenantClaim, out var tid) ? tid : null;
        var price = await _prices.GetPriceAsync(symbol, tenantId, cancellationToken);
        return price is null
            ? NotFound(new { message = "Preço indisponível para o símbolo." })
            : Ok(price);
    }

    [HttpGet("{symbol}/smc-overlays")]
    public async Task<IActionResult> GetSmcOverlays(
        string symbol,
        [FromQuery] string timeframe = "60",
        [FromQuery] int count = 120,
        CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(symbol);
        var tf = ChartTimeframe.ToChartKey(timeframe);
        var cacheKey = $"SMC:{tf}";

        var snapshot = await _service.GetSnapshotAsync(normalized, cancellationToken: cancellationToken);
        var fromSnapshot = snapshot?.SmcOverlays.FirstOrDefault(o => o.Timeframe == tf);
        if (fromSnapshot is not null && fromSnapshot.Overlays.Count > 0)
        {
            return Ok(new
            {
                symbol = normalized,
                timeframe = tf,
                score = fromSnapshot.Score,
                bias = fromSnapshot.Bias,
                summary = fromSnapshot.Summary,
                source = "snapshot-cache",
                overlays = fromSnapshot.Overlays
            });
        }

        var cached = _engineCache.Get<SmcAnalysisResult>(normalized, cacheKey);
        if (cached is not null)
        {
            return Ok(new
            {
                symbol = normalized,
                timeframe = tf,
                score = cached.Value.Score,
                bias = cached.Value.Bias.ToString(),
                summary = cached.Value.Summary,
                source = "engine-cache",
                overlays = cached.Value.Overlays.Select(z => new SmcChartZoneDto
                {
                    Type = z.Type,
                    PriceLow = z.PriceLow,
                    PriceHigh = z.PriceHigh,
                    Label = z.Label
                })
            });
        }

        var result = await _candles.GetCandlesAsync(normalized, count, tf, cancellationToken);
        if (!result.HasSufficientData(20))
            return NotFound(new { message = "Candles insuficientes para SMC." });

        var candles = result.Candles.OrderBy(c => c.OpenTime).ToList();
        var analysis = _smc.Analyze(candles);
        var lastCandle = candles.Count > 0 ? candles[^1].OpenTime : (DateTime?)null;
        _engineCache.Set(normalized, cacheKey, analysis, lastCandle, result.Source);

        var overlays = analysis.Overlays.Select(z => new SmcChartZoneDto
        {
            Type = z.Type,
            PriceLow = z.PriceLow,
            PriceHigh = z.PriceHigh,
            Label = z.Label
        }).ToList();

        return Ok(new
        {
            symbol = normalized,
            timeframe = tf,
            score = analysis.Score,
            bias = analysis.Bias.ToString(),
            summary = analysis.Summary,
            source = result.Source,
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
