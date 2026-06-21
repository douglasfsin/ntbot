using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine;

public interface IConfluenceEngine
{
    ConfluenceScoreResult Calculate(EngineScoreInput input);
}

public interface IOperationalZoneEngine
{
    IReadOnlyList<OperationalZone> BuildZones(
        string asset,
        ConfluenceScoreResult confluence,
        IReadOnlyList<TimeframeAnalysis> timeframes,
        IReadOnlyList<TimeframeIntersection> intersections);
}

public interface IWyckoffScoreProvider
{
    Task<int> GetScoreAsync(string asset, string timeframe, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeframeAnalysis>> GetTimeframeAnalysesAsync(string asset, CancellationToken cancellationToken = default);
}

public interface ISmcScoreProvider
{
    Task<int> GetScoreAsync(string asset, string timeframe, CancellationToken cancellationToken = default);
}

public interface IVolumeScoreProvider
{
    Task<int> GetScoreAsync(string asset, string timeframe, CancellationToken cancellationToken = default);
}

public interface IN8nAiProvider
{
    Task<TradingIntelligenceAiResult> GetAiResultAsync(
        string asset,
        TradingIntelligenceSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
