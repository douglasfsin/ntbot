using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Models;
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
        IOptions<TradingIntelligenceOptions> options,
        ILogger<TradingIntelligenceService> logger,
        IN8nAiProvider? ai = null)
    {
        _drivers = drivers;
        _macro = macro;
        _market = market;
        _confluence = confluence;
        _zones = zones;
        _wyckoff = wyckoff;
        _smc = smc;
        _volume = volume;
        _ai = ai;
        _options = options;
        _logger = logger;
    }

    public async Task<TradingIntelligenceSnapshot?> GetSnapshotAsync(
        string asset,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(asset);

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
                var aiSummary = await _ai.GetMasterSummaryAsync(normalized, snapshot, cancellationToken);
                snapshot = new TradingIntelligenceSnapshot
                {
                    Asset = snapshot.Asset,
                    Timestamp = snapshot.Timestamp,
                    Confluence = snapshot.Confluence,
                    OperationalZones = snapshot.OperationalZones,
                    TimeframeAnalyses = snapshot.TimeframeAnalyses,
                    Intersections = snapshot.Intersections,
                    HeatMap = snapshot.HeatMap,
                    AiSummary = aiSummary
                };
            }

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
