namespace NtBot.Connector.Windows.Providers.Profit;

/// <summary>
/// Coordena DDE (primário) e RTD (fallback) para ticks do Profit.
/// Em ReplayMode o RTD live fica bloqueado para não sobrescrever o feed do gráfico em replay.
/// </summary>
public sealed class ProfitMarketDataCoordinator
{
    private long _lastDdeTickTicks;
    private long _lastRtdTickTicks;
    private int _replayMode;

    public TimeSpan DdeFallbackThreshold { get; set; } = TimeSpan.FromSeconds(5);

    public bool ReplayModeActive
    {
        get => Volatile.Read(ref _replayMode) == 1;
        set => Interlocked.Exchange(ref _replayMode, value ? 1 : 0);
    }

    public void RecordDdeTick() => Interlocked.Exchange(ref _lastDdeTickTicks, DateTime.UtcNow.Ticks);

    public void RecordRtdTick() => Interlocked.Exchange(ref _lastRtdTickTicks, DateTime.UtcNow.Ticks);

    public void RecordDllTick() => Interlocked.Exchange(ref _lastDllTickTicks, DateTime.UtcNow.Ticks);

    public DateTime? LastDdeTickUtc => TicksToUtc(_lastDdeTickTicks);

    public DateTime? LastRtdTickUtc => TicksToUtc(_lastRtdTickTicks);

    public DateTime? LastDllTickUtc => TicksToUtc(_lastDllTickTicks);

    public bool IsDdeActive(TimeSpan? threshold = null)
    {
        var last = LastDdeTickUtc;
        if (!last.HasValue)
            return false;

        var t = threshold ?? DdeFallbackThreshold;
        return DateTime.UtcNow - last.Value < t;
    }

    public bool ShouldPublishRtdFallback() =>
        AllowRtdFallback
        && !ReplayModeActive
        && !IsDdeActive();

    public string ActiveSource =>
        ReplayModeActive ? "DDE-REPLAY"
        : IsDdeActive() ? "DDE"
        : LastDllTickUtc.HasValue && DateTime.UtcNow - LastDllTickUtc.Value < TimeSpan.FromSeconds(5) ? "DLL"
        : LastRtdTickUtc.HasValue ? "RTD"
        : "none";

    /// <summary>Quando false, RTD nunca publica ticks (modo Rtd/DLL/Dde sem fallback).</summary>
    public bool AllowRtdFallback { get; set; }

    private long _lastDllTickTicks;

    private static DateTime? TicksToUtc(long ticks) =>
        ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : null;
}
