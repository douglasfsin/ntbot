using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine.Consensus;

public interface IMultiTimeframeConsensusEngine
{
    MultiTimeframeConsensus Evaluate(IReadOnlyList<TimeframeAnalysis> timeframes);
}

/// <summary>
/// Consenso multi-timeframe a partir dos scores SMC/Wyckoff/Volume por TF.
/// </summary>
public sealed class MultiTimeframeConsensusEngine : IMultiTimeframeConsensusEngine
{
    public MultiTimeframeConsensus Evaluate(IReadOnlyList<TimeframeAnalysis> timeframes)
    {
        if (timeframes.Count == 0)
        {
            return new MultiTimeframeConsensus
            {
                Bias = "Sideways",
                AgreementPercent = 0,
                HasConflict = false,
                Summary = "Sem timeframes para consenso."
            };
        }

        var bullish = new List<string>();
        var bearish = new List<string>();
        var sideways = new List<string>();

        foreach (var tf in timeframes)
        {
            var avg = (tf.SmcScore + tf.WyckoffScore + tf.VolumeScore) / 3.0;
            if (avg >= 58) bullish.Add(tf.Timeframe);
            else if (avg <= 42) bearish.Add(tf.Timeframe);
            else sideways.Add(tf.Timeframe);
        }

        var total = timeframes.Count;
        var dominantCount = Math.Max(bullish.Count, bearish.Count);
        var agreement = (int)Math.Round(100.0 * dominantCount / total);
        var hasConflict = bullish.Count > 0 && bearish.Count > 0
                          && Math.Abs(bullish.Count - bearish.Count) <= 1
                          && sideways.Count < total;

        string bias;
        if (bullish.Count >= bearish.Count + 2) bias = "Bullish";
        else if (bearish.Count >= bullish.Count + 2) bias = "Bearish";
        else if (bullish.Count > bearish.Count) bias = "Bullish";
        else if (bearish.Count > bullish.Count) bias = "Bearish";
        else bias = "Sideways";

        // Conflito forte: HTs opostos aos LTs
        if (HasHigherLowerConflict(timeframes))
            hasConflict = true;

        var summary = hasConflict
            ? $"Conflito multi-TF: alta={string.Join(",", bullish)} · baixa={string.Join(",", bearish)}"
            : $"Consenso {bias} ({agreement}% alinhamento em {total} TFs)";

        return new MultiTimeframeConsensus
        {
            Bias = bias,
            AgreementPercent = agreement,
            HasConflict = hasConflict,
            BullishTimeframes = bullish,
            BearishTimeframes = bearish,
            SidewaysTimeframes = sideways,
            Summary = summary
        };
    }

    private static bool HasHigherLowerConflict(IReadOnlyList<TimeframeAnalysis> timeframes)
    {
        static int Rank(string tf) => tf switch
        {
            "1440" or "D1" or "1D" => 5,
            "240" or "H4" or "4H" => 4,
            "60" or "H1" or "1H" => 3,
            "30" => 2,
            "15" => 1,
            _ => 0
        };

        var ordered = timeframes.OrderBy(t => Rank(t.Timeframe)).ToList();
        if (ordered.Count < 2)
            return false;

        var lower = ordered.Take(ordered.Count / 2).ToList();
        var higher = ordered.Skip(ordered.Count / 2).ToList();
        var lowerBias = AvgBias(lower);
        var higherBias = AvgBias(higher);
        return lowerBias * higherBias < 0;
    }

    private static int AvgBias(IReadOnlyList<TimeframeAnalysis> tfs)
    {
        var avg = tfs.Average(t => (t.SmcScore + t.WyckoffScore + t.VolumeScore) / 3.0);
        return avg >= 58 ? 1 : avg <= 42 ? -1 : 0;
    }
}
