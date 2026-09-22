using NtBot.Connector.Windows.MarketData;
using NtBot.Connector.Windows.Providers.Profit;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.Workers;

/// <summary>
/// Emite heartbeat de diagnóstico do Replay DDE (WDO/WIN) a cada poucos segundos.
/// </summary>
public sealed class DdeReplayMonitorWorker : BackgroundService
{
    private readonly IDdeReplayController _replay;
    private readonly IMarketDataCache _cache;
    private readonly ProfitMarketDataCoordinator _coordinator;
    private readonly ILogger<DdeReplayMonitorWorker> _logger;

    public DdeReplayMonitorWorker(
        IDdeReplayController replay,
        IMarketDataCache cache,
        ProfitMarketDataCoordinator coordinator,
        ILogger<DdeReplayMonitorWorker> logger)
    {
        _replay = replay;
        _cache = cache;
        _coordinator = coordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);

        _logger.LogInformation(
            "[ReplayMonitor] iniciado — acompanhe Logs/dde-replay-*.log e ticks WDO/WIN");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                EmitSnapshot();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "[ReplayMonitor] falha no snapshot");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private void EmitSnapshot()
    {
        var status = _replay.GetStatus();
        var wdo = FindTick("WDO", "WDOFUT");
        var win = FindTick("WIN", "WINFUT");

        _logger.LogInformation(
            "[ReplayMonitor] ReplayMode={Replay} RtdBlocked={RtdBlocked} ActiveSource={Active} Connected={Connected} | WDO={WdoSummary} | WIN={WinSummary} | Msg={Message}",
            status.Enabled,
            _coordinator.ReplayModeActive,
            _coordinator.ActiveSource,
            status.IsConnected,
            FormatTick(wdo),
            FormatTick(win),
            status.Message);

        if (!status.Enabled)
            return;

        var lastDde = _coordinator.LastDdeTickUtc;
        var ddeAgeSec = lastDde.HasValue ? (DateTime.UtcNow - lastDde.Value).TotalSeconds : double.NaN;

        foreach (var (label, tick) in new[] { ("WDO", wdo), ("WIN", win) })
        {
            if (tick is null)
            {
                _logger.LogWarning(
                    "[ReplayMonitor] {Symbol} sem tick no cache com ReplayMode ON — DDE pode não estar lendo o contínuo",
                    label);
                continue;
            }

            var cacheAgeSec = (DateTime.UtcNow - tick.TimestampUtc).TotalSeconds;
            var source = tick.Source ?? string.Empty;
            var isReplaySource = source.StartsWith("DDE-REPLAY", StringComparison.OrdinalIgnoreCase)
                || source.StartsWith("DDE:", StringComparison.OrdinalIgnoreCase);
            var looksLikeRtdLive = source.Equals("Profit", StringComparison.OrdinalIgnoreCase)
                || source.Contains("RTD", StringComparison.OrdinalIgnoreCase);

            if (looksLikeRtdLive)
            {
                _logger.LogWarning(
                    "[ReplayMonitor] {Symbol} ainda com Source={Source} (live/RTD) — correção NÃO aplicada ou RTD não bloqueado",
                    label,
                    source);
            }
            else if (isReplaySource && !double.IsNaN(ddeAgeSec) && ddeAgeSec <= 5 && cacheAgeSec > 15)
            {
                _logger.LogWarning(
                    "[ReplayMonitor] {Symbol} Source={Source} ULT={Last} DDE vivo (hb={Hb:F1}s) mas preço parado {Age:F0}s — rode o replay no gráfico WDOFUT/WINFUT",
                    label,
                    source,
                    tick.LastPrice,
                    ddeAgeSec,
                    cacheAgeSec);
            }
            else if (isReplaySource && (double.IsNaN(ddeAgeSec) || ddeAgeSec > 15))
            {
                _logger.LogWarning(
                    "[ReplayMonitor] {Symbol} Source={Source} ULT={Last} DDE sem heartbeat ({Hb}) — sessão DDE fraca/desconectada",
                    label,
                    source,
                    tick.LastPrice,
                    double.IsNaN(ddeAgeSec) ? "n/a" : $"{ddeAgeSec:F0}s");
            }
            else if (isReplaySource)
            {
                _logger.LogInformation(
                    "[ReplayMonitor] {Symbol} OK Source={Source} ULT={Last} priceAge={Age:F1}s ddeHb={Hb:F1}s",
                    label,
                    source,
                    tick.LastPrice,
                    cacheAgeSec,
                    ddeAgeSec);
            }
        }
    }

    private MarketTick? FindTick(params string[] symbols)
    {
        MarketTick? best = null;
        foreach (var symbol in symbols)
        {
            var key = $"{BrokerSource.Profit}:{symbol}";
            if (!_cache.TryGet(key, out var tick))
                continue;

            if (best is null || tick.TimestampUtc > best.TimestampUtc)
                best = tick;
        }

        // fallback: scan snapshot
        if (best is null)
        {
            foreach (var tick in _cache.Snapshot())
            {
                if (tick.Provider != BrokerSource.Profit)
                    continue;
                if (!symbols.Any(s => tick.Symbol.Equals(s, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (best is null || tick.TimestampUtc > best.TimestampUtc)
                    best = tick;
            }
        }

        return best;
    }

    private static string FormatTick(MarketTick? tick)
    {
        if (tick is null)
            return "—";

        var age = (DateTime.UtcNow - tick.TimestampUtc).TotalSeconds;
        return $"{tick.Symbol} ULT={tick.LastPrice} Src={tick.Source} age={age:F1}s";
    }
}
