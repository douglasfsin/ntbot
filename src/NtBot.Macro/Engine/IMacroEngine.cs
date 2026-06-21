namespace NtBot.Macro.Engine;

public interface IMacroEngine
{
    MacroSnapshot BuildSnapshot(IReadOnlyList<MacroProviderPayload> payloads, string? symbol = null);
}

public interface IMacroRecommendationEngine
{
    IReadOnlyList<MacroRecommendation> GetRecommendations(MacroSnapshot snapshot, params string[] tickers);
    MacroRecommendation GetRecommendation(MacroSnapshot snapshot, string ticker);
}
