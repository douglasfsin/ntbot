using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NtBot.Analytics.Services;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/quant")]
[Authorize]
public sealed class QuantStatisticsController : ControllerBase
{
    private readonly IQuantQueryService _query;

    public QuantStatisticsController(IQuantQueryService query) => _query = query;

    [HttpGet("statistics")]
    public Task<QuantProbabilityResult> Statistics(
        [FromQuery] string? symbol,
        [FromQuery] string? strategy,
        [FromQuery] string? timeframe,
        [FromQuery] string? direction,
        [FromQuery] string? regime,
        [FromQuery] string? session,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
        => _query.ProbabilitiesAsync(new QuantProbabilityQuery
        {
            Symbol = symbol,
            Strategy = strategy,
            Timeframe = timeframe,
            Direction = direction,
            Regime = regime,
            Session = session,
            From = from,
            To = to
        }, ct);

    [HttpGet("statistics/signals")]
    public Task<QuantProbabilityResult> Signals(
        [FromQuery] string? symbol,
        [FromQuery] string? strategy,
        [FromQuery] string? direction,
        CancellationToken ct = default)
        => _query.ProbabilitiesAsync(new QuantProbabilityQuery { Symbol = symbol, Strategy = strategy, Direction = direction }, ct);

    [HttpGet("statistics/strategies")]
    public Task<IReadOnlyList<object>> Strategies([FromQuery] string? symbol, [FromQuery] string? strategy, CancellationToken ct = default)
        => _query.StrategiesAsync(new QuantProbabilityQuery { Symbol = symbol, Strategy = strategy }, ct);

    [HttpGet("statistics/regimes")]
    public Task<IReadOnlyList<object>> Regimes([FromQuery] string? symbol, [FromQuery] string? strategy, CancellationToken ct = default)
        => _query.RegimesAsync(new QuantProbabilityQuery { Symbol = symbol, Strategy = strategy }, ct);

    [HttpGet("statistics/opening-auction")]
    public Task<IReadOnlyList<object>> OpeningAuction([FromQuery] string? symbol, CancellationToken ct = default)
        => _query.OpeningAuctionAsync(symbol, ct);

    [HttpGet("statistics/time")]
    public Task<IReadOnlyList<object>> Time([FromQuery] string? symbol, CancellationToken ct = default)
        => _query.TimeAsync(new QuantProbabilityQuery { Symbol = symbol }, ct);

    [HttpGet("statistics/outcomes")]
    public Task<QuantProbabilityResult> Outcomes(
        [FromQuery] string? symbol,
        [FromQuery] string? strategy,
        [FromQuery] string horizon = "5m",
        CancellationToken ct = default)
        => _query.ProbabilitiesAsync(new QuantProbabilityQuery { Symbol = symbol, Strategy = strategy, Horizon = horizon }, ct);

    [HttpGet("probabilities")]
    public Task<QuantProbabilityResult> Probabilities(
        [FromQuery] string? symbol,
        [FromQuery] string? strategy,
        [FromQuery] string? timeframe,
        [FromQuery] string? direction,
        [FromQuery] string? regime,
        [FromQuery] string? session,
        [FromQuery] string? deltaBucket,
        [FromQuery] decimal? volumeZMin,
        [FromQuery] decimal? bookImbalanceMin,
        [FromQuery] string? priceVsVwap,
        [FromQuery] string? trend,
        [FromQuery] string? alignment,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string horizon = "5m",
        CancellationToken ct = default)
        => _query.ProbabilitiesAsync(new QuantProbabilityQuery
        {
            Symbol = symbol,
            Strategy = strategy,
            Timeframe = timeframe,
            Direction = direction,
            Regime = regime,
            Session = session,
            DeltaBucket = deltaBucket,
            VolumeZMin = volumeZMin,
            BookImbalanceMin = bookImbalanceMin,
            PriceVsVwap = priceVsVwap,
            Trend = trend,
            Alignment = alignment,
            Horizon = horizon,
            From = from,
            To = to
        }, ct);

    [HttpGet("ranking/strategies")]
    public Task<IReadOnlyList<object>> Ranking([FromQuery] string orderBy = "expectancy", CancellationToken ct = default)
        => _query.RankingAsync(orderBy, ct);

    [HttpGet("statistics/correlations")]
    public Task<IReadOnlyList<object>> Correlations(
        [FromQuery] string? symbol,
        [FromQuery] string? window,
        CancellationToken ct = default)
        => _query.CorrelationsAsync(symbol, window, ct);
}
