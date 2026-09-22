using System.Diagnostics;
using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine;

public interface IRiskEngine
{
    EngineAnalysisResult Assess(InstitutionalRiskInput input);
}

/// <summary>
/// Não altera direção — apenas quantifica risco e penaliza confiança global.
/// </summary>
public sealed class RiskEngine : IRiskEngine
{
    public EngineAnalysisResult Assess(InstitutionalRiskInput input)
    {
        var sw = Stopwatch.StartNew();
        var signals = new List<string>();
        var penalty = 0m;

        if (input.TotalEngineCount > 0)
        {
            var coverage = (decimal)input.KnownEngineCount / input.TotalEngineCount;
            if (coverage < 0.5m)
            {
                penalty += 25m;
                signals.Add($"cobertura de dados baixa ({input.KnownEngineCount}/{input.TotalEngineCount} engines)");
            }
            else if (coverage < 0.75m)
            {
                penalty += 12m;
                signals.Add($"cobertura parcial de dados ({input.KnownEngineCount}/{input.TotalEngineCount})");
            }
        }

        if (input.HasHighImpactCalendarEvent)
        {
            penalty += 18m;
            signals.Add("evento macro de alto impacto no calendário");
        }
        else if (input.HasMediumImpactCalendarEvent)
        {
            penalty += 8m;
            signals.Add("evento macro de impacto moderado no calendário");
        }

        penalty += input.Liquidity switch
        {
            MacroLiquidityLevel.Low => 15m,
            MacroLiquidityLevel.High => -5m,
            _ => 0m
        };
        if (input.Liquidity == MacroLiquidityLevel.Low)
            signals.Add("liquidez macro reduzida");

        if (input.AtrPercent is > 1.5m)
        {
            penalty += 12m;
            signals.Add($"volatilidade elevada (ATR {input.AtrPercent:F2}%)");
        }

        if (input.IsLowLiquiditySession)
        {
            penalty += 10m;
            signals.Add("sessão de baixa liquidez");
        }

        if (input.HasTimeframeConflict)
        {
            penalty += 14m;
            signals.Add("conflito entre timeframes");
        }

        if (input.IsRanging)
        {
            penalty += 10m;
            signals.Add("mercado em ranging");
        }

        if (input.StructureUndefined)
        {
            penalty += 12m;
            signals.Add("estrutura indefinida");
        }

        if (input.WeakVolume)
        {
            penalty += 8m;
            signals.Add("volume fraco");
        }

        if (input.SpreadPoints is decimal spread && spread > 40)
        {
            penalty += 12m;
            signals.Add($"spread elevado ({spread:F1})");
        }

        var confidence = Math.Clamp(100m - penalty, 15, 100);
        var riskLevel = confidence switch
        {
            >= 80 => InstitutionalRiskLevel.Low,
            >= 60 => InstitutionalRiskLevel.Moderate,
            >= 40 => InstitutionalRiskLevel.High,
            _ => InstitutionalRiskLevel.Extreme
        };

        signals.Add($"nível de risco: {riskLevel}");

        sw.Stop();
        return EngineAnalysisResult.RiskOnly(confidence, signals, sw.ElapsedMilliseconds);
    }
}
