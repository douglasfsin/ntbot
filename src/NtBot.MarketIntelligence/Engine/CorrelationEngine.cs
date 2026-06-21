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

                var assetHistory = ResolveAssetProxyHistory(relation.Asset, historyBySymbol);
                if (assetHistory.Count < 20)
                    continue;

                var corr = RollingCorrelation(assetHistory, driverHistory, 60);
                factors.Add(new ImpactFactor
                {
                    Symbol = driverSymbol,
                    Label = label,
                    Correlation = corr,
                    Weight = relation.Asset == "WIN" ? GetWinWeight(label) : 0
                });
            }

            if (factors.Count == 0)
                continue;

            var impactScore = factors.Average(f => f.Correlation * (f.Weight > 0 ? f.Weight : 1));
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

    private static IReadOnlyList<PriceHistoryPoint> ResolveAssetProxyHistory(
        string asset,
        IReadOnlyDictionary<string, IReadOnlyList<PriceHistoryPoint>> history)
    {
        // Proxies until B3 equity history is available from another provider.
        return asset switch
        {
            "PETR4" when history.TryGetValue("CL=F", out var oil) => oil,
            "VALE3" when history.TryGetValue("HG=F", out var copper) => copper,
            "WIN" when history.TryGetValue("^GSPC", out var spx) => spx,
            _ => []
        };
    }

    private static double GetWinWeight(string label) => label switch
    {
        "PETR4" => 0.18,
        "VALE3" => 0.12,
        "ITUB4" => 0.10,
        "BBDC4" => 0.08,
        "ABEV3" => 0.07,
        "WEGE3" => 0.10,
        _ => 0
    };

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
