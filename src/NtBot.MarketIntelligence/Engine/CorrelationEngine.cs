using NtBot.MarketIntelligence.Configuration;
using NtBot.MarketIntelligence.Models;

namespace NtBot.MarketIntelligence.Engine;

public sealed class CorrelationEngine
{
    public CorrelationPairResult CalculatePair(
        string symbolA,
        string labelA,
        string symbolB,
        string labelB,
        IReadOnlyList<PriceHistoryPoint> seriesA,
        IReadOnlyList<PriceHistoryPoint> seriesB)
    {
        return new CorrelationPairResult
        {
            SymbolA = symbolA,
            SymbolB = symbolB,
            LabelA = labelA,
            LabelB = labelB,
            Correlation30D = RollingCorrelation(seriesA, seriesB, 30),
            Correlation60D = RollingCorrelation(seriesA, seriesB, 60),
            Correlation120D = RollingCorrelation(seriesA, seriesB, 120)
        };
    }

    public IReadOnlyList<AssetImpactResult> BuildAssetImpacts(
        IReadOnlyDictionary<string, IReadOnlyList<PriceHistoryPoint>> historyBySymbol)
    {
        var results = new List<AssetImpactResult>();

        foreach (var relation in MarketAssetRelations.All)
        {
            var factors = new List<ImpactFactor>();
            foreach (var (driverSymbol, label) in relation.Drivers)
            {
                if (!historyBySymbol.TryGetValue(driverSymbol, out var driverHistory))
                    continue;

                var assetHistory = ResolveAssetHistory(relation.Asset, historyBySymbol);
                if (assetHistory.Count < 20)
                    continue;

                var corr = RollingCorrelation(assetHistory, driverHistory, 60);
                var weight = ComputeDynamicWeight(relation.Asset, label, corr);
                factors.Add(new ImpactFactor
                {
                    Symbol = driverSymbol,
                    Label = label,
                    Correlation = corr,
                    Weight = weight
                });
            }

            if (factors.Count == 0)
                continue;

            var weightSum = factors.Sum(f => f.Weight);
            if (weightSum <= 0)
                weightSum = factors.Count;

            var impactScore = factors.Sum(f => f.Correlation * (f.Weight / weightSum));
            results.Add(new AssetImpactResult
            {
                Asset = relation.Asset,
                Factors = factors,
                ImpactScore = impactScore,
                BasketWeightPercent = relation.Asset == "WIN"
                    ? factors.Where(f => f.Weight > 0).Sum(f => f.Weight * Math.Max(0, f.Correlation)) * 100
                    : null,
                Recommendation = ClassifyRecommendation(impactScore)
            });
        }

        return results;
    }

    private static IReadOnlyList<PriceHistoryPoint> ResolveAssetHistory(
        string asset,
        IReadOnlyDictionary<string, IReadOnlyList<PriceHistoryPoint>> history)
    {
        if (history.TryGetValue(asset, out var direct) && direct.Count >= 20)
            return direct;

        // Fallback proxies apenas quando histórico B3 real indisponível
        return asset switch
        {
            "PETR4" when history.TryGetValue("CL=F", out var oil) => oil,
            "VALE3" when history.TryGetValue("HG=F", out var copper) => copper,
            "WIN" when history.TryGetValue("^GSPC", out var spx) => spx,
            _ => []
        };
    }

    private static double ComputeDynamicWeight(string asset, string label, double correlation)
    {
        if (asset != "WIN")
            return Math.Abs(correlation);

        // Peso dinâmico: correlação × liquidez relativa do componente (prior base por liquidez B3)
        var prior = label switch
        {
            "PETR4" => 0.22,
            "VALE3" => 0.18,
            "ITUB4" => 0.14,
            "BBDC4" => 0.12,
            "WEGE3" => 0.14,
            "ABEV3" => 0.10,
            _ => 0.05
        };

        return Math.Abs(correlation) * prior;
    }

    private static string ClassifyRecommendation(double score) => score switch
    {
        >= 0.65 => "Compra Forte",
        >= 0.35 => "Compra Moderada",
        <= -0.65 => "Venda Forte",
        <= -0.35 => "Venda Moderada",
        _ => "Neutral"
    };

    internal static double RollingCorrelation(
        IReadOnlyList<PriceHistoryPoint> a,
        IReadOnlyList<PriceHistoryPoint> b,
        int window)
    {
        var aligned = AlignSeries(a, b);
        if (aligned.a.Count < window + 1 || aligned.b.Count < window + 1)
            return 0;

        var returnsA = CalculateReturns(aligned.a);
        var returnsB = CalculateReturns(aligned.b);
        var take = Math.Min(returnsA.Count, returnsB.Count);
        if (take < window)
            return 0;

        var sliceA = returnsA.TakeLast(window).ToList();
        var sliceB = returnsB.TakeLast(window).ToList();
        return Pearson(sliceA, sliceB);
    }

    private static (List<PriceHistoryPoint> a, List<PriceHistoryPoint> b) AlignSeries(
        IReadOnlyList<PriceHistoryPoint> a,
        IReadOnlyList<PriceHistoryPoint> b)
    {
        var dictB = b.GroupBy(p => p.Date.Date).ToDictionary(g => g.Key, g => g.Last());
        var alignedA = new List<PriceHistoryPoint>();
        var alignedB = new List<PriceHistoryPoint>();

        foreach (var point in a.OrderBy(p => p.Date))
        {
            if (!dictB.TryGetValue(point.Date.Date, out var match))
                continue;

            alignedA.Add(point);
            alignedB.Add(match);
        }

        return (alignedA, alignedB);
    }

    private static List<decimal> CalculateReturns(IReadOnlyList<PriceHistoryPoint> series)
    {
        var returns = new List<decimal>();
        for (var i = 1; i < series.Count; i++)
        {
            var prev = series[i - 1].Close;
            if (prev == 0) continue;
            returns.Add((series[i].Close - prev) / prev);
        }

        return returns;
    }

    private static double Pearson(IReadOnlyList<decimal> x, IReadOnlyList<decimal> y)
    {
        if (x.Count != y.Count || x.Count == 0)
            return 0;

        var xs = x.Select(v => (double)v).ToArray();
        var ys = y.Select(v => (double)v).ToArray();
        var avgX = xs.Average();
        var avgY = ys.Average();

        double sumNum = 0, sumDenX = 0, sumDenY = 0;
        for (var i = 0; i < xs.Length; i++)
        {
            var dx = xs[i] - avgX;
            var dy = ys[i] - avgY;
            sumNum += dx * dy;
            sumDenX += dx * dx;
            sumDenY += dy * dy;
        }

        var den = Math.Sqrt(sumDenX * sumDenY);
        return den == 0 ? 0 : sumNum / den;
    }
}
