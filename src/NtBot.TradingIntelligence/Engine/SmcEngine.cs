using NtBot.Domain.Entities;
using NtBot.TradingIntelligence.Models;

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
    public string PremiumDiscountZone { get; init; } = "Equilibrium";
    public bool InOteZone { get; init; }
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<SmcChartZone> Overlays { get; init; } = [];
    public IReadOnlyList<SmcStructureEvent> Events { get; init; } = [];
    public IReadOnlyList<SmcDetection> Detections { get; init; } = [];
}

public sealed class SmcStructureEvent
{
    public string Type { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public decimal? PriceLevel { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Severity { get; init; } = "Info";
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
        var events = DetectStructureEvents(swings, ordered, bullBos, bearBos, bullChoch, bearChoch);

        var lastClose = ordered[^1].Close;
        var activeOb = FindNearestOrderBlock(bullOb, bearOb, lastClose, bias);
        var (premiumDiscount, inOte, rangeLow, rangeHigh) = ResolvePremiumDiscountOte(ordered, lastClose);
        var overlays = BuildOverlays(bullOb, bearOb, bullFvg, bearFvg, rangeLow, rangeHigh, premiumDiscount, inOte);
        var detections = BuildDetections(
            ordered, bias, bullBos, bearBos, bullChoch, bearChoch,
            bullOb, bearOb, bullFvg, bearFvg, events, premiumDiscount, inOte, lastClose);

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

        if (premiumDiscount == "Discount" && bias == SmcStructureBias.Bullish) score += 6;
        if (premiumDiscount == "Premium" && bias == SmcStructureBias.Bearish) score += 6;
        if (premiumDiscount == "Premium" && bias == SmcStructureBias.Bullish) score -= 4;
        if (premiumDiscount == "Discount" && bias == SmcStructureBias.Bearish) score -= 4;
        if (inOte && bias == SmcStructureBias.Bullish) score += 5;
        if (inOte && bias == SmcStructureBias.Bearish) score += 5;

        score = (int)Math.Clamp(score, 0, 100);

        var summary = BuildSummary(
            bias, bullBos, bearBos, bullChoch, bearChoch,
            bullOb.Count, bearOb.Count, bullFvg.Count, bearFvg.Count,
            premiumDiscount, inOte);

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
            PremiumDiscountZone = premiumDiscount,
            InOteZone = inOte,
            Summary = summary,
            Overlays = overlays,
            Events = events,
            Detections = detections
        };
    }

    private static List<SmcStructureEvent> DetectStructureEvents(
        IReadOnlyList<(int Index, decimal Price, bool IsHigh)> swings,
        IReadOnlyList<Candle> candles,
        bool bullBos,
        bool bearBos,
        bool bullChoch,
        bool bearChoch)
    {
        var events = new List<SmcStructureEvent>();
        var lastSwingHigh = swings.LastOrDefault(s => s.IsHigh);
        var lastSwingLow = swings.LastOrDefault(s => !s.IsHigh);
        var scanFrom = Math.Max(SwingLookback + 1, candles.Count - 20);

        for (var i = scanFrom; i < candles.Count; i++)
        {
            var candle = candles[i];
            var priorHigh = ResolvePriorSwingPrice(swings, candles, i, isHigh: true);
            var priorLow = ResolvePriorSwingPrice(swings, candles, i, isHigh: false);
            var range = candle.High - candle.Low;
            if (range <= 0)
                continue;

            if (candle.Low < priorLow && candle.Close > priorLow)
            {
                var wickRatio = (candle.Close - candle.Low) / range;
                if (wickRatio >= 0.6m)
                {
                    events.Add(new SmcStructureEvent
                    {
                        Type = "LiquiditySweep",
                        Timestamp = candle.OpenTime,
                        PriceLevel = priorLow,
                        Title = "Sweep de liquidez (bullish)",
                        Description = "Varredura abaixo do swing low com fechamento de volta — setup tipo Spring",
                        Severity = "Bullish"
                    });
                }
            }

            if (candle.High > priorHigh && candle.Close < priorHigh)
            {
                var wickRatio = (candle.High - candle.Close) / range;
                if (wickRatio >= 0.6m)
                {
                    events.Add(new SmcStructureEvent
                    {
                        Type = "LiquiditySweep",
                        Timestamp = candle.OpenTime,
                        PriceLevel = priorHigh,
                        Title = "Sweep de liquidez (bearish)",
                        Description = "Varredura acima do swing high com fechamento de volta — setup tipo Upthrust",
                        Severity = "Bearish"
                    });
                }
            }
        }

        var last = candles[^1];
        if (bullBos)
        {
            events.Add(new SmcStructureEvent
            {
                Type = "BOS",
                Timestamp = last.OpenTime,
                PriceLevel = lastSwingHigh.Price,
                Title = "Break of Structure bullish",
                Description = "Fechamento acima do último swing high — continuação de alta",
                Severity = "Bullish"
            });
        }

        if (bearBos)
        {
            events.Add(new SmcStructureEvent
            {
                Type = "BOS",
                Timestamp = last.OpenTime,
                PriceLevel = lastSwingLow.Price,
                Title = "Break of Structure bearish",
                Description = "Fechamento abaixo do último swing low — continuação de baixa",
                Severity = "Bearish"
            });
        }

        if (bullChoch)
        {
            events.Add(new SmcStructureEvent
            {
                Type = "CHoCH",
                Timestamp = last.OpenTime,
                PriceLevel = last.Close,
                Title = "Change of Character bullish",
                Description = "Quebra estrutural de baixa para alta — possível reversão",
                Severity = "Warning"
            });
        }

        if (bearChoch)
        {
            events.Add(new SmcStructureEvent
            {
                Type = "CHoCH",
                Timestamp = last.OpenTime,
                PriceLevel = last.Close,
                Title = "Change of Character bearish",
                Description = "Quebra estrutural de alta para baixa — possível reversão",
                Severity = "Warning"
            });
        }

        return events
            .GroupBy(e => $"{e.Type}:{e.Timestamp:O}:{e.Title}")
            .Select(g => g.First())
            .OrderByDescending(e => e.Timestamp)
            .Take(10)
            .ToList();
    }

    private static decimal ResolvePriorSwingPrice(
        IReadOnlyList<(int Index, decimal Price, bool IsHigh)> swings,
        IReadOnlyList<Candle> candles,
        int index,
        bool isHigh)
    {
        var swing = swings.Where(s => s.IsHigh == isHigh && s.Index < index).OrderByDescending(s => s.Index).FirstOrDefault();
        if (swing.Price > 0)
            return swing.Price;

        var prior = candles.Take(index).ToList();
        return prior.Count == 0
            ? 0
            : isHigh ? prior.Max(c => c.High) : prior.Min(c => c.Low);
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
        var prior = candles.Take(candles.Count - 1).ToList();
        var lastHigh = swings.Where(s => s.IsHigh).OrderByDescending(s => s.Index).FirstOrDefault().Price;
        var lastLow = swings.Where(s => !s.IsHigh).OrderByDescending(s => s.Index).FirstOrDefault().Price;

        if (lastHigh <= 0 && prior.Count > 0)
            lastHigh = prior.Max(c => c.High);
        if (lastLow <= 0 && prior.Count > 0)
            lastLow = prior.Min(c => c.Low);

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
        int bearFvg,
        string premiumDiscount,
        bool inOte)
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
        parts.Add($"zona {premiumDiscount}");
        if (inOte) parts.Add("OTE ativo");

        return string.Join(" · ", parts);
    }

    private static (string Zone, bool InOte, decimal RangeLow, decimal RangeHigh) ResolvePremiumDiscountOte(
        IReadOnlyList<Candle> candles,
        decimal price)
    {
        var lookback = candles.TakeLast(Math.Min(50, candles.Count)).ToList();
        var rangeHigh = lookback.Max(c => c.High);
        var rangeLow = lookback.Min(c => c.Low);
        var range = rangeHigh - rangeLow;
        if (range <= 0)
            return ("Equilibrium", false, rangeLow, rangeHigh);

        var position = (price - rangeLow) / range;
        var zone = position switch
        {
            >= 0.7m => "Premium",
            <= 0.3m => "Discount",
            _ => "Equilibrium"
        };

        // OTE (Optimal Trade Entry) ≈ retração 61.8–79% do impulso
        var inOte = zone == "Discount" && position is >= 0.20m and <= 0.40m
                    || zone == "Premium" && position is >= 0.60m and <= 0.80m
                    || position is >= 0.618m and <= 0.79m
                    || position is >= 0.21m and <= 0.382m;

        return (zone, inOte, rangeLow, rangeHigh);
    }

    private static List<SmcDetection> BuildDetections(
        IReadOnlyList<Candle> candles,
        SmcStructureBias bias,
        bool bullBos,
        bool bearBos,
        bool bullChoch,
        bool bearChoch,
        IReadOnlyList<(decimal Low, decimal High)> bullOb,
        IReadOnlyList<(decimal Low, decimal High)> bearOb,
        IReadOnlyList<(decimal Low, decimal High)> bullFvg,
        IReadOnlyList<(decimal Low, decimal High)> bearFvg,
        IReadOnlyList<SmcStructureEvent> events,
        string premiumDiscount,
        bool inOte,
        decimal lastClose)
    {
        var ts = candles[^1].OpenTime;
        var list = new List<SmcDetection>();

        void Add(string type, int score, decimal weight, decimal confidence, string detBias, string explanation, decimal? low = null, decimal? high = null)
        {
            list.Add(new SmcDetection
            {
                Type = type,
                Score = score,
                Weight = weight,
                Confidence = confidence,
                Timestamp = ts,
                Bias = detBias,
                Explanation = explanation,
                PriceLow = low,
                PriceHigh = high
            });
        }

        if (bullBos)
            Add("BOS", 78, 0.18m, 80, "Bullish", "Break of Structure bullish — fechamento acima do swing high");
        if (bearBos)
            Add("BOS", 22, 0.18m, 80, "Bearish", "Break of Structure bearish — fechamento abaixo do swing low");
        if (bullChoch)
            Add("CHOCH", 72, 0.16m, 75, "Bullish", "Change of Character bullish — possível reversão de baixa");
        if (bearChoch)
            Add("CHOCH", 28, 0.16m, 75, "Bearish", "Change of Character bearish — possível reversão de alta");

        foreach (var ob in bullOb.TakeLast(2))
            Add("OrderBlock", 70, 0.12m, 68, "Bullish", "Order Block comprador", ob.Low, ob.High);
        foreach (var ob in bearOb.TakeLast(2))
            Add("OrderBlock", 30, 0.12m, 68, "Bearish", "Order Block vendedor", ob.Low, ob.High);

        foreach (var fvg in bullFvg.TakeLast(2))
            Add("FVG", 66, 0.10m, 64, "Bullish", "Fair Value Gap bullish (ineficiência de alta)", fvg.Low, fvg.High);
        foreach (var fvg in bearFvg.TakeLast(2))
            Add("FVG", 34, 0.10m, 64, "Bearish", "Fair Value Gap bearish (ineficiência de baixa)", fvg.Low, fvg.High);

        foreach (var sweep in events.Where(e => e.Type == "LiquiditySweep").Take(2))
        {
            var bullish = sweep.Severity.Contains("Bullish", StringComparison.OrdinalIgnoreCase);
            Add("LiquiditySweep", bullish ? 74 : 26, 0.14m, 72,
                bullish ? "Bullish" : "Bearish",
                sweep.Description,
                sweep.PriceLevel, sweep.PriceLevel);
        }

        Add("PremiumDiscount",
            premiumDiscount == "Discount" ? 68 : premiumDiscount == "Premium" ? 32 : 50,
            0.08m, 60, bias.ToString(),
            $"Preço em zona {premiumDiscount} do range recente (close {lastClose:F2})");

        if (inOte)
        {
            Add("OTE", bias == SmcStructureBias.Bearish ? 30 : 70, 0.12m, 70,
                bias == SmcStructureBias.Bearish ? "Bearish" : "Bullish",
                "Optimal Trade Entry — retração na zona de entrada preferencial (≈61.8–79%)");
        }

        return list
            .OrderByDescending(d => d.Confidence * d.Weight)
            .Take(12)
            .ToList();
    }

    private static List<SmcChartZone> BuildOverlays(
        IReadOnlyList<(decimal Low, decimal High)> bullOb,
        IReadOnlyList<(decimal Low, decimal High)> bearOb,
        IReadOnlyList<(decimal Low, decimal High)> bullFvg,
        IReadOnlyList<(decimal Low, decimal High)> bearFvg,
        decimal rangeLow,
        decimal rangeHigh,
        string premiumDiscount,
        bool inOte)
    {
        var overlays = new List<SmcChartZone>();
        // Fewer OB/FVG bands — chart stays readable (nearest/latest structures only).
        var obIdx = 1;
        foreach (var z in bullOb.TakeLast(2))
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
        foreach (var z in bearOb.TakeLast(2))
        {
            overlays.Add(new SmcChartZone
            {
                Type = "OrderBlockSell",
                PriceLow = z.Low,
                PriceHigh = z.High,
                Label = $"OB Venda {obIdx++}"
            });
        }
        foreach (var z in bullFvg.TakeLast(1))
        {
            overlays.Add(new SmcChartZone
            {
                Type = "FvgBuy",
                PriceLow = z.Low,
                PriceHigh = z.High,
                Label = "FVG ↑"
            });
        }
        foreach (var z in bearFvg.TakeLast(1))
        {
            overlays.Add(new SmcChartZone
            {
                Type = "FvgSell",
                PriceLow = z.Low,
                PriceHigh = z.High,
                Label = "FVG ↓"
            });
        }

        var mid = (rangeLow + rangeHigh) / 2m;
        if (rangeHigh > rangeLow)
        {
            // Active side only — inactive Premium/Discount was pure visual noise.
            if (premiumDiscount == "Premium")
            {
                overlays.Add(new SmcChartZone
                {
                    Type = "Premium",
                    PriceLow = mid,
                    PriceHigh = rangeHigh,
                    Label = "Prêmio ★"
                });
            }
            else if (premiumDiscount == "Discount")
            {
                overlays.Add(new SmcChartZone
                {
                    Type = "Discount",
                    PriceLow = rangeLow,
                    PriceHigh = mid,
                    Label = "Desconto ★"
                });
            }
        }

        if (inOte)
        {
            var oteLow = rangeLow + (rangeHigh - rangeLow) * 0.618m;
            var oteHigh = rangeLow + (rangeHigh - rangeLow) * 0.79m;
            overlays.Add(new SmcChartZone
            {
                Type = "OTE",
                PriceLow = Math.Min(oteLow, oteHigh),
                PriceHigh = Math.Max(oteLow, oteHigh),
                Label = "OTE"
            });
        }

        return overlays;
    }
}
