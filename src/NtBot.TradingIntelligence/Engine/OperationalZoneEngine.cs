using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine;

public sealed class OperationalZoneEngine : IOperationalZoneEngine
{
    public IReadOnlyList<OperationalZone> BuildZones(
        string asset,
        ConfluenceScoreResult confluence,
        IReadOnlyList<TimeframeAnalysis> timeframes,
        IReadOnlyList<TimeframeIntersection> intersections)
    {
        var zones = new List<OperationalZone>();

        foreach (var intersection in intersections.Where(i => i.HighConfluence))
        {
            zones.Add(new OperationalZone
            {
                Type = confluence.Score >= 55 ? OperationalZoneType.StrongBuy : OperationalZoneType.ModerateBuy,
                Label = $"Alta Confluência {intersection.Pair}",
                PriceLow = intersection.PriceLow,
                PriceHigh = intersection.PriceHigh,
                ConfluenceScore = intersection.ConfluenceScore,
                Sources = ["Wyckoff", "SMC", "Volume", "Drivers", "Macro"],
                Timeframe = intersection.Pair,
                Description = $"Interseção {intersection.Pair} com score {intersection.ConfluenceScore}."
            });
        }

        foreach (var tf in timeframes)
        {
            var tfScore = (tf.WyckoffScore + tf.SmcScore + tf.VolumeScore) / 3;
            if (tfScore >= 70)
            {
                zones.Add(new OperationalZone
                {
                    Type = OperationalZoneType.StrongBuy,
                    Label = $"Zona Compradora {tf.Timeframe}min",
                    PriceLow = tf.Mid,
                    PriceHigh = tf.High,
                    ConfluenceScore = tfScore,
                    Sources = ["Wyckoff", "SMC", "Volume"],
                    Timeframe = tf.Timeframe,
                    Description = $"Região 50%-máxima em {tf.Timeframe}min."
                });
            }
            else if (tfScore <= 30)
            {
                zones.Add(new OperationalZone
                {
                    Type = OperationalZoneType.StrongSell,
                    Label = $"Zona Vendedora {tf.Timeframe}min",
                    PriceLow = tf.Low,
                    PriceHigh = tf.Mid,
                    ConfluenceScore = tfScore,
                    Sources = ["Wyckoff", "SMC", "Volume"],
                    Timeframe = tf.Timeframe,
                    Description = $"Região mínima-50% em {tf.Timeframe}min."
                });
            }
            else if (tfScore is > 30 and < 70)
            {
                zones.Add(new OperationalZone
                {
                    Type = OperationalZoneType.Neutral,
                    Label = $"Zona Neutra {tf.Timeframe}min",
                    PriceLow = tf.Low,
                    PriceHigh = tf.High,
                    ConfluenceScore = tfScore,
                    Sources = ["Wyckoff", "SMC"],
                    Timeframe = tf.Timeframe,
                    Description = $"Range operacional em {tf.Timeframe}min."
                });
            }
        }

        if (zones.Count == 0 && confluence.Score >= 55)
        {
            zones.Add(new OperationalZone
            {
                Type = OperationalZoneType.ModerateBuy,
                Label = "Confluência Macro + Drivers",
                ConfluenceScore = confluence.Score,
                Sources = ["Macro", "Drivers", "Momentum"],
                Description = $"Confluence Score {confluence.Score} sem interseção de timeframe definida."
            });
        }

        return zones;
    }
}

public static class TimeframeIntersectionEngine
{
    private const decimal MinOverlapRatio = 0.08m;

    public static IReadOnlyList<TimeframeIntersection> Calculate(IReadOnlyList<TimeframeAnalysis> timeframes)
    {
        var pairs = new (string a, string b)[]
        {
            ("5", "15"), ("5", "30"), ("5", "60"),
            ("15", "30"), ("15", "60"), ("30", "60")
        };

        var map = timeframes.ToDictionary(t => t.Timeframe, t => t);
        var results = new List<TimeframeIntersection>();

        foreach (var (a, b) in pairs)
        {
            if (!map.TryGetValue(a, out var ta) || !map.TryGetValue(b, out var tb))
                continue;

            var overlapLow = Math.Max(ta.Low, tb.Low);
            var overlapHigh = Math.Min(ta.High, tb.High);
            if (overlapHigh <= overlapLow)
                continue;

            var rangeA = ta.High - ta.Low;
            var rangeB = tb.High - tb.Low;
            if (rangeA <= 0 || rangeB <= 0)
                continue;

            var overlapSize = overlapHigh - overlapLow;
            var overlapRatio = overlapSize / Math.Min(rangeA, rangeB);
            if (overlapRatio < MinOverlapRatio)
                continue;

            var engineScore = (ta.WyckoffScore + tb.WyckoffScore + ta.SmcScore + tb.SmcScore + ta.VolumeScore + tb.VolumeScore) / 6;
            var alignmentBonus = ScoreAlignmentBonus(ta, tb);
            var overlapBonus = (int)Math.Clamp(overlapRatio * 20m, 0, 15);
            var score = (int)Math.Clamp(engineScore + alignmentBonus + overlapBonus, 0, 100);

            results.Add(new TimeframeIntersection
            {
                Pair = $"{a}x{b}",
                PriceLow = overlapLow,
                PriceHigh = overlapHigh,
                ConfluenceScore = score,
                HighConfluence = score >= 70 && overlapRatio >= 0.15m
            });
        }

        return results.OrderByDescending(r => r.ConfluenceScore).ToList();
    }

    private static int ScoreAlignmentBonus(TimeframeAnalysis a, TimeframeAnalysis b)
    {
        var bonus = 0;
        if (BothStrong(a.WyckoffScore, b.WyckoffScore)) bonus += 4;
        if (BothStrong(a.SmcScore, b.SmcScore)) bonus += 4;
        if (BothStrong(a.VolumeScore, b.VolumeScore)) bonus += 2;
        if (BothWeak(a.WyckoffScore, b.WyckoffScore)) bonus -= 3;
        return bonus;
    }

    private static bool BothStrong(int a, int b) => a >= 65 && b >= 65;
    private static bool BothWeak(int a, int b) => a <= 35 && b <= 35;
}
