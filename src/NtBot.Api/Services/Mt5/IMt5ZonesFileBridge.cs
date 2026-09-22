using NtBot.TradingIntelligence.Engine;

namespace NtBot.Api.Services.Mt5;

public interface IMt5ZonesFileBridge
{
    /// <summary>Writes delim (+ header) for the given symbol/zones into Common Files.</summary>
    string? WriteZonesFile(
        string symbol,
        string timeframe,
        IReadOnlyList<Mt5ChartZoneMark> zones,
        DateTimeOffset updatedAt);

    /// <summary>Absolute directory currently used for writes (resolved).</summary>
    string ResolvedOutputDirectory { get; }
}
