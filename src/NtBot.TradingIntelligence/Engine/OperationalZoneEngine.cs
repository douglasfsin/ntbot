using NtBot.Shared.MarketData;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine;

public sealed class OperationalZoneEngine : IOperationalZoneEngine
{
    private const int MaxZones = 10;
    private const decimal HeavyOverlapRatio = 0.55m;

    public IReadOnlyList<OperationalZone> BuildZones(
        string asset,
        ConfluenceScoreResult confluence,
        IReadOnlyList<TimeframeAnalysis> timeframes,
        IReadOnlyList<TimeframeIntersection> intersections)
    {
        var zones = new List<OperationalZone>();

        // High-signal confluence pockets only (top 2) — avoids stacking every TF pair.
        foreach (var intersection in intersections
                     .Where(i => i.HighConfluence && i.ConfluenceScore >= 70)
                     .OrderByDescending(i => i.ConfluenceScore)
                     .Take(2))
        {
            zones.Add(new OperationalZone
            {
                Type = confluence.Score >= 55 ? OperationalZoneType.StrongBuy : OperationalZoneType.ModerateBuy,
                Label = $"FVG / Confluência {intersection.Pair}",
                PriceLow = intersection.PriceLow,
                PriceHigh = intersection.PriceHigh,
                ConfluenceScore = intersection.ConfluenceScore,
                Sources = ["Wyckoff", "SMC", "Volume", "FVG"],
                Timeframe = intersection.Pair,
                Description = $"Interseção {intersection.Pair} — zona de fair value gap / confluência."
            });
        }

        if (timeframes.Count > 0)
        {
            var vwap = timeframes.Average(t => t.Mid);
            var avgRange = timeframes.Average(t => Math.Max(0m, t.High - t.Low));
            // Tight VWAP band (not min–max of all TFs) so the chart stays readable.
            var vwapHalf = Math.Max(RelativeBand(asset, vwap, 0.0004m), avgRange * 0.025m);
            zones.Add(new OperationalZone
            {
                Type = confluence.Score >= 55 ? OperationalZoneType.ModerateBuy : OperationalZoneType.Neutral,
                Label = "VWAP (proxy multi-TF)",
                PriceLow = vwap - vwapHalf,
                PriceHigh = vwap + vwapHalf,
                ConfluenceScore = Math.Max(40, confluence.Score - 5),
                Sources = ["Trend", "Volume"],
                Timeframe = "multi",
                Description = $"Referência VWAP ~{FormatPrice(asset, vwap)} agregada dos timeframes."
            });

            var sessionHigh = timeframes.Max(t => t.High);
            var sessionLow = timeframes.Min(t => t.Low);
            var liqHalf = RelativeBand(asset, sessionHigh, 0.0008m);
            zones.Add(new OperationalZone
            {
                Type = OperationalZoneType.Neutral,
                Label = "Liquidez — topo de sessão",
                PriceLow = sessionHigh - liqHalf,
                PriceHigh = sessionHigh + liqHalf,
                ConfluenceScore = Math.Max(35, confluence.Score - 15),
                Sources = ["SMC", "Liquidez"],
                Timeframe = "session",
                Description = "Pool de liquidez acima do topo — alvo de stops institucionais."
            });
            zones.Add(new OperationalZone
            {
                Type = OperationalZoneType.Neutral,
                Label = "Liquidez — fundo de sessão",
                PriceLow = sessionLow - liqHalf,
                PriceHigh = sessionLow + liqHalf,
                ConfluenceScore = Math.Max(35, confluence.Score - 15),
                Sources = ["SMC", "Liquidez"],
                Timeframe = "session",
                Description = "Pool de liquidez abaixo do fundo — sweep provável."
            });

            var poc = timeframes
                .OrderByDescending(t => t.VolumeScore)
                .ThenByDescending(t => t.WyckoffScore + t.SmcScore)
                .First();
            var pocRange = Math.Max((poc.High - poc.Low) * 0.12m, RelativeBand(asset, poc.Mid, 0.0006m));
            zones.Add(new OperationalZone
            {
                Type = OperationalZoneType.Neutral,
                Label = $"POC / Área de valor ({poc.Timeframe}min)",
                PriceLow = poc.Mid - pocRange,
                PriceHigh = poc.Mid + pocRange,
                ConfluenceScore = (poc.WyckoffScore + poc.SmcScore + poc.VolumeScore) / 3,
                Sources = ["Volume", "Wyckoff"],
                Timeframe = poc.Timeframe,
                Description = "Point of Control — região de maior negociação relativa."
            });

            // Demand / Supply only for decisive TFs (skip mid-score full-range "Value Area" clutter).
            foreach (var tf in timeframes
                         .Select(t => (Tf: t, Score: (t.WyckoffScore + t.SmcScore + t.VolumeScore) / 3))
                         .Where(x => x.Score >= 65)
                         .OrderByDescending(x => x.Score)
                         .Take(2))
            {
                zones.Add(new OperationalZone
                {
                    Type = tf.Score >= 75 ? OperationalZoneType.StrongBuy : OperationalZoneType.ModerateBuy,
                    Label = $"Demanda / Order Block {tf.Tf.Timeframe}min",
                    PriceLow = tf.Tf.Low,
                    PriceHigh = tf.Tf.Mid,
                    ConfluenceScore = tf.Score,
                    Sources = ["SMC", "Wyckoff", "Order Block"],
                    Timeframe = tf.Tf.Timeframe,
                    Description = $"Zona de demanda institucional em {tf.Tf.Timeframe}min."
                });
            }

            foreach (var tf in timeframes
                         .Select(t => (Tf: t, Score: (t.WyckoffScore + t.SmcScore + t.VolumeScore) / 3))
                         .Where(x => x.Score <= 35)
                         .OrderBy(x => x.Score)
                         .Take(2))
            {
                zones.Add(new OperationalZone
                {
                    Type = tf.Score <= 25 ? OperationalZoneType.StrongSell : OperationalZoneType.ModerateSell,
                    Label = $"Oferta / Order Block {tf.Tf.Timeframe}min",
                    PriceLow = tf.Tf.Mid,
                    PriceHigh = tf.Tf.High,
                    ConfluenceScore = 100 - tf.Score,
                    Sources = ["SMC", "Wyckoff", "Order Block"],
                    Timeframe = tf.Tf.Timeframe,
                    Description = $"Zona de oferta institucional em {tf.Tf.Timeframe}min."
                });
            }
        }

        if (confluence.Score >= 70)
        {
            var bullishZones = zones
                .Where(z => z.Type is OperationalZoneType.StrongBuy or OperationalZoneType.ModerateBuy)
                .ToList();
            if (bullishZones.Count > 0)
            {
                var targetHigh = bullishZones.Max(z => z.PriceHigh);
                var targetPad = RelativeBand(asset, targetHigh, 0.0025m);
                zones.Add(new OperationalZone
                {
                    Type = OperationalZoneType.ModerateBuy,
                    Label = "Alvo institucional",
                    PriceLow = targetHigh,
                    PriceHigh = targetHigh + targetPad,
                    ConfluenceScore = confluence.Score,
                    Sources = ["Confluence", "Liquidez"],
                    Timeframe = "target",
                    Description = "Projeção de alvo com base nas zonas compradoras."
                });
            }
        }

        return RankAndDedupe(zones, asset);
    }

    private static IReadOnlyList<OperationalZone> RankAndDedupe(List<OperationalZone> zones, string asset)
    {
        var ranked = zones
            .GroupBy(z => $"{NormalizeLabel(z.Label)}|{z.PriceLow:F4}|{z.PriceHigh:F4}")
            .Select(g => g.First())
            .OrderByDescending(z => z.ConfluenceScore)
            .ThenByDescending(ZonePriority)
            .ToList();

        var result = new List<OperationalZone>();
        foreach (var zone in ranked)
        {
            if (zone.PriceHigh < zone.PriceLow)
                continue;
            if (result.Any(existing => OverlapsHeavily(existing, zone)))
                continue;

            result.Add(zone);
            if (result.Count >= MaxZones)
                break;
        }

        return result;
    }

    private static int ZonePriority(OperationalZone z)
    {
        var label = z.Label ?? "";
        if (label.Contains("FVG", StringComparison.OrdinalIgnoreCase)
            || label.Contains("Demand", StringComparison.OrdinalIgnoreCase)
            || label.Contains("Demanda", StringComparison.OrdinalIgnoreCase)
            || label.Contains("Supply", StringComparison.OrdinalIgnoreCase)
            || label.Contains("Oferta", StringComparison.OrdinalIgnoreCase))
            return 5;
        if (label.Contains("POC", StringComparison.OrdinalIgnoreCase))
            return 4;
        if (label.Contains("Target", StringComparison.OrdinalIgnoreCase)
            || label.Contains("Alvo", StringComparison.OrdinalIgnoreCase))
            return 3;
        if (label.Contains("Liquidez", StringComparison.OrdinalIgnoreCase))
            return 2;
        if (label.Contains("VWAP", StringComparison.OrdinalIgnoreCase))
            return 1;
        return z.Type switch
        {
            OperationalZoneType.StrongBuy or OperationalZoneType.StrongSell => 4,
            OperationalZoneType.ModerateBuy or OperationalZoneType.ModerateSell => 3,
            _ => 0
        };
    }

    private static bool OverlapsHeavily(OperationalZone a, OperationalZone b)
    {
        var overlapLow = Math.Max(a.PriceLow, b.PriceLow);
        var overlapHigh = Math.Min(a.PriceHigh, b.PriceHigh);
        if (overlapHigh <= overlapLow)
            return false;

        var overlap = overlapHigh - overlapLow;
        var smaller = Math.Min(a.PriceHigh - a.PriceLow, b.PriceHigh - b.PriceLow);
        if (smaller <= 0)
            return Math.Abs(Mid(a) - Mid(b)) / Math.Max(Math.Abs(Mid(a)), 1m) < 0.001m;

        return overlap / smaller >= HeavyOverlapRatio;
    }

    private static decimal Mid(OperationalZone z) => (z.PriceLow + z.PriceHigh) / 2m;

    private static decimal RelativeBand(string asset, decimal price, decimal fraction)
    {
        var baseBand = Math.Abs(price) * fraction;
        // XAUUSD: keep bands readable in dollars (min ~0.30).
        if (IsXau(asset))
            return Math.Max(baseBand, 0.30m);
        return Math.Max(baseBand, Math.Abs(price) * 0.00005m);
    }

    private static bool IsXau(string asset)
    {
        var a = (asset ?? "").Trim().ToUpperInvariant();
        return a is "XAUUSD" or "XAU" or "GOLD";
    }

    private static string FormatPrice(string asset, decimal price) =>
        IsXau(asset) ? price.ToString("F2") : price.ToString("F0");

    private static string NormalizeLabel(string label) =>
        (label ?? "").Trim().ToUpperInvariant();
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

        // Chart keys (5/M5 → "5") — tolerates duplicate / alias entries from ResolveAnalysisTimeframes.
        var map = timeframes
            .GroupBy(t => ChartTimeframe.ToChartKey(t.Timeframe), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
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
