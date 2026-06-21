using Microsoft.Extensions.Options;
using NtBot.Mentor.Configuration;
using NtBot.Mentor.Engine;
using NtBot.Mentor.Models;

namespace NtBot.UnitTests.Mentor;

public class PersonalRecommendationEngineTests
{
    private readonly PersonalRecommendationEngine _engine = new(
        Options.Create(new MentorOptions { MinTradesForRecommendations = 10, MinTradesPerBucket = 5 }));

    [Fact]
    public void Build_ReturnsEmpty_WhenInsufficientTrades()
    {
        var trades = BuildTrades(5, hour: 9, win: true);
        var result = _engine.Build(trades, BuildTimeSlots(trades), []);
        Assert.Empty(result);
    }

    [Fact]
    public void Build_DetectsBestTimingWindow()
    {
        var trades = new List<AnalyzedTrade>();
        trades.AddRange(BuildTrades(8, hour: 9, win: true, pnl: 100));
        trades.AddRange(BuildTrades(8, hour: 14, win: false, pnl: -80));
        trades.AddRange(BuildTrades(4, hour: 10, win: true, pnl: 50));

        var timeSlots = new TradeAnalyticsEngine().BuildTimeSlots(trades);
        var result = _engine.Build(trades, timeSlots, []);

        Assert.Contains(result, r => r.Category == RecommendationCategory.Timing);
    }

    private static List<AnalyzedTrade> BuildTrades(int count, int hour, bool win, decimal pnl = 50)
    {
        var list = new List<AnalyzedTrade>();
        for (var i = 0; i < count; i++)
        {
            list.Add(new AnalyzedTrade
            {
                Symbol = "WIN",
                EntryTime = DateTime.UtcNow.AddDays(-i),
                NetPnL = win ? pnl : -pnl,
                IsWin = win,
                EntryHourLocal = hour,
                DayOfWeek = DayOfWeek.Tuesday
            });
        }
        return list;
    }

    private static IReadOnlyList<TimeSlotStat> BuildTimeSlots(IReadOnlyList<AnalyzedTrade> trades) =>
        new TradeAnalyticsEngine().BuildTimeSlots(trades);
}
