using NtBot.Domain.Entities;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine;

public interface IConfluenceEngine
{
    ConfluenceScoreResult Calculate(InstitutionalConfluenceInput input);
    ConfluenceScoreResult Calculate(EngineScoreInput legacy);
}

public interface ITradingCandleSource
{
    Task<CandleFetchBundle> GetCandlesAsync(
        string asset,
        int count,
        string timeframe,
        CancellationToken cancellationToken = default);
}

public sealed class CandleFetchBundle
{
    public IReadOnlyList<Candle> Candles { get; init; } = [];
    public string Source { get; init; } = string.Empty;
    public bool HasSufficientData(int minimum) => Candles.Count >= minimum;
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
    Task<EngineAnalysisResult> GetAnalysisAsync(string asset, string timeframe, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeframeAnalysis>> GetTimeframeAnalysesAsync(
        string asset,
        IReadOnlyList<string>? timeframes = null,
        CancellationToken cancellationToken = default);
}

public interface ISmcScoreProvider
{
    Task<EngineAnalysisResult> GetAnalysisAsync(string asset, string timeframe, CancellationToken cancellationToken = default);
}

public interface IVolumeScoreProvider
{
    Task<EngineAnalysisResult> GetAnalysisAsync(string asset, string timeframe, CancellationToken cancellationToken = default);
}

public interface IN8nAiProvider
{
    Task<TradingIntelligenceAiResult> GetAiResultAsync(
        string asset,
        TradingIntelligenceSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
