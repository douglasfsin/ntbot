using NtBot.Domain.Entities;

namespace NtBot.TradingIntelligence.Engine;

public enum SmcStructureBias
{
    Neutral,
    Bullish,
    Bearish
}

public sealed class SmcAnalysisResult
{
    public int Score { get; init; } = 50;
    public SmcStructureBias Bias { get; init; } = SmcStructureBias.Neutral;
    public bool BullishBos { get; init; }
    public bool BearishBos { get; init; }
    public bool BullishChoch { get; init; }
    public bool BearishChoch { get; init; }
    public int BullishOrderBlocks { get; init; }
    public int BearishOrderBlocks { get; init; }
    public int BullishFvgs { get; init; }
    public int BearishFvgs { get; init; }
    public decimal? ActiveZoneLow { get; init; }
    public decimal? ActiveZoneHigh { get; init; }
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<SmcChartZone> Overlays { get; init; } = [];
}

public sealed class SmcChartZone
{
    public string Type { get; init; } = string.Empty;
    public decimal PriceLow { get; init; }
    public decimal PriceHigh { get; init; }
    public string Label { get; init; } = string.Empty;
}

public interface ISmcEngine
{
    SmcAnalysisResult Analyze(IReadOnlyList<Candle> candles);
}

/// <summary>
/// Smart Money Concepts — estrutura, order blocks, FVG e BOS/CHoCH.
/// </summary>
public sealed class SmcEngine : ISmcEngine
{
    private const int SwingLookback = 2;
    private const int MinCandles = 20;

    public SmcAnalysisResult Analyze(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < MinCandles)
            return new SmcAnalysisResult { Summary = "Dados insuficientes para SMC." };

        var ordered = candles.OrderBy(c => c.OpenTime).ToList();
        var swings = FindSwingPoints(ordered);
        var bias = DetermineStructureBias(swings, ordered);
        var (bullOb, bearOb) = FindOrderBlocks(ordered);
        var (bullFvg, bearFvg) = FindFairValueGaps(ordered);
        var (bullBos, bearBos, bullChoch, bearChoch) = DetectBreaks(swings, ordered, bias);

        var lastClose = ordered[^1].Close;
        var activeOb = FindNearestOrderBlock(bullOb, bearOb, lastClose, bias);
        var overlays = BuildOverlays(bullOb, bearOb, bullFvg, bearFvg);

        var score = 50;
        score += bias switch
        {
            SmcStructureBias.Bullish => 12,
            SmcStructureBias.Bearish => -12,
            _ => 0
        };
        if (bullBos && bias == SmcStructureBias.Bullish) score += 10;
        if (bearBos && bias == SmcStructureBias.Bearish) score += 10;
        if (bullChoch) score += bias == SmcStructureBias.Bearish ? 8 : -4;
        if (bearChoch) score += bias == SmcStructureBias.Bullish ? -4 : 8;
        score += Math.Min(bullFvg.Count, 3) * 2;
        score -= Math.Min(bearFvg.Count, 3) * (bias == SmcStructureBias.Bullish ? 2 : 0);
        if (activeOb is not null && lastClose >= activeOb.Value.Low && lastClose <= activeOb.Value.High)
            score += bias == SmcStructureBias.Bullish ? 8 : -8;

        score = (int)Math.Clamp(score, 0, 100);

        var summary = BuildSummary(bias, bullBos, bearBos, bullChoch, bearChoch, bullOb.Count, bearOb.Count, bullFvg.Count, bearFvg.Count);

        return new SmcAnalysisResult
        {
            Score = score,
            Bias = bias,
            BullishBos = bullBos,
            BearishBos = bearBos,
            BullishChoch = bullChoch,
            BearishChoch = bearChoch,
            BullishOrderBlocks = bullOb.Count,
            BearishOrderBlocks = bearOb.Count,
            BullishFvgs = bullFvg.Count,
            BearishFvgs = bearFvg.Count,
            ActiveZoneLow = activeOb?.Low,
            ActiveZoneHigh = activeOb?.High,
            Summary = summary,
            Overlays = overlays
        };
    }

    private static List<(int Index, decimal Price, bool IsHigh)> FindSwingPoints(IReadOnlyList<Candle> candles)
    {
        var swings = new List<(int Index, decimal Price, bool IsHigh)>();
        for (var i = SwingLookback; i < candles.Count - SwingLookback; i++)
        {
            var isHigh = true;
            var isLow = true;
            for (var j = 1; j <= SwingLookback; j++)
            {
                if (candles[i].High <= candles[i - j].High || candles[i].High <= candles[i + j].High)
                    isHigh = false;
                if (candles[i].Low >= candles[i - j].Low || candles[i].Low >= candles[i + j].Low)
                    isLow = false;
            }

            if (isHigh) swings.Add((i, candles[i].High, true));
            if (isLow) swings.Add((i, candles[i].Low, false));
        }

        return swings;
    }

    private static SmcStructureBias DetermineStructureBias(
        IReadOnlyList<(int Index, decimal Price, bool IsHigh)> swings,
        IReadOnlyList<Candle> candles)
    {
        var highs = swings.Where(s => s.IsHigh).TakeLast(3).Select(s => s.Price).ToList();
        var lows = swings.Where(s => !s.IsHigh).TakeLast(3).Select(s => s.Price).ToList();

        if (highs.Count >= 2 && lows.Count >= 2)
        {
            var hh = highs[^1] > highs[^2];
            var hl = lows[^1] > lows[^2];
            var lh = highs[^1] < highs[^2];
            var ll = lows[^1] < lows[^2];

            if (hh && hl) return SmcStructureBias.Bullish;
            if (lh && ll) return SmcStructureBias.Bearish;
        }

        var last = candles[^1];
        var prev = candles[^10];
        return last.Close > prev.Close ? SmcStructureBias.Bullish
            : last.Close < prev.Close ? SmcStructureBias.Bearish
            : SmcStructureBias.Neutral;
    }

    private static (List<(decimal Low, decimal High)> Bullish, List<(decimal Low, decimal High)> Bearish) FindOrderBlocks(
        IReadOnlyList<Candle> candles)
    {
        var bullish = new List<(decimal Low, decimal High)>();
        var bearish = new List<(decimal Low, decimal High)>();
        var avgBody = candles.Average(c => Math.Abs(c.Close - c.Open));

        for (var i = 1; i < candles.Count - 2; i++)
        {
            var prev = candles[i - 1];
            var curr = candles[i];
            var next = candles[i + 1];

            var impulseUp = next.Close - next.Open > avgBody * 1.2m && next.Close > curr.High;
            var impulseDown = next.Open - next.Close > avgBody * 1.2m && next.Close < curr.Low;

            if (impulseUp && curr.Close < curr.Open)
                bullish.Add((curr.Low, curr.High));

            if (impulseDown && curr.Close > curr.Open)
                bearish.Add((curr.Low, curr.High));
        }

        return (bullish.TakeLast(5).ToList(), bearish.TakeLast(5).ToList());
    }

    private static (List<(decimal Low, decimal High)> Bullish, List<(decimal Low, decimal High)> Bearish) FindFairValueGaps(
        IReadOnlyList<Candle> candles)
    {
        var bullish = new List<(decimal Low, decimal High)>();
        var bearish = new List<(decimal Low, decimal High)>();

        for (var i = 1; i < candles.Count - 1; i++)
        {
            var left = candles[i - 1];
            var mid = candles[i];
            var right = candles[i + 1];

            if (left.High < right.Low)
                bullish.Add((left.High, right.Low));

            if (left.Low > right.High)
                bearish.Add((right.High, left.Low));
        }

        return (bullish.TakeLast(5).ToList(), bearish.TakeLast(5).ToList());
    }

    private static (bool BullBos, bool BearBos, bool BullChoch, bool BearChoch) DetectBreaks(
        IReadOnlyList<(int Index, decimal Price, bool IsHigh)> swings,
        IReadOnlyList<Candle> candles,
        SmcStructureBias bias)
    {
        var lastHigh = swings.LastOrDefault(s => s.IsHigh).Price;
        var lastLow = swings.LastOrDefault(s => !s.IsHigh).Price;
        var close = candles[^1].Close;

        var bullBos = lastHigh > 0 && close > lastHigh;
        var bearBos = lastLow > 0 && close < lastLow;
        var bullChoch = bearBos && bias == SmcStructureBias.Bullish;
        var bearChoch = bullBos && bias == SmcStructureBias.Bearish;

        return (bullBos, bearBos, bullChoch, bearChoch);
    }

    private static (decimal Low, decimal High)? FindNearestOrderBlock(
        IReadOnlyList<(decimal Low, decimal High)> bull,
        IReadOnlyList<(decimal Low, decimal High)> bear,
        decimal price,
        SmcStructureBias bias)
    {
        var pool = bias == SmcStructureBias.Bearish ? bear : bull;
        if (pool.Count == 0) pool = bull.Concat(bear).ToList();

        if (pool.Count == 0)
            return null;

        return pool
            .OrderBy(z => Math.Min(Math.Abs(price - z.Low), Math.Abs(price - z.High)))
            .First();
    }

    private static string BuildSummary(
        SmcStructureBias bias,
        bool bullBos,
        bool bearBos,
        bool bullChoch,
        bool bearChoch,
        int bullOb,
        int bearOb,
        int bullFvg,
        int bearFvg)
    {
        var parts = new List<string>
        {
            bias switch
            {
                SmcStructureBias.Bullish => "Estrutura bullish (HH/HL)",
                SmcStructureBias.Bearish => "Estrutura bearish (LH/LL)",
                _ => "Estrutura lateral"
            }
        };

        if (bullBos) parts.Add("BOS bullish");
        if (bearBos) parts.Add("BOS bearish");
        if (bullChoch) parts.Add("CHoCH bullish");
        if (bearChoch) parts.Add("CHoCH bearish");
        if (bullOb > 0) parts.Add($"{bullOb} OB comprador");
        if (bearOb > 0) parts.Add($"{bearOb} OB vendedor");
        if (bullFvg > 0) parts.Add($"{bullFvg} FVG bullish");
        if (bearFvg > 0) parts.Add($"{bearFvg} FVG bearish");

        return string.Join(" · ", parts);
    }

    private static List<SmcChartZone> BuildOverlays(
        IReadOnlyList<(decimal Low, decimal High)> bullOb,
        IReadOnlyList<(decimal Low, decimal High)> bearOb,
        IReadOnlyList<(decimal Low, decimal High)> bullFvg,
        IReadOnlyList<(decimal Low, decimal High)> bearFvg)
    {
        var overlays = new List<SmcChartZone>();
        var obIdx = 1;
        foreach (var z in bullOb.TakeLast(3))
        {
            overlays.Add(new SmcChartZone
            {
                Type = "OrderBlockBuy",
                PriceLow = z.Low,
                PriceHigh = z.High,
                Label = $"OB Compra {obIdx++}"
            });
        }
        obIdx = 1;
        foreach (var z in bearOb.TakeLast(3))
        {
            overlays.Add(new SmcChartZone
            {
                Type = "OrderBlockSell",
                PriceLow = z.Low,
                PriceHigh = z.High,
                Label = $"OB Venda {obIdx++}"
            });
        }
        var fvgIdx = 1;
        foreach (var z in bullFvg.TakeLast(2))
        {
            overlays.Add(new SmcChartZone
            {
                Type = "FvgBuy",
                PriceLow = z.Low,
                PriceHigh = z.High,
                Label = $"FVG ↑ {fvgIdx++}"
            });
        }
        fvgIdx = 1;
        foreach (var z in bearFvg.TakeLast(2))
        {
            overlays.Add(new SmcChartZone
            {
                Type = "FvgSell",
                PriceLow = z.Low,
                PriceHigh = z.High,
                Label = $"FVG ↓ {fvgIdx++}"
            });
        }

        return overlays;
    }
}
