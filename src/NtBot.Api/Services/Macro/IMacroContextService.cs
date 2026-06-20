using NtBot.Domain.Entities;

namespace NtBot.Api.Services.Macro
{
    public interface IMacroContextService
    {
        Task<MacroContextResult> AnalyzeAsync(string primarySymbol = "MNQ");
        Task<MacroBias> GetDailyBiasAsync(string symbol);
        Task<RiskMode> GetRiskModeAsync();
        Task<Dictionary<string, decimal>> GetCorrelationsAsync(string symbol, List<string> referenceSymbols);
    }
}
