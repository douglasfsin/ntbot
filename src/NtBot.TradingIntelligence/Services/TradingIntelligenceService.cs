using System.Collections.Concurrent;
using System.Diagnostics;
using NtBot.Shared.MarketData;
using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Engine.Consensus;
using NtBot.TradingIntelligence.Engine.Context;
using NtBot.TradingIntelligence.Engine.Filters;
using NtBot.TradingIntelligence.Engine.Liquidity;
using NtBot.TradingIntelligence.Engine.Risk;
using NtBot.TradingIntelligence.Engine.Structure;
using NtBot.TradingIntelligence.Engine.Volatility;
using NtBot.TradingIntelligence.Engine.Volume;
using NtBot.TradingIntelligence.Models;
using NtBot.TradingIntelligence.Cache;
using NtBot.MarketDrivers.Services;
using NtBot.Macro.Services;
using NtBot.MarketIntelligence.Services;

namespace NtBot.TradingIntelligence.Services;

public interface ITradingIntelligenceUpdateNotifier
{
    Task NotifySnapshotUpdatedAsync(TradingIntelligenceSnapshot snapshot, CancellationToken cancellationToken = default);
}

public interface ITradingIntelligenceService
{
    Task<TradingIntelligenceSnapshot?> GetSnapshotAsync(string asset, Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task<TradingIntelligenceSnapshot?> RefreshSnapshotAsync(string asset, Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TradingIntelligenceSnapshot>> RefreshAllAsync(Guid? tenantId = null, bool notifyClients = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TradingIntelligenceDashboardItem>> GetDashboardAsync(CancellationToken cancellationToken = default);
    TradingIntelligenceStatus GetStatus();
}

public sealed class TradingIntelligenceService : ITradingIntelligenceService
{
    private static readonly ConcurrentDictionary<string, Task<TradingIntelligenceSnapshot?>> InflightBuilds = new(StringComparer.OrdinalIgnoreCase);

    private readonly IMarketDriversService _drivers;
    private readonly IMacroIntelligenceService _macro;
    private readonly IMarketIntelligenceService _market;
    private readonly IConfluenceEngine _confluence;
    private readonly IOperationalZoneEngine _zones;
    private readonly IWyckoffScoreProvider _wyckoff;
    private readonly ISmcScoreProvider _smc;
    private readonly IVolumeScoreProvider _volume;
    private readonly ITrendEngine _trend;
    private readonly IMomentumEngine _momentum;
    private readonly IRiskEngine _risk;
    private readonly ISmcEngine _smcEngine;
    private readonly IWyckoffEngine _wyckoffEngine;
    private readonly IMarketContextEngine _context;
    private readonly ILiquidityEngine _liquidity;
    private readonly IVolumeAnalysisEngine _volumeEngine;
    private readonly IVolatilityEngine _volatility;
    private readonly IAntiLossFilter _antiLoss;
    private readonly IMultiTimeframeConsensusEngine _mtfConsensus;
    private readonly ITradeRiskPlanner _riskPlanner;
    private readonly ITradingEngineCacheService _engineCache;
    private readonly ITradingCandleSource? _candles;
    private readonly IN8nAiProvider? _ai;
    private readonly ITradingIntelligenceCacheService _cache;
    private readonly ITradingIntelligenceUpdateNotifier? _notifier;
    private readonly IOptions<TradingIntelligenceOptions> _options;
    private readonly ILogger<TradingIntelligenceService> _logger;

    public TradingIntelligenceService(
        IMarketDriversService drivers,
        IMacroIntelligenceService macro,
        IMarketIntelligenceService market,
        IConfluenceEngine confluence,
        IOperationalZoneEngine zones,
        IWyckoffScoreProvider wyckoff,
        ISmcScoreProvider smc,
        IVolumeScoreProvider volume,
        ITrendEngine trend,
        IMomentumEngine momentum,
        IRiskEngine risk,
        ISmcEngine smcEngine,
        IWyckoffEngine wyckoffEngine,
        IMarketContextEngine context,
        ILiquidityEngine liquidity,
        IVolumeAnalysisEngine volumeEngine,
        IVolatilityEngine volatility,
        IAntiLossFilter antiLoss,
        IMultiTimeframeConsensusEngine mtfConsensus,
        ITradeRiskPlanner riskPlanner,
        ITradingEngineCacheService engineCache,
        ITradingIntelligenceCacheService cache,
        IOptions<TradingIntelligenceOptions> options,
        ILogger<TradingIntelligenceService> logger,
        ITradingCandleSource? candles = null,
        IN8nAiProvider? ai = null,
        ITradingIntelligenceUpdateNotifier? notifier = null)
    {
        _drivers = drivers;
        _macro = macro;
        _market = market;
        _confluence = confluence;
        _zones = zones;
        _wyckoff = wyckoff;
        _smc = smc;
        _volume = volume;
        _trend = trend;
        _momentum = momentum;
        _risk = risk;
        _smcEngine = smcEngine;
        _wyckoffEngine = wyckoffEngine;
        _context = context;
        _liquidity = liquidity;
        _volumeEngine = volumeEngine;
        _volatility = volatility;
        _antiLoss = antiLoss;
        _mtfConsensus = mtfConsensus;
        _riskPlanner = riskPlanner;
        _engineCache = engineCache;
        _candles = candles;
        _cache = cache;
        _ai = ai;
        _notifier = notifier;
        _options = options;
        _logger = logger;
    }

    public async Task<TradingIntelligenceSnapshot?> RefreshSnapshotAsync(
        string asset,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(asset);
        await _cache.RemoveSnapshotAsync(normalized, tenantId, cancellationToken);
        _engineCache.InvalidateAsset(normalized);
        await _drivers.ForceRefreshAsync(cancellationToken);
        return await BuildSnapshotAsync(normalized, tenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<TradingIntelligenceSnapshot>> RefreshAllAsync(
        Guid? tenantId = null,
        bool notifyClients = true,
        CancellationToken cancellationToken = default)
    {
        var snapshots = new List<TradingIntelligenceSnapshot>();
        foreach (var asset in _options.Value.SupportedAssets)
        {
            var snapshot = await RefreshSnapshotAsync(asset, tenantId, cancellationToken);
            if (snapshot is null) continue;
            snapshots.Add(snapshot);
            if (notifyClients && _notifier is not null)
            {
                try
                {
                    // Snapshot is already built — do not fail refresh if hub write is canceled.
                    await _notifier.NotifySnapshotUpdatedAsync(snapshot, CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                    // Client disconnect / aborted SignalR write after successful refresh.
                }
            }
        }

        return snapshots;
    }

    public async Task<TradingIntelligenceSnapshot?> GetSnapshotAsync(
        string asset,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(asset);

        var cached = await _cache.GetSnapshotAsync(normalized, tenantId, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("TI span asset={Asset} span=cache_hit status=ok", normalized);
            return cached;
        }

        TradingIntelligenceSnapshot? snapshot;
        try
        {
            snapshot = await BuildSnapshotAsync(normalized, tenantId, cancellationToken)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // HTTP client timed out — do not abort the shared inflight build (hub/other callers).
            return _cache.GetLastKnownSnapshot(normalized, tenantId);
        }
        if (snapshot is not null)
            return snapshot;

        var lastKnown = _cache.GetLastKnownSnapshot(normalized, tenantId);
        if (lastKnown is not null)
            return lastKnown;

        // Ativo suportado nunca deve “sumir” da UI — devolve snapshot degradado.
        if (IsSupportedAsset(normalized))
            return CreateDegradedSnapshot(normalized, "Análise temporariamente indisponível — dados de mercado/drivers em carregamento.");

        return null;
    }

    private Task<TradingIntelligenceSnapshot?> BuildSnapshotAsync(
        string normalized,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var key = $"{normalized}|{tenantId?.ToString("N") ?? "global"}";

        while (true)
        {
            if (InflightBuilds.TryGetValue(key, out var running))
                return running;

            var task = BuildSnapshotCoreAsync(normalized, tenantId, CancellationToken.None);
            if (InflightBuilds.TryAdd(key, task))
            {
                return AwaitAndCleanupBuildAsync(key, task);
            }
        }
    }

    private static async Task<TradingIntelligenceSnapshot?> AwaitAndCleanupBuildAsync(
        string key,
        Task<TradingIntelligenceSnapshot?> task)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            InflightBuilds.TryRemove(key, out _);
        }
    }

    private async Task<TradingIntelligenceSnapshot?> BuildSnapshotCoreAsync(
        string normalized,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var buildSw = Stopwatch.StartNew();
        try
        {
            // Per-build cache — avoids Clear() races when InflightBuilds run for multiple assets.
            var candleRequestCache = new Dictionary<string, CandleFetchBundle>(StringComparer.OrdinalIgnoreCase);

            // Critical path: drivers + macro + correlation are independent — run in parallel with soft timeouts
            // so one slow provider does not block the whole TI snapshot.
            var driversTask = SoftAwaitAsync(
                () => _drivers.GetSnapshotAsync(normalized, cancellationToken),
                TimeSpan.FromSeconds(18),
                cancellationToken,
                onTimeoutOrError: () => (MarketDrivers.Models.MarketDriversSnapshot?)null);

            var macroTask = SoftAwaitAsync(
                () => _macro.GetCurrentSnapshotAsync(normalized, cancellationToken),
                TimeSpan.FromSeconds(10),
                cancellationToken,
                onTimeoutOrError: () => new Macro.DTO.MacroSnapshot());

            var correlationTask = SoftAwaitAsync(
                () => _market.GetCorrelationAsync(cancellationToken),
                TimeSpan.FromSeconds(8),
                cancellationToken,
                onTimeoutOrError: () => new MarketIntelligence.Models.CorrelationResult { Timestamp = DateTime.UtcNow });

            await Task.WhenAll(driversTask, macroTask, correlationTask).ConfigureAwait(false);

            var driverSnapshot = await driversTask.ConfigureAwait(false)
                ?? CreateEmptyDriversSnapshot(normalized);
            var macro = await macroTask.ConfigureAwait(false);
            var marketCorrelation = await correlationTask.ConfigureAwait(false);

            _logger.LogInformation(
                "TI span asset={Asset} span=deps_parallel duration_ms={DurationMs} drivers={HasDrivers}",
                normalized, buildSw.ElapsedMilliseconds, driverSnapshot.Drivers.Count > 0);

            var assetImpact = marketCorrelation.AssetImpacts.FirstOrDefault(a =>
                string.Equals(a.Asset, normalized, StringComparison.OrdinalIgnoreCase));

            var analysisTimeframes = ResolveAnalysisTimeframes(normalized);
            var tfSw = Stopwatch.StartNew();
            IReadOnlyList<TimeframeAnalysis> timeframes;
            try
            {
                timeframes = await _wyckoff.GetTimeframeAnalysesAsync(normalized, analysisTimeframes, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Timeframes indisponíveis para TI {Asset}", normalized);
                timeframes = [];
            }
            _logger.LogInformation(
                "TI span asset={Asset} span=timeframes duration_ms={DurationMs} count={Count}",
                normalized, tfSw.ElapsedMilliseconds, timeframes.Count);

            var mtfConsensus = _mtfConsensus.Evaluate(timeframes);

            var wyckoff = timeframes.Count > 0
                ? AggregateTimeframeEngine("Wyckoff", timeframes.Select(t => t.WyckoffScore), InstitutionalWeights.Wyckoff, "multi-tf")
                : await _wyckoff.GetAnalysisAsync(normalized, "60", cancellationToken);

            var smc = timeframes.Count > 0
                ? AggregateTimeframeEngine("SMC", timeframes.Select(t => t.SmcScore), InstitutionalWeights.Smc, "multi-tf")
                : await _smc.GetAnalysisAsync(normalized, "60", cancellationToken);

            EngineAnalysisResult trend;
            EngineAnalysisResult volume;
            EngineAnalysisResult liquidity;
            EngineAnalysisResult volatility;
            MarketSessionContext? session = null;
            WyckoffEngineResult? wyckoffDetail = null;
            SmcAnalysisResult? smcDetail = null;
            decimal? dailyChange = null;
            decimal? lastPrice = null;
            decimal? atrPercent = null;
            IReadOnlyList<NtBot.Domain.Entities.Candle> primaryCandles = [];

            if (_candles is not null)
            {
                var h1 = await GetCandlesCachedAsync(candleRequestCache, normalized, "60", 120, cancellationToken);
                primaryCandles = h1.Candles;
                var lastCandleTime = h1.Candles.Count > 0
                    ? h1.Candles.Max(c => c.OpenTime)
                    : (DateTime?)null;

                trend = ResolveCachedEngine(
                    normalized, "Trend", lastCandleTime,
                    () => _trend.Analyze(normalized, h1.Candles),
                    h1.Source);

                volume = ResolveCachedEngine(
                    normalized, "Volume", lastCandleTime,
                    () => _volumeEngine.Analyze(normalized, h1.Candles),
                    h1.Source);

                liquidity = ResolveCachedEngine(
                    normalized, "Liquidity", lastCandleTime,
                    () => _liquidity.Analyze(normalized, h1.Candles),
                    h1.Source);

                volatility = ResolveCachedEngine(
                    normalized, "Volatility", lastCandleTime,
                    () => _volatility.Analyze(normalized, h1.Candles),
                    h1.Source);

                atrPercent = _volatility.GetAtrPercent(h1.Candles);
                session = _context.Analyze(normalized, h1.Candles);
                wyckoffDetail = ResolveWyckoffCached(normalized, "60", h1);
                smcDetail = ResolveSmcCached(normalized, "60", h1);

                if (h1.Candles.Count >= 2)
                {
                    var ordered = h1.Candles.OrderBy(c => c.OpenTime).ToList();
                    var first = ordered[0];
                    var last = ordered[^1];
                    lastPrice = last.Close;
                    if (first.Open > 0)
                        dailyChange = (last.Close - first.Open) / first.Open * 100m;
                }
            }
            else
            {
                trend = EngineAnalysisResult.Unknown("Trend", InstitutionalWeights.Trend, "Fonte de candles não configurada.");
                volume = timeframes.Count > 0
                    ? AggregateTimeframeEngine("Volume", timeframes.Select(t => t.VolumeScore), InstitutionalWeights.Volume, "multi-tf")
                    : await _volume.GetAnalysisAsync(normalized, "60", cancellationToken);
                liquidity = EngineAnalysisResult.Unknown("Liquidity", InstitutionalWeights.Liquidity, "Fonte de candles não configurada.");
                volatility = EngineAnalysisResult.Unknown("Volatility", InstitutionalWeights.Volatility, "Fonte de candles não configurada.");
            }

            var structure = StructureComposer.Compose(trend, smc);
            var correlation = InstitutionalEngineComposer.BuildCorrelationForAsset(
                normalized, assetImpact, driverSnapshot);

            var engines = new List<EngineAnalysisResult>
            {
                structure,
                liquidity,
                volume,
                wyckoff,
                correlation,
                InstitutionalEngineComposer.BuildMacro(macro),
                volatility
            };

            var structureScore = structure.Score ?? 50;
            var volumeScore = volume.Score ?? 50;
            var isRanging = session?.Regime == "Ranging"
                            || structure.Bias == EngineMarketBias.Sideways && structureScore is >= 45 and <= 55;
            var structureUndefined = structure.Status != EngineDataStatus.Known
                                     || structure.Bias == EngineMarketBias.Sideways && structureScore is >= 45 and <= 55;
            var weakVolume = volume.Status != EngineDataStatus.Known || volumeScore < 40
                             || volume.Signals.Any(s => s.Contains("fraco", StringComparison.OrdinalIgnoreCase));
            var lowLiqSession = session?.IsLowLiquidity
                                ?? InstitutionalEngineComposer.IsLowLiquiditySession(normalized);

            var antiLoss = _antiLoss.Evaluate(new AntiLossFilterInput
            {
                Asset = normalized,
                AtrPercent = atrPercent,
                IsRanging = isRanging,
                HasTimeframeConflict = mtfConsensus.HasConflict,
                WeakVolume = weakVolume,
                StructureUndefined = structureUndefined,
                IsLowLiquiditySession = lowLiqSession,
                HasHighImpactCalendar = macro.UpcomingEvents.Any(e => e.Impact is "High"),
                StructureScore = structureScore,
                VolumeScore = volumeScore
            });

            var knownDirectional = engines.Count(e => e.Status == EngineDataStatus.Known && e.Score.HasValue);
            var risk = _risk.Assess(new InstitutionalRiskInput
            {
                Asset = normalized,
                KnownEngineCount = knownDirectional,
                TotalEngineCount = engines.Count,
                HasHighImpactCalendarEvent = macro.UpcomingEvents.Any(e => e.Impact is "High"),
                HasMediumImpactCalendarEvent = macro.UpcomingEvents.Any(e => e.Impact is "Medium"),
                Liquidity = macro.Liquidity switch
                {
                    Macro.DTO.MacroLevel.High => MacroLiquidityLevel.High,
                    Macro.DTO.MacroLevel.Low => MacroLiquidityLevel.Low,
                    _ => MacroLiquidityLevel.Normal
                },
                AtrPercent = atrPercent,
                IsLowLiquiditySession = lowLiqSession,
                HasTimeframeConflict = mtfConsensus.HasConflict,
                IsRanging = isRanging,
                StructureUndefined = structureUndefined,
                WeakVolume = weakVolume
            });

            // Penaliza confiança do Risk com anti-loss
            if (antiLoss.ConfidencePenalty > 0 && risk.Confidence > 0)
            {
                risk = EngineAnalysisResult.RiskOnly(
                    Math.Clamp(risk.Confidence - antiLoss.ConfidencePenalty, 10, 100),
                    risk.Signals.Concat(antiLoss.Reasons).Distinct().ToList(),
                    risk.ProcessingMs);
            }

            foreach (var engine in engines.Where(e => e.Status == EngineDataStatus.Known))
                _logger.LogDebug(
                    "TI engine {Engine} score={Score} confidence={Confidence:F0} source={Source} ms={Ms}",
                    engine.Engine, engine.Score, engine.Confidence, engine.DataSource, engine.ProcessingMs);

            // Risk suggestion provisional — Confluence só mantém se recomendação direcional sobreviver aos gates
            var provisionalScore = engines.Where(e => e.Score.HasValue).Select(e => e.Score!.Value).DefaultIfEmpty(50).Average();
            var provisionalRec = ConfluenceClassification.ClassifyRecommendation((int)Math.Round(provisionalScore));
            var provisionalRisk = _riskPlanner.Plan(provisionalRec, lastPrice, primaryCandles, smcDetail, atrPercent);

            var blocking = antiLoss.ShouldBlock ? antiLoss.Reasons.ToList() : [];
            if (session is not null)
                foreach (var note in session.Notes.Take(2))
                    if (session.IsLowLiquidity && note.Contains("fina", StringComparison.OrdinalIgnoreCase))
                        blocking.Add(note);

            var confluence = _confluence.Calculate(new InstitutionalConfluenceInput
            {
                Asset = normalized,
                Engines = engines,
                Risk = risk,
                LastPrice = lastPrice,
                DailyChangePercent = dailyChange,
                BlockingFactors = blocking.Distinct().ToList(),
                RiskSuggestion = provisionalRisk,
                Session = session,
                TimeframeConsensus = mtfConsensus
            });

            // Replaneja stop/TP com a recomendação final
            if (confluence.Recommendation.Contains("COMPRA", StringComparison.OrdinalIgnoreCase)
                || confluence.Recommendation.Contains("VENDA", StringComparison.OrdinalIgnoreCase))
            {
                var finalRisk = _riskPlanner.Plan(
                    confluence.Recommendation, lastPrice, primaryCandles, smcDetail, atrPercent);
                if (finalRisk is not null)
                {
                    confluence = new ConfluenceScoreResult
                    {
                        Score = confluence.Score,
                        Classification = confluence.Classification,
                        Recommendation = confluence.Recommendation,
                        Confidence = confluence.Confidence,
                        ConfidenceLevel = confluence.ConfidenceLevel,
                        Bias = confluence.Bias,
                        RiskLevel = confluence.RiskLevel,
                        DataQuality = confluence.DataQuality,
                        KnownEngineCount = confluence.KnownEngineCount,
                        TotalEngineCount = confluence.TotalEngineCount,
                        Components = confluence.Components,
                        PositiveFactors = confluence.PositiveFactors,
                        NegativeFactors = confluence.NegativeFactors,
                        BlockingFactors = confluence.BlockingFactors,
                        RiskSuggestion = finalRisk,
                        Explanation = confluence.Explanation.Contains("Risco sugerido", StringComparison.OrdinalIgnoreCase)
                            ? confluence.Explanation
                            : confluence.Explanation + "\n\nRisco sugerido: " + finalRisk.Summary
                    };
                }
            }

            var intersections = TimeframeIntersectionEngine.Calculate(timeframes);
            var operationalZones = _zones.BuildZones(normalized, confluence, timeframes, intersections);
            var (smcOverlays, smcTimelineEvents) = await BuildSmcOverlaysAsync(
                normalized, timeframes, candleRequestCache, cancellationToken);
            var timeline = TradingTimelineEngine.Build(new InstitutionalTimelineInput
            {
                Asset = normalized,
                Engines = engines,
                Confluence = confluence,
                Intersections = intersections,
                WyckoffEvents = wyckoffDetail?.Events ?? [],
                SmcEvents = smcTimelineEvents,
                SmcOverlays = smcOverlays
            });

            var heatMap = confluence.Components.Select(c => new TradingIntelligenceHeatCell
            {
                Engine = c.Engine,
                Score = c.Status == EngineDataStatus.Unknown ? 0 : c.Score,
                Weight = c.EffectiveWeight,
                Impact = c.Impact,
                Tooltip = c.Tooltip
            }).ToList();

            var snapshot = new TradingIntelligenceSnapshot
            {
                Asset = normalized,
                Timestamp = DateTime.UtcNow,
                Confluence = confluence,
                OperationalZones = operationalZones,
                TimeframeAnalyses = timeframes,
                Intersections = intersections,
                HeatMap = heatMap,
                Timeline = timeline,
                SmcOverlays = smcOverlays
            };

            if (_ai is not null)
            {
                var aiResult = await _ai.GetAiResultAsync(normalized, snapshot, cancellationToken);
                snapshot = new TradingIntelligenceSnapshot
                {
                    Asset = snapshot.Asset,
                    Timestamp = snapshot.Timestamp,
                    Confluence = snapshot.Confluence,
                    OperationalZones = snapshot.OperationalZones,
                    TimeframeAnalyses = snapshot.TimeframeAnalyses,
                    Intersections = snapshot.Intersections,
                    HeatMap = snapshot.HeatMap,
                    Timeline = snapshot.Timeline,
                    SmcOverlays = snapshot.SmcOverlays,
                    AiSummary = aiResult.Master,
                    AgentInsights = aiResult.AgentInsights
                };
            }
            else
            {
                snapshot = new TradingIntelligenceSnapshot
                {
                    Asset = snapshot.Asset,
                    Timestamp = snapshot.Timestamp,
                    Confluence = snapshot.Confluence,
                    OperationalZones = snapshot.OperationalZones,
                    TimeframeAnalyses = snapshot.TimeframeAnalyses,
                    Intersections = snapshot.Intersections,
                    HeatMap = snapshot.HeatMap,
                    Timeline = snapshot.Timeline,
                    SmcOverlays = snapshot.SmcOverlays,
                    AgentInsights = SpecialistAgentEngine.BuildInsights(normalized, snapshot)
                };
            }

            await _cache.SetSnapshotAsync(normalized, snapshot, tenantId, cancellationToken);
            _logger.LogInformation(
                "TI span asset={Asset} span=build_total duration_ms={DurationMs} status=ok",
                normalized, buildSw.ElapsedMilliseconds);
            return snapshot;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Trading intelligence snapshot failed for {Asset} duration_ms={DurationMs}",
                normalized, buildSw.ElapsedMilliseconds);
            return _cache.GetLastKnownSnapshot(normalized, tenantId)
                   ?? (IsSupportedAsset(normalized)
                       ? CreateDegradedSnapshot(normalized, ex.Message)
                       : null);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Trading intelligence snapshot cancelado para {Asset} duration_ms={DurationMs}",
                normalized, buildSw.ElapsedMilliseconds);
            return _cache.GetLastKnownSnapshot(normalized, tenantId)
                   ?? (IsSupportedAsset(normalized)
                       ? CreateDegradedSnapshot(normalized, "Timeout ao montar análise — tente Atualizar.")
                       : null);
        }
    }

    /// <summary>
    /// Soft timeout wrapper: on timeout/error returns fallback without canceling the parent request.
    /// </summary>
    private async Task<T> SoftAwaitAsync<T>(
        Func<Task<T>> work,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Func<T> onTimeoutOrError)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            return await work().WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "TI soft-timeout span elapsed_ms={ElapsedMs} — using fallback",
                (int)timeout.TotalMilliseconds);
            return onTimeoutOrError();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "TI soft-await failed — using fallback");
            return onTimeoutOrError();
        }
    }

    private bool IsSupportedAsset(string normalized) =>
        _options.Value.SupportedAssets.Any(a =>
            string.Equals(a, normalized, StringComparison.OrdinalIgnoreCase));

    private static MarketDrivers.Models.MarketDriversSnapshot CreateEmptyDriversSnapshot(string asset) =>
        new()
        {
            Asset = asset,
            Timestamp = DateTime.UtcNow,
            Drivers = [],
            Score = new MarketDrivers.Models.DriverScore
            {
                Score = 50,
                Label = "Neutro",
                Recommendation = "AGUARDAR DADOS",
                DataQuality = "Insuficiente"
            },
            Explanation = $"Drivers indisponíveis para {asset}."
        };

    private static TradingIntelligenceSnapshot CreateDegradedSnapshot(string asset, string reason) =>
        new()
        {
            Asset = asset,
            Timestamp = DateTime.UtcNow,
            Confluence = new ConfluenceScoreResult
            {
                Score = 50,
                Classification = "Neutral",
                Recommendation = "AGUARDAR DADOS",
                Confidence = 0,
                Bias = "Sideways",
                RiskLevel = "Moderado",
                DataQuality = "Insuficiente",
                Explanation = $"Trading Intelligence temporariamente indisponível para {asset}. {reason}"
            },
            Timeline =
            [
                new TradingTimelineEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Category = "System",
                    Title = "Análise parcial",
                    Description = reason,
                    Severity = "Warning"
                }
            ]
        };

    private EngineAnalysisResult ResolveCachedEngine(
        string asset,
        string engineName,
        DateTime? lastCandleTime,
        Func<EngineAnalysisResult> compute,
        string source)
    {
        if (_engineCache.IsFresh(asset, engineName, lastCandleTime))
        {
            var cached = _engineCache.Get<EngineAnalysisResult>(asset, engineName);
            if (cached is not null)
                return cached.Value;
        }

        var result = compute();
        _engineCache.Set(asset, engineName, result, lastCandleTime, source);
        return result;
    }

    private async Task<CandleFetchBundle> GetCandlesCachedAsync(
        Dictionary<string, CandleFetchBundle> candleRequestCache,
        string asset,
        string timeframe,
        int count,
        CancellationToken cancellationToken)
    {
        if (_candles is null)
            return new CandleFetchBundle();

        var key = $"{asset}|{ChartTimeframe.ToChartKey(timeframe)}|{count}";
        if (candleRequestCache.TryGetValue(key, out var cached))
            return cached;

        var bundle = await _candles.GetCandlesAsync(asset, count, timeframe, cancellationToken);
        candleRequestCache[key] = bundle;
        return bundle;
    }

    private WyckoffEngineResult? ResolveWyckoffCached(string asset, string timeframe, CandleFetchBundle bundle)
    {
        if (!bundle.HasSufficientData(50))
            return null;

        var candles = bundle.Candles.OrderBy(c => c.OpenTime).ToList();
        var lastCandleTime = candles[^1].OpenTime;
        var cacheKey = $"Wyckoff:{timeframe}";

        if (_engineCache.IsFresh(asset, cacheKey, lastCandleTime))
        {
            var cached = _engineCache.Get<WyckoffEngineResult>(asset, cacheKey);
            if (cached is not null)
                return cached.Value;
        }

        var result = _wyckoffEngine.Analyze(candles);
        _engineCache.Set(asset, cacheKey, result, lastCandleTime, bundle.Source);
        return result;
    }

    private SmcAnalysisResult? ResolveSmcCached(string asset, string timeframe, CandleFetchBundle bundle)
    {
        if (!bundle.HasSufficientData(20))
            return null;

        var candles = bundle.Candles.OrderBy(c => c.OpenTime).ToList();
        var lastCandleTime = candles[^1].OpenTime;
        var cacheKey = $"SMC:{timeframe}";

        if (_engineCache.IsFresh(asset, cacheKey, lastCandleTime))
        {
            var cached = _engineCache.Get<SmcAnalysisResult>(asset, cacheKey);
            if (cached is not null)
                return cached.Value;
        }

        var result = _smcEngine.Analyze(candles);
        _engineCache.Set(asset, cacheKey, result, lastCandleTime, bundle.Source);
        return result;
    }

    private IReadOnlyList<string> ResolveAnalysisTimeframes(string asset)
    {
        var opts = _options.Value;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();

        void AddUnique(IEnumerable<string> timeframes)
        {
            foreach (var raw in timeframes)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                // M5/5, H4/240, etc. → same chart key so ChartTimeframes + extras never collide.
                var key = ChartTimeframe.ToChartKey(raw);
                if (seen.Add(key))
                    list.Add(key);
            }
        }

        AddUnique(opts.ChartTimeframes);
        AddUnique(opts.ConsensusExtraTimeframes);

        // XAUUSD: prioriza M5–H1 + H4/D1 quando MT5 fornecer
        if (asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase))
            AddUnique(["5", "15", "30", "60", "240", "1440"]);

        return list;
    }

    private async Task<(IReadOnlyList<SmcOverlayBundle> Overlays, IReadOnlyList<SmcStructureEvent> Events)> BuildSmcOverlaysAsync(
        string asset,
        IReadOnlyList<TimeframeAnalysis> timeframes,
        Dictionary<string, CandleFetchBundle> candleRequestCache,
        CancellationToken cancellationToken)
    {
        if (_candles is null)
            return ([], []);

        var overlays = new List<SmcOverlayBundle>();
        var timelineEvents = new List<SmcStructureEvent>();
        var targets = timeframes.Count > 0
            ? timeframes.Select(t => ChartTimeframe.ToChartKey(t.Timeframe)).Distinct(StringComparer.OrdinalIgnoreCase)
            : new[] { "60" };

        foreach (var tf in targets)
        {
            var cacheKey = $"SMC:{tf}";
            var bundle = await GetCandlesCachedAsync(candleRequestCache, asset, tf, 120, cancellationToken);
            var lastCandle = bundle.Candles.Count > 0 ? bundle.Candles.Max(c => c.OpenTime) : (DateTime?)null;

            SmcAnalysisResult analysis;
            if (_engineCache.IsFresh(asset, cacheKey, lastCandle))
            {
                var cached = _engineCache.Get<SmcAnalysisResult>(asset, cacheKey);
                analysis = cached?.Value ?? _smcEngine.Analyze(bundle.Candles.OrderBy(c => c.OpenTime).ToList());
            }
            else
            {
                analysis = _smcEngine.Analyze(bundle.Candles.OrderBy(c => c.OpenTime).ToList());
                _engineCache.Set(asset, cacheKey, analysis, lastCandle, bundle.Source);
            }

            overlays.Add(new SmcOverlayBundle
            {
                Timeframe = tf,
                Score = analysis.Score,
                Bias = analysis.Bias.ToString(),
                Summary = analysis.Summary,
                Overlays = analysis.Overlays.Select(z => new SmcChartZoneDto
                {
                    Type = z.Type,
                    PriceLow = z.PriceLow,
                    PriceHigh = z.PriceHigh,
                    Label = z.Label
                }).ToList()
            });

            timelineEvents.AddRange(analysis.Events);
        }

        return (overlays, timelineEvents);
    }

    private static EngineAnalysisResult AggregateTimeframeEngine(
        string name,
        IEnumerable<int> scores,
        decimal weight,
        string source)
    {
        var list = scores.ToList();
        if (list.Count == 0)
            return EngineAnalysisResult.Unknown(name, weight, "Sem timeframes disponíveis.");

        var avg = (int)Math.Round(list.Average());
        return EngineAnalysisResult.Known(
            name,
            avg,
            Math.Clamp(40m + list.Count * 8m, 40, 88),
            weight,
            avg >= 58 ? EngineMarketBias.Bullish : avg <= 42 ? EngineMarketBias.Bearish : EngineMarketBias.Sideways,
            [$"média {list.Count} timeframes"],
            source);
    }

    public async Task<IReadOnlyList<TradingIntelligenceDashboardItem>> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var items = new List<TradingIntelligenceDashboardItem>();
        foreach (var asset in _options.Value.DashboardAssets)
        {
            var snapshot = await GetSnapshotAsync(asset, cancellationToken: cancellationToken);
            if (snapshot is null) continue;

            var topIntersection = snapshot.Intersections
                .Where(i => i.HighConfluence)
                .OrderByDescending(i => i.ConfluenceScore)
                .FirstOrDefault();

            items.Add(new TradingIntelligenceDashboardItem
            {
                Asset = snapshot.Asset,
                ConfluenceScore = snapshot.Confluence.Score,
                Classification = snapshot.Confluence.Classification,
                Recommendation = snapshot.Confluence.Recommendation,
                Confidence = snapshot.Confluence.Confidence,
                HighConfluenceZones = snapshot.Intersections.Count(i => i.HighConfluence),
                AgentInsightCount = snapshot.AgentInsights.Count,
                TopIntersection = topIntersection?.Pair ?? string.Empty,
                ExplanationPreview = snapshot.Confluence.Explanation.Length > 120
                    ? snapshot.Confluence.Explanation[..117] + "..."
                    : snapshot.Confluence.Explanation
            });
        }

        return items;
    }

    public TradingIntelligenceStatus GetStatus() =>
        new()
        {
            RedisEnabled = _options.Value.UseRedis,
            N8nConfigured = !string.IsNullOrWhiteSpace(_options.Value.N8nWebhookUrl),
            N8nAssetWebhooks = _options.Value.N8nAssetWebhookUrls.Count(kv => !string.IsNullOrWhiteSpace(kv.Value)),
            DashboardAssets = _options.Value.DashboardAssets,
            AiMode = !string.IsNullOrWhiteSpace(_options.Value.N8nWebhookUrl) ? "n8n" : "stub"
        };
}

public interface IDriverCompositionAdminService
{
    Task<IReadOnlyList<DriverCompositionDto>> ListAsync(string targetAsset, Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task<DriverCompositionDto?> CreateAsync(DriverCompositionUpsertRequest request, Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task<DriverCompositionDto?> UpdateAsync(Guid id, DriverCompositionUpsertRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> DuplicateAsync(string sourceAsset, string targetAsset, Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DriverCompositionDto>> ExportAsync(string targetAsset, Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task<int> ImportAsync(string targetAsset, IReadOnlyList<DriverCompositionUpsertRequest> items, Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task ReorderAsync(string targetAsset, IReadOnlyList<Guid> orderedIds, Guid? tenantId = null, CancellationToken cancellationToken = default);
}
