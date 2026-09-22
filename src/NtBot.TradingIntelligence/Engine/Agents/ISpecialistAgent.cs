using NtBot.TradingIntelligence.Models;

namespace NtBot.TradingIntelligence.Engine.Agents;

/// <summary>
/// Contrato para orquestração de agentes especialistas (stub — sem ML).
/// </summary>
public interface ISpecialistAgent
{
    string AgentId { get; }
    string Specialization { get; }
    Task<AiAgentInsight> AnalyzeAsync(TradingIntelligenceSnapshot snapshot, CancellationToken cancellationToken = default);
}

public interface IMasterAgentOrchestrator
{
    Task<TradingIntelligenceAiResult> OrchestrateAsync(
        TradingIntelligenceSnapshot snapshot,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Orquestrador stub: reúne insights existentes sem chamar modelos externos.
/// Persistência/recalibração de pesos fica como hook futuro via <see cref="IWeightCalibrationStore"/>.
/// </summary>
public sealed class StubMasterAgentOrchestrator : IMasterAgentOrchestrator
{
    public Task<TradingIntelligenceAiResult> OrchestrateAsync(
        TradingIntelligenceSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var insights = SpecialistAgentEngine.BuildInsights(snapshot.Asset, snapshot);
        var master = new MasterAgentSummary
        {
            Summary = snapshot.Confluence.Explanation,
            Confluences = snapshot.Intersections.Where(i => i.HighConfluence).Select(i => i.Pair).ToList(),
            Strengths = snapshot.Confluence.PositiveFactors.ToList(),
            Weaknesses = snapshot.Confluence.NegativeFactors
                .Concat(snapshot.Confluence.BlockingFactors)
                .Distinct()
                .ToList(),
            Probability = snapshot.Confluence.ConfidenceLevel,
            Risk = snapshot.Confluence.RiskLevel
        };

        return Task.FromResult(new TradingIntelligenceAiResult
        {
            Master = master,
            AgentInsights = insights
        });
    }
}

/// <summary>Hook de persistência para recalibração futura de pesos (não implementa ML).</summary>
public interface IWeightCalibrationStore
{
    Task<IReadOnlyDictionary<string, decimal>?> GetWeightsAsync(string asset, CancellationToken cancellationToken = default);
    Task SaveWeightsAsync(string asset, IReadOnlyDictionary<string, decimal> weights, CancellationToken cancellationToken = default);
}

public sealed class InMemoryWeightCalibrationStore : IWeightCalibrationStore
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, decimal>> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyDictionary<string, decimal>?> GetWeightsAsync(string asset, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(asset, out var weights);
        return Task.FromResult(weights);
    }

    public Task SaveWeightsAsync(string asset, IReadOnlyDictionary<string, decimal> weights, CancellationToken cancellationToken = default)
    {
        _store[asset] = weights;
        return Task.CompletedTask;
    }
}
