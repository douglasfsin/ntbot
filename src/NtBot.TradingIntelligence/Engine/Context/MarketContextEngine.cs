using NtBot.Domain.Entities;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine.Context;

public interface IMarketContextEngine
{
    MarketSessionContext Analyze(string asset, IReadOnlyList<Candle> candles, DateTime? utcNow = null);
}

/// <summary>
/// Contexto multi-barra e sessões (Asia / London / NY) — especialmente relevante para XAUUSD.
/// </summary>
public sealed class MarketContextEngine : IMarketContextEngine
{
    private const int ContextBars = 24;

    public MarketSessionContext Analyze(string asset, IReadOnlyList<Candle> candles, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var session = ResolveSession(now);
        var notes = new List<string> { session.Description };

        var ordered = candles.Count > 0
            ? candles.OrderBy(c => c.OpenTime).ToList()
            : [];

        var used = Math.Min(ContextBars, ordered.Count);
        var regime = "Undefined";
        if (used >= 8)
        {
            var slice = ordered.TakeLast(used).ToList();
            var atr = AverageTrueRange(slice, Math.Min(14, slice.Count - 1));
            var range = slice.Max(c => c.High) - slice.Min(c => c.Low);
            var net = slice[^1].Close - slice[0].Open;
            var netAbs = Math.Abs(net);

            if (atr > 0 && range < atr * 2.2m)
            {
                regime = "Ranging";
                notes.Add($"Contexto de {used} barras lateral (range {range:F2} < 2.2×ATR).");
            }
            else if (netAbs > atr * 1.5m)
            {
                regime = net > 0 ? "TrendingUp" : "TrendingDown";
                notes.Add($"Contexto de {used} barras em tendência {(net > 0 ? "de alta" : "de baixa")}.");
            }
            else
            {
                regime = "Transitional";
                notes.Add($"Contexto de {used} barras em transição.");
            }

            if (IsGoldAsset(asset) && session.IsOverlap)
                notes.Add("Sobreposição London/NY — liquidez tipicamente elevada para ouro.");
            else if (IsGoldAsset(asset) && session.IsLowLiquidity)
                notes.Add("Sessão fina para ouro — preferir não operar setups fracos.");
        }
        else
        {
            notes.Add("Poucas barras para contexto multi-candle.");
        }

        return new MarketSessionContext
        {
            Session = session.Name,
            IsOverlap = session.IsOverlap,
            IsLowLiquidity = session.IsLowLiquidity,
            Description = session.Description,
            ContextBarsUsed = used,
            Regime = regime,
            Notes = notes
        };
    }

    private static (string Name, bool IsOverlap, bool IsLowLiquidity, string Description) ResolveSession(DateTime utc)
    {
        var hour = utc.Hour;
        // Sessões em UTC (aproximação institucional para ouro):
        // Asia 00–07, London 07–12, Overlap 12–16, NY 16–21, Late/thin 21–00
        return hour switch
        {
            >= 12 and < 16 => ("LondonNY_Overlap", true, false, "Sobreposição London/NY (UTC)"),
            >= 7 and < 12 => ("London", false, false, "Sessão London (UTC)"),
            >= 16 and < 21 => ("NewYork", false, false, "Sessão New York (UTC)"),
            >= 0 and < 7 => ("Asia", false, true, "Sessão Asia (UTC) — liquidez reduzida no ouro"),
            _ => ("Late", false, true, "Sessão tardia (UTC) — liquidez tipicamente baixa")
        };
    }

    private static bool IsGoldAsset(string asset) =>
        asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase)
        || asset.Equals("XAU", StringComparison.OrdinalIgnoreCase)
        || asset.Equals("GOLD", StringComparison.OrdinalIgnoreCase);

    private static decimal AverageTrueRange(IReadOnlyList<Candle> candles, int period)
    {
        if (candles.Count < 2 || period < 1)
            return 0;

        var trs = new List<decimal>();
        for (var i = 1; i < candles.Count; i++)
        {
            var c = candles[i];
            var p = candles[i - 1];
            trs.Add(Math.Max(c.High - c.Low, Math.Max(Math.Abs(c.High - p.Close), Math.Abs(c.Low - p.Close))));
        }

        return trs.TakeLast(period).DefaultIfEmpty(0).Average();
    }
}
