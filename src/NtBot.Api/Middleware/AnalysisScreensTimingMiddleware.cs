using System.Diagnostics;

namespace NtBot.Api.Middleware;

/// <summary>
/// Structured timing for REST endpoints used by WIN/WDO/XAUUSD analysis screens.
/// Logs path, optional symbol, duration_ms, and status — no per-candle noise.
/// </summary>
public sealed class AnalysisScreensTimingMiddleware
{
    private static readonly PathString[] PrefixedPaths =
    [
        new("/api/trading-intelligence"),
        new("/api/market-drivers"),
        new("/api/macro"),
        new("/api/boletagem"),
        new("/api/health"),
        new("/api/ProfitChart"),
        new("/api/profitchart"),
        new("/api/connector")
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<AnalysisScreensTimingMiddleware> _logger;

    public AnalysisScreensTimingMiddleware(RequestDelegate next, ILogger<AnalysisScreensTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldTime(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var path = context.Request.Path.Value ?? "";
            var symbol = TryExtractSymbol(path);
            var status = context.Response.StatusCode;
            var durationMs = sw.ElapsedMilliseconds;

            if (durationMs >= 2000)
            {
                _logger.LogWarning(
                    "AnalysisEndpointSlow path={Path} symbol={Symbol} duration_ms={DurationMs} status={Status}",
                    path, symbol ?? "-", durationMs, status);
            }
            else
            {
                _logger.LogInformation(
                    "AnalysisEndpoint path={Path} symbol={Symbol} duration_ms={DurationMs} status={Status}",
                    path, symbol ?? "-", durationMs, status);
            }
        }
    }

    private static bool ShouldTime(PathString path)
    {
        foreach (var prefix in PrefixedPaths)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts symbol from paths like /api/trading-intelligence/WIN/candles or /api/boletagem/WDO.
    /// </summary>
    internal static string? TryExtractSymbol(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3)
            return null;

        // api / {area} / {symbol} [/...]
        var candidate = segments[2];
        if (IsReservedSegment(candidate))
            return null;

        // Skip query-like or numeric-only noise
        if (candidate.All(char.IsDigit))
            return null;

        return candidate.ToUpperInvariant();
    }

    private static bool IsReservedSegment(string segment) =>
        segment.Equals("dashboard", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("status", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("refresh", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("sync", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("current", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("regime", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("recommendations", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("calendar", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("providers", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("strategies", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("tickers", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("dde-replay", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("preview", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("execute", StringComparison.OrdinalIgnoreCase);
}
