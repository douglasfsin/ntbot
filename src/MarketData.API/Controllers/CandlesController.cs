using MarketData.API.Services;
using Marketdata.Database.Services;
using Microsoft.AspNetCore.Mvc;

namespace MarketData.API.Controllers;

[ApiController]
[Route("api/candles")]
public class CandlesController : ControllerBase
{
    private readonly IMarketDataHistoryProvider _historyProvider;
    private readonly ILogger<CandlesController> _logger;

    public CandlesController(
        IMarketDataHistoryProvider historyProvider,
        ILogger<CandlesController> logger)
    {
        _historyProvider = historyProvider;
        _logger = logger;
    }

    /// <summary>
    /// Retorna candles OHLCV agregados a partir dos trades históricos do ProfitDLL.
    /// </summary>
    [HttpGet("{ticker}")]
    public async Task<IActionResult> GetCandles(
        string ticker,
        [FromQuery] int timeframe = 5,
        [FromQuery] int count = 80,
        [FromQuery] DateTime? date = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return BadRequest("Ticker obrigatório.");

        var targetDate = (date ?? DateTime.Today).Date;
        if (targetDate > DateTime.Today)
            targetDate = DateTime.Today;

        var profitTicker = ResolveProfitTicker(ticker);
        var lookbackDays = EstimateLookbackDays(profitTicker, timeframe, count);
        var startDate = SubtractBusinessDays(targetDate, lookbackDays);

        try
        {
            var trades = await _historyProvider.GetHistoricalTradesAsync(profitTicker, startDate, targetDate, cancellationToken);
            if (trades.Count == 0)
            {
                return Ok(new CandleResponse
                {
                    Ticker = ticker,
                    TimeframeMinutes = timeframe,
                    Source = "profitdll-empty",
                    Candles = []
                });
            }

            var candles = TradeToCandleAggregator.Aggregate(trades, timeframe, count);
            return Ok(new CandleResponse
            {
                Ticker = ticker,
                TimeframeMinutes = timeframe,
                Source = "profitdll",
                Candles = candles
            });
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException)
        {
            _logger.LogWarning("Candles cancelados para {Ticker}", profitTicker);
            return Ok(new CandleResponse
            {
                Ticker = ticker,
                TimeframeMinutes = timeframe,
                Source = "profitdll-cancelled",
                Candles = []
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao obter candles para {Ticker}", profitTicker);
            return Ok(new CandleResponse
            {
                Ticker = ticker,
                TimeframeMinutes = timeframe,
                Source = "profitdll-error",
                Candles = []
            });
        }
    }

    private static DateTime SubtractBusinessDays(DateTime date, int businessDays)
    {
        var current = date.Date;
        var remaining = Math.Max(businessDays, 1);
        while (remaining > 0)
        {
            current = current.AddDays(-1);
            if (current.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                remaining--;
        }

        return current;
    }

    private static string ResolveProfitTicker(string ticker)
    {
        var t = ticker.Trim().ToUpperInvariant();
        return t switch
        {
            "WIN" => "WINFUT",
            "WDO" => "WDOFUT",
            "DOL" => "DOLFUT",
            "IND" => "INDFUT",
            _ => t
        };
    }

    /// <summary>
    /// Estima dias úteis de histórico necessários para montar <paramref name="count"/> candles.
    /// </summary>
    private static int EstimateLookbackDays(string ticker, int timeframeMinutes, int count)
    {
        const int sessionMinutes = 540; // ~9h pregão B3
        var totalMinutes = Math.Max(timeframeMinutes, 1) * Math.Max(count, 1);
        var sessions = (int)Math.Ceiling(totalMinutes / (double)sessionMinutes);
        return Math.Clamp(sessions + 1, 1, IsEquityTicker(ticker) ? 8 : 20);
    }

    private static bool IsEquityTicker(string ticker)
    {
        var t = ticker.Trim().ToUpperInvariant();
        return t.Length >= 5 && (t.EndsWith('4') || t.EndsWith('3'));
    }
}

public sealed class CandleResponse
{
    public string Ticker { get; set; } = string.Empty;
    public int TimeframeMinutes { get; set; }
    public string Source { get; set; } = string.Empty;
    public List<CandleDto> Candles { get; set; } = [];
}

public sealed class CandleDto
{
    public DateTime Time { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}
