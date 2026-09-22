using NtBot.Domain.Entities;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine.Risk;

public interface ITradeRiskPlanner
{
    TradeRiskSuggestion? Plan(
        string recommendation,
        decimal? lastPrice,
        IReadOnlyList<Candle> candles,
        SmcAnalysisResult? smc,
        decimal? atrPercent);
}

/// <summary>
/// Sugere stop (ATR/estrutura), TP e R:R quando há recomendação Compra/Venda.
/// </summary>
public sealed class TradeRiskPlanner : ITradeRiskPlanner
{
    public TradeRiskSuggestion? Plan(
        string recommendation,
        decimal? lastPrice,
        IReadOnlyList<Candle> candles,
        SmcAnalysisResult? smc,
        decimal? atrPercent)
    {
        if (lastPrice is null or <= 0 || candles.Count < 20)
            return null;

        var isBuy = recommendation.Contains("COMPRA", StringComparison.OrdinalIgnoreCase);
        var isSell = recommendation.Contains("VENDA", StringComparison.OrdinalIgnoreCase);
        if (!isBuy && !isSell)
            return null;

        var ordered = candles.OrderBy(c => c.OpenTime).ToList();
        var atr = CalculateAtr(ordered, 14);
        if (atr <= 0)
            atr = lastPrice.Value * 0.002m;

        var entry = lastPrice.Value;
        decimal stop;
        string stopBasis;
        decimal tp;
        string tpBasis;

        if (isBuy)
        {
            var structureStop = smc?.ActiveZoneLow ?? ordered.TakeLast(20).Min(c => c.Low);
            var atrStop = entry - atr * 1.5m;
            stop = Math.Min(structureStop, atrStop);
            if (stop >= entry)
                stop = entry - atr * 1.5m;
            stopBasis = structureStop < atrStop ? "abaixo da zona/estrutura" : "1.5×ATR";
            tp = entry + Math.Abs(entry - stop) * 2m;
            tpBasis = "R:R alvo 1:2";
        }
        else
        {
            var structureStop = smc?.ActiveZoneHigh ?? ordered.TakeLast(20).Max(c => c.High);
            var atrStop = entry + atr * 1.5m;
            stop = Math.Max(structureStop, atrStop);
            if (stop <= entry)
                stop = entry + atr * 1.5m;
            stopBasis = structureStop > atrStop ? "acima da zona/estrutura" : "1.5×ATR";
            tp = entry - Math.Abs(stop - entry) * 2m;
            tpBasis = "R:R alvo 1:2";
        }

        var risk = Math.Abs(entry - stop);
        var reward = Math.Abs(tp - entry);
        var rr = risk > 0 ? reward / risk : 0;

        return new TradeRiskSuggestion
        {
            Entry = Math.Round(entry, 2),
            StopLoss = Math.Round(stop, 2),
            TakeProfit = Math.Round(tp, 2),
            RiskReward = Math.Round(rr, 2),
            StopBasis = stopBasis,
            TakeProfitBasis = tpBasis,
            Summary = atrPercent is not null
                ? $"Stop {stop:F2} ({stopBasis}) · TP {tp:F2} · R:R {rr:F2} · ATR {atrPercent:F2}%"
                : $"Stop {stop:F2} ({stopBasis}) · TP {tp:F2} · R:R {rr:F2}"
        };
    }

    private static decimal CalculateAtr(IReadOnlyList<Candle> candles, int period)
    {
        if (candles.Count < period + 1)
            return 0;

        var trs = new List<decimal>();
        for (var i = 1; i < candles.Count; i++)
        {
            var c = candles[i];
            var p = candles[i - 1];
            trs.Add(Math.Max(c.High - c.Low, Math.Max(Math.Abs(c.High - p.Close), Math.Abs(c.Low - p.Close))));
        }

        return trs.TakeLast(period).Average();
    }
}
