using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Engine;
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
    private readonly IMarketDriversService _drivers;
    private readonly IMacroIntelligenceService _macro;
    private readonly IMarketIntelligenceService _market;
    private readonly IConfluenceEngine _confluence;
    private readonly IOperationalZoneEngine _zones;
    private readonly IWyckoffScoreProvider _wyckoff;
    private readonly ISmcScoreProvider _smc;
    private readonly IVolumeScoreProvider _volume;
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
        ITradingIntelligenceCacheService cache,
        IOptions<TradingIntelligenceOptions> options,
        ILogger<TradingIntelligenceService> logger,
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
                await _notifier.NotifySnapshotUpdatedAsync(snapshot, cancellationToken);
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
            return cached;

        return await BuildSnapshotAsync(normalized, tenantId, cancellationToken);
    }

    private async Task<TradingIntelligenceSnapshot?> BuildSnapshotAsync(
        string normalized,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            var driverSnapshot = await _drivers.GetSnapshotAsync(normalized, cancellationToken);
            if (driverSnapshot is null)
                return null;

            var macro = await _macro.GetCurrentSnapshotAsync(normalized, cancellationToken);
            var quant = await _market.GetQuantScoreAsync(cancellationToken);
            var correlation = await _market.GetCorrelationAsync(cancellationToken);

            var timeframes = await _wyckoff.GetTimeframeAnalysesAsync(normalized, cancellationToken);
            var wyckoffScore = timeframes.Count > 0
                ? (int)timeframes.Average(t => t.WyckoffScore)
                : await _wyckoff.GetScoreAsync(normalized, "60", cancellationToken);

            var smcScores = await Task.WhenAll(_options.Value.ChartTimeframes.Select(tf =>
                _smc.GetScoreAsync(normalized, tf, cancellationToken)));
            var smcScore = smcScores.Length > 0 ? (int)smcScores.Average() : 50;

            var volumeScores = await Task.WhenAll(_options.Value.ChartTimeframes.Select(tf =>
                _volume.GetScoreAsync(normalized, tf, cancellationToken)));
            var volumeScore = volumeScores.Length > 0 ? (int)volumeScores.Average() : 50;

            var assetImpact = correlation.AssetImpacts.FirstOrDefault(a =>
                string.Equals(a.Asset, normalized, StringComparison.OrdinalIgnoreCase));

            var calendarScore = macro.UpcomingEvents.Any(e => e.Impact is "High")
                ? 40
                : macro.UpcomingEvents.Any(e => e.Impact is "Medium") ? 55 : 60;

            var liquidityScore = macro.Liquidity switch
            {
                Macro.DTO.MacroLevel.High => 75,
                Macro.DTO.MacroLevel.Low => 35,
                _ => 55
            };

            var momentumDriver = driverSnapshot.Drivers
                .FirstOrDefault(d => d.Category == MarketDrivers.Configuration.MarketDriverCategory.Momentum);
            var momentumScore = momentumDriver is not null
                ? ScoreFromVariation(momentumDriver.Variation)
                : quant.Score;

            var input = new EngineScoreInput
            {
                Asset = normalized,
                MacroScore = ScoreMacro(macro),
                QuantScore = quant.Score,
                DriverScore = driverSnapshot.Score.Score,
                WyckoffScore = wyckoffScore,
                SmcScore = smcScore,
                VolumeScore = volumeScore,
                MomentumScore = momentumScore,
                CorrelationScore = assetImpact is null
                    ? 50
                    : (int)Math.Clamp((assetImpact.ImpactScore + 1) / 2 * 100, 0, 100),
                LiquidityScore = liquidityScore,
                CalendarScore = calendarScore
            };

            var confluence = _confluence.Calculate(input);
            var intersections = TimeframeIntersectionEngine.Calculate(timeframes);
            var operationalZones = _zones.BuildZones(normalized, confluence, timeframes, intersections);

            var heatMap = confluence.Components.Select(c => new TradingIntelligenceHeatCell
            {
                Engine = c.Engine,
                Score = c.Score,
                Weight = c.Weight,
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
                HeatMap = heatMap
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
                    AgentInsights = SpecialistAgentEngine.BuildInsights(normalized, snapshot)
                };
            }

            await _cache.SetSnapshotAsync(normalized, snapshot, tenantId, cancellationToken);
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Trading intelligence snapshot failed for {Asset}", normalized);
            return null;
        }
    }

    private static int ScoreMacro(Macro.DTO.MacroSnapshot macro) =>
        macro.MacroScore switch
        {
            Macro.DTO.MacroRegimeLabel.Bullish => (int)Math.Clamp(55m + macro.Confidence * 0.45m, 0, 100),
            Macro.DTO.MacroRegimeLabel.Bearish => (int)Math.Clamp(45m - macro.Confidence * 0.45m, 0, 100),
            _ => 50
        };

    private static int ScoreFromVariation(decimal variation) =>
        (int)Math.Clamp(50 + variation * 15, 0, 100);

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
