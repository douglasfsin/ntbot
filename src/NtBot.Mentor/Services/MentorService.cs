using Microsoft.Extensions.Options;
using NtBot.Mentor.Configuration;
using NtBot.Mentor.Engine;
using NtBot.Mentor.Models;
using NtBot.Mentor.Persistence;

namespace NtBot.Mentor.Services;

public interface IMentorService
{
    Task<MentorSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class MentorService : IMentorService
{
    private readonly ITradeHistoryRepository _history;
    private readonly ITradeAnalyticsEngine _analytics;
    private readonly IPersonalRecommendationEngine _recommendations;
    private readonly IPerformanceScoreEngine _performance;
    private readonly MentorOptions _options;

    public MentorService(
        ITradeHistoryRepository history,
        ITradeAnalyticsEngine analytics,
        IPersonalRecommendationEngine recommendations,
        IPerformanceScoreEngine performance,
        IOptions<MentorOptions> options)
    {
        _history = history;
        _analytics = analytics;
        _recommendations = recommendations;
        _performance = performance;
        _options = options.Value;
    }

    public async Task<MentorSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var raw = await _history.GetClosedTradesAsync(tenantId, _options.HistoryDays, cancellationToken);
        var analyzed = _analytics.Analyze(raw);
        var timeSlots = _analytics.BuildTimeSlots(analyzed);
        var assets = _analytics.BuildAssetStats(analyzed);
        var score = _performance.Calculate(analyzed, timeSlots);
        var hasData = analyzed.Count >= _options.MinTradesForRecommendations;
        var recommendations = hasData
            ? _recommendations.Build(analyzed, timeSlots, assets)
            : [];

        var sessionDays = analyzed.Select(t => t.EntryTime.Date).Distinct().Count();
        var avgConfidence = recommendations.Count > 0
            ? (int)recommendations.Average(r => r.Confidence)
            : 0;

        return new MentorSnapshot
        {
            TradeCount = analyzed.Count,
            SessionDays = sessionDays,
            HistoryDays = _options.HistoryDays,
            HasSufficientData = hasData,
            Score = score,
            Recommendations = recommendations,
            TimeSlots = timeSlots,
            Assets = assets,
            DailyPlanSummary = hasData
                ? BuildDailyPlan(recommendations, score, avgConfidence, analyzed.Count, sessionDays)
                : $"Histórico insuficiente ({analyzed.Count}/{_options.MinTradesForRecommendations} trades). Continue operando para desbloquear recomendações personalizadas."
        };
    }

    private static string BuildDailyPlan(
        IReadOnlyList<PersonalRecommendation> recommendations,
        PerformanceScoreBreakdown score,
        int avgConfidence,
        int trades,
        int days)
    {
        var timing = recommendations.FirstOrDefault(r => r.Category == RecommendationCategory.Timing);
        var risk = recommendations.FirstOrDefault(r => r.Category == RecommendationCategory.Risk);
        var parts = new List<string>
        {
            $"Score {score.Total}/100 ({score.Classification}).",
            timing is not null ? timing.Action : "Defina janela operacional após mais dados.",
            risk is not null ? risk.Action : "Respeite limite de perdas consecutivas."
        };
        return string.Join(" ", parts) +
               $" Confiança média {avgConfidence}% · {trades} ops · {days} pregões.";
    }
}
