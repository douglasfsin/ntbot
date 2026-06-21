using NtBot.Api.Services.MarketData;
using NtBot.Api.Services.Wyckoff;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Models;

namespace NtBot.Api.Services.TradingIntelligence;

public sealed class WyckoffScoreProviderAdapter : IWyckoffScoreProvider
{
    private readonly IWyckoffService _wyckoff;
    private readonly IMarketCandleService _candles;

    public WyckoffScoreProviderAdapter(IWyckoffService wyckoff, IMarketCandleService candles)
    {
        _wyckoff = wyckoff;
        _candles = candles;
    }

    public async Task<int> GetScoreAsync(string asset, string timeframe, CancellationToken cancellationToken = default)
    {
        var result = await _candles.GetCandlesAsync(asset, 120, timeframe, cancellationToken);
        if (!result.HasSufficientData(20))
            return 50;

        var analysis = await _wyckoff.AnalyzeAsync(asset, timeframe, result.Candles.ToList());
        return ScoreFromAnalysis(analysis);
    }

    public async Task<IReadOnlyList<TimeframeAnalysis>> GetTimeframeAnalysesAsync(
        string asset,
        CancellationToken cancellationToken = default)
    {
        var timeframes = new[] { "5", "15", "30", "60" };
        var list = new List<TimeframeAnalysis>();

        foreach (var tf in timeframes)
        {
            var result = await _candles.GetCandlesAsync(asset, 120, tf, cancellationToken);
            if (!result.HasSufficientData(10))
                continue;

            var candles = result.Candles.ToList();
            var analysis = await _wyckoff.AnalyzeAsync(asset, tf, candles);
            var high = candles.Max(c => c.High);
            var low = candles.Min(c => c.Low);

            list.Add(new TimeframeAnalysis
            {
                Timeframe = tf,
                High = high,
                Low = low,
                Mid = (high + low) / 2,
                WyckoffScore = ScoreFromAnalysis(analysis),
                SmcScore = 50,
                VolumeScore = analysis.VolumeConfirmation ? 70 : 45
            });
        }

        return list;
    }

    private static int ScoreFromAnalysis(WyckoffAnalysisResult analysis) =>
        analysis.Bias switch
        {
            MarketBias.BULLISH => (int)Math.Clamp(55m + analysis.PhaseConfidence * 0.45m, 0, 100),
            MarketBias.BEARISH => (int)Math.Clamp(45m - analysis.PhaseConfidence * 0.45m, 0, 100),
            _ => (int)Math.Clamp(analysis.PhaseConfidence * 0.5m + 25, 0, 100)
        };
}

public sealed class SmcScoreProviderAdapter : ISmcScoreProvider
{
    private readonly IMarketCandleService _candles;

    public SmcScoreProviderAdapter(IMarketCandleService candles) => _candles = candles;

    public async Task<int> GetScoreAsync(string asset, string timeframe, CancellationToken cancellationToken = default)
    {
        var result = await _candles.GetCandlesAsync(asset, 80, timeframe, cancellationToken);
        if (!result.HasSufficientData(15))
            return 50;

        var candles = result.Candles.OrderBy(c => c.OpenTime).ToList();
        var last = candles[^1];
        var prev = candles[^2];
        var bullish = last.Close > prev.Close && last.Close > last.Open;
        var bearish = last.Close < prev.Close && last.Close < last.Open;

        if (bullish) return 68;
        if (bearish) return 32;
        return 50;
    }
}

public sealed class VolumeScoreProviderAdapter : IVolumeScoreProvider
{
    private readonly IWyckoffService _wyckoff;
    private readonly IMarketCandleService _candles;

    public VolumeScoreProviderAdapter(IWyckoffService wyckoff, IMarketCandleService candles)
    {
        _wyckoff = wyckoff;
        _candles = candles;
    }

    public async Task<int> GetScoreAsync(string asset, string timeframe, CancellationToken cancellationToken = default)
    {
        var result = await _candles.GetCandlesAsync(asset, 60, timeframe, cancellationToken);
        if (!result.HasSufficientData(10))
            return 50;

        var candles = result.Candles.ToList();
        var divergent = await _wyckoff.IsVolumeDivergentAsync(candles);
        var avgVol = candles.Average(c => (double)c.Volume);
        var lastVol = (double)candles[^1].Volume;
        var ratio = avgVol > 0 ? lastVol / avgVol : 1;

        var score = 50;
        if (ratio > 1.3) score += 15;
        if (ratio < 0.7) score -= 10;
        if (divergent) score -= 12;
        if ((candles[^1].Delta ?? 0) > 0) score += 8;

        return (int)Math.Clamp(score, 0, 100);
    }
}

public sealed class N8nAiProviderStub : IN8nAiProvider
{
    public Task<MasterAgentSummary?> GetMasterSummaryAsync(
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
            Risk = snapshot.Confluence.Score >= 80 ? "Controlado com confluência" : "Monitorar volatilidade"
        };

        return Task.FromResult<MasterAgentSummary?>(summary);
    }
}
