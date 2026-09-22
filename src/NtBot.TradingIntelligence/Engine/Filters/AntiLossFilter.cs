using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine.Filters;

public interface IAntiLossFilter
{
    AntiLossFilterResult Evaluate(AntiLossFilterInput input);
}

/// <summary>
/// Filtros anti-perda: spread alto, ATR extremo, ranging, conflito de TF, volume fraco, estrutura indefinida.
/// Preferência: NÃO operar quando o filtro dispara.
/// </summary>
public sealed class AntiLossFilter : IAntiLossFilter
{
    public AntiLossFilterResult Evaluate(AntiLossFilterInput input)
    {
        var reasons = new List<string>();
        var penalty = 0m;

        var maxSpread = input.MaxAcceptableSpread ?? DefaultMaxSpread(input.Asset);
        if (input.SpreadPoints is decimal spread && spread > maxSpread)
        {
            reasons.Add($"spread elevado ({spread:F1} > {maxSpread:F1})");
            penalty += 22;
        }

        if (input.AtrPercent is > 1.5m)
        {
            reasons.Add($"ATR extremo ({input.AtrPercent:F2}%)");
            penalty += 20;
        }
        else if (input.AtrPercent is > 1.1m)
        {
            reasons.Add($"ATR elevado ({input.AtrPercent:F2}%)");
            penalty += 10;
        }

        if (input.IsRanging)
        {
            reasons.Add("mercado em ranging — estrutura sem direção clara");
            penalty += 14;
        }

        if (input.HasTimeframeConflict)
        {
            reasons.Add("conflito entre timeframes");
            penalty += 18;
        }

        if (input.WeakVolume || input.VolumeScore is > 0 and < 40)
        {
            reasons.Add("volume fraco / sem confirmação");
            penalty += 12;
        }

        if (input.StructureUndefined || input.StructureScore is >= 45 and <= 55)
        {
            reasons.Add("estrutura indefinida");
            penalty += 16;
        }

        if (input.IsLowLiquiditySession)
        {
            reasons.Add("sessão de baixa liquidez");
            penalty += 10;
        }

        if (input.HasHighImpactCalendar)
        {
            reasons.Add("evento macro de alto impacto");
            penalty += 15;
        }

        // Bloqueia direção se houver 2+ razões ou penalidade alta
        var shouldBlock = reasons.Count >= 2 || penalty >= 28;
        return new AntiLossFilterResult
        {
            ShouldBlock = shouldBlock,
            Reasons = reasons,
            ConfidencePenalty = Math.Clamp(penalty, 0, 55)
        };
    }

    private static decimal DefaultMaxSpread(string asset) =>
        asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase) ? 45m : 30m;
}
