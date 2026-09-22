using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine;

/// <summary>
/// Curates operational + SMC zones into a compact payload for MT5 chart objects
/// (OBJ_RECTANGLE / labels). Prioritizes operator readability — fewer, higher-signal bands.
/// </summary>
public static class Mt5ZoneMarkupBuilder
{
    public const int DefaultMaxZones = 7;
    public const int XauMaxZones = 6;

    public static IReadOnlyList<Mt5ChartZoneMark> Build(
        string symbol,
        TradingIntelligenceSnapshot? snapshot,
        string? preferredTimeframe = "60",
        decimal? lastPrice = null,
        int? maxZones = null)
    {
        if (snapshot is null)
            return [];

        var isXau = IsXau(symbol);
        var limit = maxZones ?? (isXau ? XauMaxZones : DefaultMaxZones);
        var price = lastPrice
                    ?? snapshot.TimeframeAnalyses.FirstOrDefault(t => t.Timeframe == preferredTimeframe)?.Mid
                    ?? snapshot.TimeframeAnalyses.FirstOrDefault()?.Mid
                    ?? 0m;

        var candidates = new List<(Mt5ChartZoneMark Mark, int Priority, decimal Distance)>();

        foreach (var z in snapshot.OperationalZones)
        {
            if (!TryMapOperational(z, symbol, out var mark, out var priority))
                continue;
            candidates.Add((mark, priority, Distance(mark, price)));
        }

        var tf = string.IsNullOrWhiteSpace(preferredTimeframe) ? "60" : preferredTimeframe.Trim();
        var smcBundle = snapshot.SmcOverlays.FirstOrDefault(o => o.Timeframe == tf)
                        ?? snapshot.SmcOverlays.FirstOrDefault();
        if (smcBundle is not null)
        {
            foreach (var z in smcBundle.Overlays)
            {
                if (!TryMapSmc(z, symbol, out var mark, out var priority))
                    continue;
                candidates.Add((mark, priority, Distance(mark, price)));
            }
        }

        // Rank: priority desc, then proximity to last price, then score.
        var ranked = candidates
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.Distance)
            .ThenByDescending(c => c.Mark.Score)
            .Select(c => c.Mark)
            .ToList();

        var result = new List<Mt5ChartZoneMark>();
        foreach (var mark in ranked)
        {
            if (mark.PriceHigh < mark.PriceLow)
                continue;
            if (result.Any(existing => OverlapsHeavily(existing, mark)))
                continue;
            result.Add(mark with { Id = $"z{result.Count}" });
            if (result.Count >= limit)
                break;
        }

        return result;
    }

    private static bool TryMapOperational(
        OperationalZone z,
        string symbol,
        out Mt5ChartZoneMark mark,
        out int priority)
    {
        mark = default!;
        priority = 0;
        var label = z.Label ?? "";

        // Drop noisy / low-value markings for the MT5 chart.
        if ((label.Contains("Value Area", StringComparison.OrdinalIgnoreCase)
             || label.Contains("Área de valor", StringComparison.OrdinalIgnoreCase))
            && !label.Contains("POC", StringComparison.OrdinalIgnoreCase))
            return false;
        if (z.ConfluenceScore < 35 && !label.Contains("Liquidez", StringComparison.OrdinalIgnoreCase))
            return false;

        string kind;
        string side;
        if (label.Contains("Demand", StringComparison.OrdinalIgnoreCase)
            || label.Contains("Demanda", StringComparison.OrdinalIgnoreCase)
            || z.Type is OperationalZoneType.StrongBuy or OperationalZoneType.ModerateBuy
               && (label.Contains("Order Block", StringComparison.OrdinalIgnoreCase)
                   || label.Contains("Bloco de ordens", StringComparison.OrdinalIgnoreCase)))
        {
            kind = "demand";
            side = "buy";
            priority = 90 + Math.Min(z.ConfluenceScore / 10, 9);
        }
        else if (label.Contains("Supply", StringComparison.OrdinalIgnoreCase)
                 || label.Contains("Oferta", StringComparison.OrdinalIgnoreCase)
                 || z.Type is OperationalZoneType.StrongSell or OperationalZoneType.ModerateSell)
        {
            kind = "supply";
            side = "sell";
            priority = 90 + Math.Min(z.ConfluenceScore / 10, 9);
        }
        else if (label.Contains("FVG", StringComparison.OrdinalIgnoreCase))
        {
            kind = "fvg";
            side = z.Type is OperationalZoneType.StrongSell or OperationalZoneType.ModerateSell ? "sell" : "buy";
            priority = 85 + Math.Min(z.ConfluenceScore / 10, 9);
        }
        else if (label.Contains("POC", StringComparison.OrdinalIgnoreCase))
        {
            kind = "poc";
            side = "neutral";
            priority = 70;
        }
        else if (label.Contains("Liquidez", StringComparison.OrdinalIgnoreCase)
                 || label.Contains("Liquidity", StringComparison.OrdinalIgnoreCase))
        {
            kind = "liquidity";
            side = "neutral";
            priority = 55;
        }
        else if (label.Contains("Target", StringComparison.OrdinalIgnoreCase))
        {
            kind = "target";
            side = "buy";
            priority = 50;
        }
        else if (label.Contains("VWAP", StringComparison.OrdinalIgnoreCase))
        {
            // VWAP line is useful but often clutters XAU — keep only if strong confluence.
            if (IsXau(symbol) && z.ConfluenceScore < 60)
                return false;
            kind = "vwap";
            side = "neutral";
            priority = 40;
        }
        else
        {
            kind = "neutral";
            side = z.Type switch
            {
                OperationalZoneType.StrongBuy or OperationalZoneType.ModerateBuy => "buy",
                OperationalZoneType.StrongSell or OperationalZoneType.ModerateSell => "sell",
                _ => "neutral"
            };
            priority = 30 + Math.Min(z.ConfluenceScore / 20, 5);
        }

        mark = new Mt5ChartZoneMark
        {
            Id = "tmp",
            Kind = kind,
            Side = side,
            Label = ShortLabel(kind, label, z.PriceLow, z.PriceHigh, symbol),
            PriceLow = z.PriceLow,
            PriceHigh = z.PriceHigh,
            Score = z.ConfluenceScore,
            FillColor = ColorFor(kind, side),
            BorderColor = BorderFor(kind, side),
            FillAlpha = AlphaFor(kind),
            LineStyle = kind is "fvg" or "vwap" ? "dash" : "solid",
            Source = "operational"
        };
        return true;
    }

    private static bool TryMapSmc(
        SmcChartZoneDto z,
        string symbol,
        out Mt5ChartZoneMark mark,
        out int priority)
    {
        mark = default!;
        priority = 0;
        var type = (z.Type ?? "").ToLowerInvariant();
        var label = z.Label ?? "";

        // Skip inactive / unlabeled premium-discount noise (builder already prefers ★).
        if ((type.Contains("premium") || type.Contains("discount"))
            && !label.Contains('★') && !label.Contains('*'))
            return false;

        string kind;
        string side;
        if (type.Contains("orderblockbuy") || (type.Contains("orderblock") && label.Contains("Compra", StringComparison.OrdinalIgnoreCase)))
        {
            kind = "ob_buy";
            side = "buy";
            priority = 75;
        }
        else if (type.Contains("orderblocksell") || (type.Contains("orderblock") && label.Contains("Venda", StringComparison.OrdinalIgnoreCase)))
        {
            kind = "ob_sell";
            side = "sell";
            priority = 75;
        }
        else if (type.Contains("fvgbuy")
                 || (type.Contains("fvg") && (label.Contains('↑') || label.Contains("FVG+", StringComparison.OrdinalIgnoreCase)
                                              || label.Contains("alta", StringComparison.OrdinalIgnoreCase))))
        {
            kind = "fvg";
            side = "buy";
            priority = 72;
        }
        else if (type.Contains("fvgsell")
                 || (type.Contains("fvg") && (label.Contains('↓') || label.Contains("FVG-", StringComparison.OrdinalIgnoreCase)
                                              || label.Contains("baixa", StringComparison.OrdinalIgnoreCase))))
        {
            kind = "fvg";
            side = "sell";
            priority = 72;
        }
        else if (type.Contains("premium"))
        {
            kind = "premium";
            side = "sell";
            priority = 60;
        }
        else if (type.Contains("discount"))
        {
            kind = "discount";
            side = "buy";
            priority = 60;
        }
        else if (type.Contains("ote"))
        {
            kind = "ote";
            side = "neutral";
            priority = 68;
        }
        else
            return false;

        mark = new Mt5ChartZoneMark
        {
            Id = "tmp",
            Kind = kind,
            Side = side,
            Label = ShortLabel(kind, label, z.PriceLow, z.PriceHigh, symbol),
            PriceLow = z.PriceLow,
            PriceHigh = z.PriceHigh,
            Score = priority,
            FillColor = ColorFor(kind, side),
            BorderColor = BorderFor(kind, side),
            FillAlpha = AlphaFor(kind),
            LineStyle = kind == "fvg" ? "dash" : "solid",
            Source = "smc"
        };
        return true;
    }

    private static string ShortLabel(string kind, string raw, decimal lo, decimal hi, string symbol)
    {
        // ASCII-only labels: MT5 file bridge is read as ANSI — Unicode arrows corrupt (FVG↑ → SVG…).
        var name = kind switch
        {
            "demand" or "ob_buy" => "Demanda",
            "supply" or "ob_sell" => "Oferta",
            "fvg" => IsBearishFvgLabel(raw) ? "FVG-" : "FVG+",
            "poc" => "POC",
            "liquidity" => raw.Contains("topo", StringComparison.OrdinalIgnoreCase) ? "Liquidez+" : "Liquidez-",
            "vwap" => "VWAP",
            "target" => "Alvo",
            "premium" => "Premio",
            "discount" => "Desconto",
            "ote" => "OTE",
            _ => AsciiTruncate(raw, 14)
        };
        var a = FormatPrice(symbol, Math.Min(lo, hi));
        var b = FormatPrice(symbol, Math.Max(lo, hi));
        return a == b ? $"{name} {a}" : $"{name} {a}-{b}";
    }

    private static bool IsBearishFvgLabel(string raw) =>
        raw.Contains('↓')
        || raw.Contains("FVG-", StringComparison.OrdinalIgnoreCase)
        || raw.Contains("Sell", StringComparison.OrdinalIgnoreCase)
        || raw.Contains("Venda", StringComparison.OrdinalIgnoreCase)
        || raw.Contains("baixa", StringComparison.OrdinalIgnoreCase);

    private static string AsciiTruncate(string raw, int max)
    {
        var cleaned = new string(raw.Where(c => c is >= (char)32 and < (char)127).ToArray()).Trim();
        if (cleaned.Length == 0) return "Zona";
        return cleaned.Length > max ? cleaned[..(max - 1)] + "..." : cleaned;
    }

    private static string ColorFor(string kind, string side) => (kind, side) switch
    {
        // Compra = verde claro | Venda = vermelho claro
        ("demand", _) or ("ob_buy", _) or ("discount", _) or ("target", _) => "120,230,150",
        ("supply", _) or ("ob_sell", _) or ("premium", _) => "255,140,145",
        ("fvg", "sell") => "255,140,145",
        ("fvg", _) => "120,230,150",
        ("liquidity", "sell") => "255,140,145",
        ("liquidity", _) => "120,230,150",
        ("poc", _) => "230,210,140",
        ("vwap", _) => "140,200,230",
        ("ote", _) => "230,210,140",
        (_, "sell") => "255,140,145",
        (_, "buy") => "120,230,150",
        _ => "170,180,195"
    };

    private static string BorderFor(string kind, string side) => ColorFor(kind, side);

    private static int AlphaFor(string kind) => kind switch
    {
        "demand" or "supply" or "ob_buy" or "ob_sell" => 40,
        "fvg" => 36,
        "poc" or "ote" => 15,
        "premium" or "discount" => 12,
        "liquidity" or "target" => 14,
        "vwap" => 12,
        _ => 14
    };

    private static decimal Distance(Mt5ChartZoneMark m, decimal price)
    {
        if (price <= 0) return 0;
        var mid = (m.PriceLow + m.PriceHigh) / 2m;
        return Math.Abs(mid - price);
    }

    private static bool OverlapsHeavily(Mt5ChartZoneMark a, Mt5ChartZoneMark b)
    {
        var oLow = Math.Max(a.PriceLow, b.PriceLow);
        var oHigh = Math.Min(a.PriceHigh, b.PriceHigh);
        if (oHigh <= oLow) return false;
        var smaller = Math.Min(a.PriceHigh - a.PriceLow, b.PriceHigh - b.PriceLow);
        if (smaller <= 0) return false;
        return (oHigh - oLow) / smaller >= 0.55m;
    }

    private static bool IsXau(string symbol)
    {
        var s = (symbol ?? "").Trim().ToUpperInvariant();
        return s is "XAUUSD" or "XAU" or "GOLD" || s.Contains("XAU", StringComparison.Ordinal);
    }

    private static string FormatPrice(string symbol, decimal price)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        return IsXau(symbol)
            ? price.ToString("F2", culture)
            : price >= 1000
                ? price.ToString("F0", culture)
                : price.ToString("F2", culture);
    }
}

public sealed record Mt5ChartZoneMark
{
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Side { get; init; } = "neutral";
    public string Label { get; init; } = "";
    public decimal PriceLow { get; init; }
    public decimal PriceHigh { get; init; }
    public int Score { get; init; }
    /// <summary>RGB "r,g,b" for MQL ColorToARGB.</summary>
    public string FillColor { get; init; } = "148,163,184";
    public string BorderColor { get; init; } = "148,163,184";
    /// <summary>0–255 alpha used by indicator for fill transparency.</summary>
    public int FillAlpha { get; init; } = 40;
    public string LineStyle { get; init; } = "solid";
    public string Source { get; init; } = "operational";
}
