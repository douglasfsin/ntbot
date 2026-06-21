using Microsoft.Extensions.Options;
using NtBot.Mentor.Configuration;
using NtBot.Mentor.Models;

namespace NtBot.Mentor.Engine;

public interface IPersonalRecommendationEngine
{
    IReadOnlyList<PersonalRecommendation> Build(
        IReadOnlyList<AnalyzedTrade> trades,
        IReadOnlyList<TimeSlotStat> timeSlots,
        IReadOnlyList<AssetStat> assets);
}

public sealed class PersonalRecommendationEngine : IPersonalRecommendationEngine
{
    private readonly MentorOptions _options;

    public PersonalRecommendationEngine(IOptions<MentorOptions> options) =>
        _options = options.Value;

    public IReadOnlyList<PersonalRecommendation> Build(
        IReadOnlyList<AnalyzedTrade> trades,
        IReadOnlyList<TimeSlotStat> timeSlots,
        IReadOnlyList<AssetStat> assets)
    {
        if (trades.Count < _options.MinTradesForRecommendations)
            return [];

        var list = new List<PersonalRecommendation>();
        list.AddRange(BuildTimingRecommendations(trades, timeSlots));
        list.AddRange(BuildRiskRecommendations(trades));
        list.AddRange(BuildAssetRecommendations(assets));
        list.AddRange(BuildDayOfWeekRecommendations(trades));
        return list.OrderByDescending(r => r.Confidence).ToList();
    }

    private IEnumerable<PersonalRecommendation> BuildTimingRecommendations(
        IReadOnlyList<AnalyzedTrade> trades,
        IReadOnlyList<TimeSlotStat> timeSlots)
    {
        var qualified = timeSlots
            .Where(s => s.Trades >= _options.MinTradesPerBucket)
            .ToList();

        var best = qualified
            .Where(s => s.WinRate >= 55 && s.AvgPnL > 0)
            .OrderByDescending(s => s.WinRate)
            .ThenByDescending(s => s.Trades)
            .FirstOrDefault();

        if (best is not null)
        {
            var endHour = Math.Min(best.Hour + 1, 17);
            yield return new PersonalRecommendation
            {
                Category = RecommendationCategory.Timing,
                Title = "Janela de maior acerto",
                Summary = $"Você apresenta {best.WinRate:F0}% de acerto entre {best.Hour:D2}:00 e {endHour:D2}:00.",
                Action = "Priorize operações nesse horário.",
                Confidence = ConfidenceFromSample(best.Trades, best.WinRate),
                Evidence =
                [
                    $"{best.Trades} operações analisadas",
                    $"Payoff {best.Payoff:F2}",
                    $"PnL médio {best.AvgPnL:N2}"
                ]
            };
        }

        var afternoon = qualified
            .Where(s => s.Hour >= 11 && s.AvgPnL < 0)
            .OrderBy(s => s.AvgPnL)
            .FirstOrDefault();

        if (afternoon is not null)
        {
            yield return new PersonalRecommendation
            {
                Category = RecommendationCategory.Operational,
                Title = "Expectativa negativa após meio-dia",
                Summary = $"Após {afternoon.Hour:D2}:00 sua expectativa matemática média é {afternoon.AvgPnL:N2} por trade.",
                Action = "Evite novas entradas após 11:30 ou reduza exposição.",
                Confidence = ConfidenceFromSample(afternoon.Trades, 100 - afternoon.WinRate),
                Evidence =
                [
                    $"{afternoon.Trades} trades no período",
                    $"Win rate {afternoon.WinRate:F0}%"
                ]
            };
        }
    }

    private IEnumerable<PersonalRecommendation> BuildRiskRecommendations(IReadOnlyList<AnalyzedTrade> trades)
    {
        var streak = 0;
        var afterTwoLosses = new List<decimal>();

        foreach (var trade in trades.OrderBy(t => t.EntryTime))
        {
            if (streak >= 2)
                afterTwoLosses.Add(trade.NetPnL);

            streak = trade.IsWin ? 0 : streak + 1;
        }

        if (afterTwoLosses.Count < _options.MinTradesPerBucket)
            yield break;

        var avgAfterLosses = afterTwoLosses.Average();
        if (avgAfterLosses >= 0)
            yield break;

        yield return new PersonalRecommendation
        {
            Category = RecommendationCategory.Risk,
            Title = "Sequência de perdas",
            Summary = "Após duas perdas consecutivas, o resultado médio da próxima operação deteriora.",
            Action = "Encerre operações por 20 minutos e revise contexto antes de reentrar.",
            Confidence = ConfidenceFromSample(afterTwoLosses.Count, 70),
            Evidence =
            [
                $"{afterTwoLosses.Count} eventos pós-2 perdas",
                $"PnL médio seguinte: {avgAfterLosses:N2}"
            ]
        };
    }

    private IEnumerable<PersonalRecommendation> BuildAssetRecommendations(IReadOnlyList<AssetStat> assets)
    {
        var top = assets.Where(a => a.Trades >= _options.MinTradesPerBucket && a.NetPnL > 0)
            .OrderByDescending(a => a.Payoff)
            .FirstOrDefault();
        var weak = assets.Where(a => a.Trades >= _options.MinTradesPerBucket && a.NetPnL < 0)
            .OrderBy(a => a.NetPnL)
            .FirstOrDefault();

        if (top is not null)
        {
            yield return new PersonalRecommendation
            {
                Category = RecommendationCategory.Asset,
                Title = $"Ativo mais consistente: {top.Symbol}",
                Summary = $"Payoff {top.Payoff:F2} com win rate {top.WinRate:F0}% em {top.Trades} trades.",
                Action = $"Priorize {top.Symbol} quando o plano do dia permitir.",
                Confidence = ConfidenceFromSample(top.Trades, top.WinRate),
                Evidence = [$"PnL acumulado {top.NetPnL:N2}"]
            };
        }

        if (weak is not null)
        {
            yield return new PersonalRecommendation
            {
                Category = RecommendationCategory.Asset,
                Title = $"Revisar exposição em {weak.Symbol}",
                Summary = $"Expectativa negativa ({weak.NetPnL:N2}) em {weak.Trades} operações.",
                Action = $"Reduzir ou pausar {weak.Symbol} até revisar setup.",
                Confidence = ConfidenceFromSample(weak.Trades, 100 - weak.WinRate),
                Evidence = [$"Payoff {weak.Payoff:F2}"]
            };
        }
    }

    private IEnumerable<PersonalRecommendation> BuildDayOfWeekRecommendations(IReadOnlyList<AnalyzedTrade> trades)
    {
        var byDay = trades
            .GroupBy(t => t.DayOfWeek)
            .Select(g =>
            {
                var wins = g.Count(t => t.IsWin);
                var avgWin = g.Where(t => t.IsWin).Select(t => t.NetPnL).DefaultIfEmpty(0).Average();
                var avgLoss = Math.Abs(g.Where(t => !t.IsWin).Select(t => t.NetPnL).DefaultIfEmpty(0).Average());
                return new
                {
                    Day = g.Key,
                    Trades = g.Count(),
                    WinRate = g.Count() > 0 ? (decimal)wins / g.Count() * 100 : 0,
                    Net = g.Sum(t => t.NetPnL),
                    Payoff = avgLoss > 0 ? avgWin / avgLoss : 0
                };
            })
            .Where(x => x.Trades >= _options.MinTradesPerBucket)
            .ToList();

        var friday = byDay.FirstOrDefault(x => x.Day == DayOfWeek.Friday && x.Net < 0);
        if (friday is not null)
        {
            yield return new PersonalRecommendation
            {
                Category = RecommendationCategory.Session,
                Title = "Sexta-feira",
                Summary = $"Drawdown elevado nas sextas: PnL acumulado {friday.Net:N2} em {friday.Trades} trades.",
                Action = "Reduzir exposição às sextas-feiras.",
                Confidence = ConfidenceFromSample(friday.Trades, 100 - friday.WinRate),
                Evidence = [$"Payoff {friday.Payoff:F2}"]
            };
        }
    }

    private static int ConfidenceFromSample(int sampleSize, decimal qualityPercent)
    {
        var sampleFactor = Math.Min(1m, sampleSize / 30m);
        var quality = Math.Clamp(qualityPercent, 0, 100) / 100m;
        return (int)Math.Clamp(50 + sampleFactor * 40 + quality * 10, 55, 98);
    }
}
