using NtBot.Api.Services.MarketData;
using NtBot.Domain.Entities;
using NtBot.Shared.MarketData;
using NtBot.TradingIntelligence.Cache;
using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Engine.Volume;
using NtBot.TradingIntelligence.Models;

namespace NtBot.Api.Services.TradingIntelligence;

public sealed class TradingCandleSourceAdapter : ITradingCandleSource
{
    private readonly IMarketCandleService _candles;

    public TradingCandleSourceAdapter(IMarketCandleService candles) => _candles = candles;

    public async Task<CandleFetchBundle> GetCandlesAsync(
        string asset,
        int count,
        string timeframe,
        CancellationToken cancellationToken = default)
    {
        var result = await _candles.GetCandlesAsync(asset, count, timeframe, cancellationToken);
        return new CandleFetchBundle
        {
            Candles = result.Candles,
            Source = result.Source
        };
    }
}

public sealed class WyckoffScoreProviderAdapter : IWyckoffScoreProvider
{
    private readonly IWyckoffEngine _wyckoff;
    private readonly ITradingCandleSource _candles;
    private readonly ISmcEngine _smc;
    private readonly IVolumeAnalysisEngine _volumeEngine;
    private readonly ITradingEngineCacheService _engineCache;

    public WyckoffScoreProviderAdapter(
        IWyckoffEngine wyckoff,
        ITradingCandleSource candles,
        ISmcEngine smc,
        IVolumeAnalysisEngine volumeEngine,
        ITradingEngineCacheService engineCache)
    {
        _wyckoff = wyckoff;
        _candles = candles;
        _smc = smc;
        _volumeEngine = volumeEngine;
        _engineCache = engineCache;
    }

    public async Task<EngineAnalysisResult> GetAnalysisAsync(
        string asset,
        string timeframe,
        CancellationToken cancellationToken = default)
    {
        var result = await _candles.GetCandlesAsync(asset, 120, timeframe, cancellationToken);
        if (!result.HasSufficientData(20))
        {
            return EngineAnalysisResult.Unknown(
                "Wyckoff",
                InstitutionalWeights.Wyckoff,
                $"Candles insuficientes para Wyckoff ({result.Candles.Count}/20).",
                result.Source);
        }

        var candles = result.Candles.OrderBy(c => c.OpenTime).ToList();
        var lastCandle = candles[^1].OpenTime;
        var analysis = ResolveWyckoff(asset, timeframe, candles, lastCandle, result.Source);
        return MapWyckoffResult(analysis, result.Source);
    }

    public async Task<IReadOnlyList<TimeframeAnalysis>> GetTimeframeAnalysesAsync(
        string asset,
        IReadOnlyList<string>? timeframes = null,
        CancellationToken cancellationToken = default)
    {
        var tfs = (timeframes is { Count: > 0 } ? timeframes : (IReadOnlyList<string>)["5", "15", "30", "60"])
            .Select(ChartTimeframe.ToChartKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var tasks = tfs.Select(tf => LoadTimeframeAnalysisAsync(asset, tf, cancellationToken)).ToList();
        var results = await Task.WhenAll(tasks);
        return results.Where(r => r is not null).Cast<TimeframeAnalysis>().ToList();
    }

    private async Task<TimeframeAnalysis?> LoadTimeframeAnalysisAsync(
        string asset,
        string tf,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _candles.GetCandlesAsync(asset, 120, tf, cancellationToken);
            if (!result.HasSufficientData(10))
                return null;

            var candles = result.Candles.OrderBy(c => c.OpenTime).ToList();
            var lastCandle = candles[^1].OpenTime;
            var analysis = ResolveWyckoff(asset, tf, candles, lastCandle, result.Source);
            var smc = _smc.Analyze(candles);
            var volume = _volumeEngine.Analyze(asset, candles);
            var high = candles.Max(c => c.High);
            var low = candles.Min(c => c.Low);

            return new TimeframeAnalysis
            {
                Timeframe = tf,
                High = high,
                Low = low,
                Mid = (high + low) / 2,
                WyckoffScore = MapWyckoffResult(analysis, result.Source).Score ?? 50,
                SmcScore = smc.Score,
                VolumeScore = volume.Score ?? 50
            };
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private WyckoffEngineResult ResolveWyckoff(
        string asset,
        string timeframe,
        IReadOnlyList<Candle> candles,
        DateTime lastCandleTime,
        string source)
    {
        var cacheKey = $"Wyckoff:{timeframe}";
        if (_engineCache.IsFresh(asset, cacheKey, lastCandleTime))
        {
            var cached = _engineCache.Get<WyckoffEngineResult>(asset, cacheKey);
            if (cached is not null)
                return cached.Value;
        }

        var analysis = _wyckoff.Analyze(candles);
        _engineCache.Set(asset, cacheKey, analysis, lastCandleTime, source);
        return analysis;
    }

    private static EngineAnalysisResult MapWyckoffResult(WyckoffEngineResult analysis, string source)
    {
        if (analysis.Signals.Any(s => s.Contains("insuficientes", StringComparison.OrdinalIgnoreCase)))
        {
            return EngineAnalysisResult.Unknown(
                "Wyckoff",
                InstitutionalWeights.Wyckoff,
                analysis.Signals[0],
                source);
        }

        return EngineAnalysisResult.Known(
            "Wyckoff",
            analysis.Score,
            analysis.Confidence,
            InstitutionalWeights.Wyckoff,
            analysis.Bias,
            analysis.Signals,
            source);
    }
}

public sealed class SmcScoreProviderAdapter : ISmcScoreProvider
{
    private readonly ITradingCandleSource _candles;
    private readonly ISmcEngine _smc;

    public SmcScoreProviderAdapter(ITradingCandleSource candles, ISmcEngine smc)
    {
        _candles = candles;
        _smc = smc;
    }

    public async Task<EngineAnalysisResult> GetAnalysisAsync(
        string asset,
        string timeframe,
        CancellationToken cancellationToken = default)
    {
        var result = await _candles.GetCandlesAsync(asset, 120, timeframe, cancellationToken);
        if (!result.HasSufficientData(20))
        {
            return EngineAnalysisResult.Unknown(
                "SMC",
                InstitutionalWeights.Smc,
                $"Candles insuficientes para SMC ({result.Candles.Count}/20).",
                result.Source);
        }

        var smc = _smc.Analyze(result.Candles.OrderBy(c => c.OpenTime).ToList());
        var bias = smc.Bias switch
        {
            SmcStructureBias.Bullish => EngineMarketBias.Bullish,
            SmcStructureBias.Bearish => EngineMarketBias.Bearish,
            _ => EngineMarketBias.Sideways
        };

        var signals = new List<string>();
        if (!string.IsNullOrWhiteSpace(smc.Summary)) signals.Add(smc.Summary);
        if (smc.BullishBos) signals.Add("BOS bullish");
        if (smc.BearishBos) signals.Add("BOS bearish");
        if (smc.BullishChoch) signals.Add("CHoCH bullish");
        if (smc.BearishChoch) signals.Add("CHoCH bearish");
        if (smc.BullishOrderBlocks > 0) signals.Add($"{smc.BullishOrderBlocks} order block(s) comprador(es)");
        if (smc.BearishOrderBlocks > 0) signals.Add($"{smc.BearishOrderBlocks} order block(s) vendedor(es)");
        foreach (var evt in smc.Events.Take(3))
            signals.Add(evt.Title);

        var confidence = 40m + Math.Abs(smc.Score - 50) * 0.8m
            + (smc.BullishBos || smc.BearishBos ? 15m : 0m);

        return EngineAnalysisResult.Known(
            "SMC",
            smc.Score,
            Math.Clamp(confidence, 35, 92),
            InstitutionalWeights.Smc,
            bias,
            signals,
            result.Source);
    }
}

public sealed class VolumeScoreProviderAdapter : IVolumeScoreProvider
{
    private readonly ITradingCandleSource _candles;
    private readonly IVolumeAnalysisEngine _volumeEngine;

    public VolumeScoreProviderAdapter(
        ITradingCandleSource candles,
        IVolumeAnalysisEngine volumeEngine)
    {
        _candles = candles;
        _volumeEngine = volumeEngine;
    }

    public async Task<EngineAnalysisResult> GetAnalysisAsync(
        string asset,
        string timeframe,
        CancellationToken cancellationToken = default)
    {
        var result = await _candles.GetCandlesAsync(asset, 80, timeframe, cancellationToken);
        if (!result.HasSufficientData(20))
        {
            return EngineAnalysisResult.Unknown(
                "Volume",
                InstitutionalWeights.Volume,
                $"Candles insuficientes para Volume ({result.Candles.Count}/20).",
                result.Source);
        }

        return _volumeEngine.Analyze(asset, result.Candles.OrderBy(c => c.OpenTime).ToList());
    }
}

public sealed class N8nAiProviderStub : IN8nAiProvider
{
    public Task<TradingIntelligenceAiResult> GetAiResultAsync(
        string asset,
        TradingIntelligenceSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var summary = new MasterAgentSummary
        {
            Summary = snapshot.Confluence.Explanation,
            Confluences = snapshot.Intersections.Where(i => i.HighConfluence).Select(i => i.Pair).ToList(),
            Strengths = snapshot.Confluence.PositiveFactors.ToList(),
            Weaknesses = snapshot.Confluence.NegativeFactors.ToList(),
            Drivers = snapshot.HeatMap.Where(h => h.Engine == "Drivers").Select(h => $"{h.Engine}: {h.Score}").ToList(),
            Probability = snapshot.Confluence.Score >= 70 ? "Elevada" : snapshot.Confluence.Score <= 30 ? "Baixa" : "Moderada",
            Risk = snapshot.Confluence.RiskLevel
        };

        return Task.FromResult(new TradingIntelligenceAiResult
        {
            Master = summary,
            AgentInsights = SpecialistAgentEngine.BuildInsights(asset, snapshot)
        });
    }
}
