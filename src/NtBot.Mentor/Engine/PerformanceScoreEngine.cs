using NtBot.Mentor.Models;

namespace NtBot.Mentor.Engine;

public interface IPerformanceScoreEngine
{
    PerformanceScoreBreakdown Calculate(IReadOnlyList<AnalyzedTrade> trades, IReadOnlyList<TimeSlotStat> timeSlots);
}

public sealed class PerformanceScoreEngine : IPerformanceScoreEngine
{
    public PerformanceScoreBreakdown Calculate(
        IReadOnlyList<AnalyzedTrade> trades,
        IReadOnlyList<TimeSlotStat> timeSlots)
    {
        if (trades.Count == 0)
            return new PerformanceScoreBreakdown { Classification = "Sem dados" };

        var wins = trades.Where(t => t.IsWin).ToList();
        var losses = trades.Where(t => !t.IsWin).ToList();
        var avgWin = wins.Select(t => t.NetPnL).DefaultIfEmpty(0).Average();
        var avgLoss = Math.Abs(losses.Select(t => t.NetPnL).DefaultIfEmpty(0).Average());
        var payoff = avgLoss > 0 ? avgWin / avgLoss : avgWin > 0 ? 2m : 0m;
        var winRate = trades.Count > 0 ? (decimal)wins.Count / trades.Count : 0;
        var expectancy = trades.Average(t => t.NetPnL);

        var bestHours = timeSlots.Where(s => s.WinRate >= 55 && s.Trades >= 3).Select(s => s.Hour).ToHashSet();
        var disciplineTrades = trades.Count(t => bestHours.Count == 0 || bestHours.Contains(t.EntryHourLocal));
        var discipline = trades.Count > 0 ? (int)Math.Clamp(disciplineTrades * 100m / trades.Count, 0, 100) : 50;

        var stopRespected = trades.Count(t =>
            t.ExitReason is not null &&
            (t.ExitReason.Contains("Stop", StringComparison.OrdinalIgnoreCase) ||
             t.ExitReason.Contains("SL", StringComparison.OrdinalIgnoreCase)));
        var risk = losses.Count > 0
            ? (int)Math.Clamp(100 - Math.Min(avgLoss / Math.Max(avgWin, 1) * 30, 40) + stopRespected * 5, 40, 98)
            : 85;

        var dailyPnL = trades
            .GroupBy(t => t.EntryTime.Date)
            .Select(g => g.Sum(t => t.NetPnL))
            .ToList();
        var avgDaily = dailyPnL.DefaultIfEmpty(0).Average();
        var variance = dailyPnL.Count > 1
            ? dailyPnL.Average(d => (d - avgDaily) * (d - avgDaily))
            : 0;
        var consistency = (int)Math.Clamp(100 - Math.Min((double)Math.Sqrt((double)variance) / 100, 50), 45, 98);

        var payoffScore = (int)Math.Clamp(payoff * 35, 0, 100);
        var expectancyScore = expectancy >= 0
            ? (int)Math.Clamp(60 + expectancy * 2, 50, 98)
            : (int)Math.Clamp(50 + expectancy * 2, 20, 55);

        var maxStreakLoss = MaxConsecutiveLosses(trades);
        var drawdownControl = (int)Math.Clamp(100 - maxStreakLoss * 12, 35, 98);

        var total = (int)Math.Clamp(
            discipline * 0.30m +
            risk * 0.25m +
            consistency * 0.20m +
            payoffScore * 0.10m +
            expectancyScore * 0.10m +
            drawdownControl * 0.05m,
            0, 100);

        return new PerformanceScoreBreakdown
        {
            Discipline = discipline,
            RiskManagement = risk,
            Consistency = consistency,
            Payoff = payoffScore,
            Expectancy = expectancyScore,
            DrawdownControl = drawdownControl,
            Total = total,
            Classification = Classify(total)
        };
    }

    private static int MaxConsecutiveLosses(IReadOnlyList<AnalyzedTrade> trades)
    {
        var max = 0;
        var current = 0;
        foreach (var t in trades.OrderBy(x => x.EntryTime))
        {
            if (!t.IsWin) { current++; max = Math.Max(max, current); }
            else current = 0;
        }
        return max;
    }

    private static string Classify(int score) => score switch
    {
        >= 95 => "Institucional",
        >= 85 => "Excelente",
        >= 70 => "Consistente",
        >= 55 => "Em Evolução",
        >= 40 => "Instável",
        _ => "Necessita Revisão"
    };
}
