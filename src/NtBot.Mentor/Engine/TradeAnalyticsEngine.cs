using NtBot.Domain.Entities;
using NtBot.Mentor.Models;

namespace NtBot.Mentor.Engine;

public interface ITradeAnalyticsEngine
{
    IReadOnlyList<AnalyzedTrade> Analyze(IReadOnlyList<Trade> trades);
    IReadOnlyList<TimeSlotStat> BuildTimeSlots(IReadOnlyList<AnalyzedTrade> trades);
    IReadOnlyList<AssetStat> BuildAssetStats(IReadOnlyList<AnalyzedTrade> trades);
}

public sealed class TradeAnalyticsEngine : ITradeAnalyticsEngine
{
    private static readonly TimeSpan BrazilOffset = TimeSpan.FromHours(-3);

    public IReadOnlyList<AnalyzedTrade> Analyze(IReadOnlyList<Trade> trades) =>
        trades
            .Where(t => t.ExitTime.HasValue)
            .Select(t =>
            {
                var net = t.NetPnL ?? t.PnL ?? 0m;
                var localEntry = t.EntryTime.ToUniversalTime().Add(BrazilOffset);
                return new AnalyzedTrade
                {
                    Symbol = t.Symbol.ToUpperInvariant(),
                    EntryTime = t.EntryTime,
                    ExitTime = t.ExitTime,
                    NetPnL = net,
                    IsWin = net > 0,
                    EntryHourLocal = localEntry.Hour,
                    DayOfWeek = localEntry.DayOfWeek,
                    ExitReason = t.ExitReason
                };
            })
            .OrderBy(t => t.EntryTime)
            .ToList();

    public IReadOnlyList<TimeSlotStat> BuildTimeSlots(IReadOnlyList<AnalyzedTrade> trades) =>
        trades
            .GroupBy(t => t.EntryHourLocal)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var wins = g.Count(t => t.IsWin);
                var losses = g.Count(t => !t.IsWin);
                var avgWin = g.Where(t => t.IsWin).Select(t => t.NetPnL).DefaultIfEmpty(0).Average();
                var avgLoss = Math.Abs(g.Where(t => !t.IsWin).Select(t => t.NetPnL).DefaultIfEmpty(0).Average());
                return new TimeSlotStat
                {
                    Hour = g.Key,
                    Trades = g.Count(),
                    Wins = wins,
                    WinRate = g.Count() > 0 ? (decimal)wins / g.Count() * 100 : 0,
                    AvgPnL = g.Average(t => t.NetPnL),
                    Payoff = avgLoss > 0 ? avgWin / avgLoss : avgWin > 0 ? 2 : 0
                };
            })
            .ToList();

    public IReadOnlyList<AssetStat> BuildAssetStats(IReadOnlyList<AnalyzedTrade> trades) =>
        trades
            .GroupBy(t => t.Symbol)
            .OrderByDescending(g => g.Sum(t => t.NetPnL))
            .Select(g =>
            {
                var wins = g.Where(t => t.IsWin).ToList();
                var losses = g.Where(t => !t.IsWin).ToList();
                var avgWin = wins.Select(t => t.NetPnL).DefaultIfEmpty(0).Average();
                var avgLoss = Math.Abs(losses.Select(t => t.NetPnL).DefaultIfEmpty(0).Average());
                return new AssetStat
                {
                    Symbol = g.Key,
                    Trades = g.Count(),
                    WinRate = g.Count() > 0 ? (decimal)wins.Count / g.Count() * 100 : 0,
                    NetPnL = g.Sum(t => t.NetPnL),
                    Payoff = avgLoss > 0 ? avgWin / avgLoss : avgWin > 0 ? 2 : 0
                };
            })
            .ToList();
}
