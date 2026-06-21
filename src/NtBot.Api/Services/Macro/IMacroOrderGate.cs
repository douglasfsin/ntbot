using NtBot.Api.Services.Interfaces;
using NtBot.Domain.Entities;

namespace NtBot.Api.Services.Macro;

public interface IMacroOrderGate
{
    Task<MacroOrderGateResult> EvaluateAsync(
        Guid tenantId,
        string symbol,
        TradeDirection direction,
        CancellationToken cancellationToken = default);
}

public sealed class MacroOrderGateResult
{
    public bool Allowed { get; init; } = true;
    public string? Reason { get; init; }
    public decimal SizeMultiplier { get; init; } = 1m;
    public bool EconomicEventActive { get; init; }
}
