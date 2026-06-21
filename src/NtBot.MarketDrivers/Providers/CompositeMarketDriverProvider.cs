using NtBot.MarketDrivers.Configuration;
using NtBot.MarketDrivers.Models;
using NtBot.MarketDrivers.Rules;
using NtBot.Macro.DTO;
using NtBot.Macro.Engine;
using NtBot.MarketIntelligence.Models;

namespace NtBot.MarketDrivers.Providers;

public sealed class CompositeMarketDriverProvider : IMarketDriverProvider
{
    private readonly IEnumerable<IMarketDriverRule> _rules;

    public CompositeMarketDriverProvider(IEnumerable<IMarketDriverRule> rules) => _rules = rules;

    public string Name => "composite";
    public IReadOnlyList<string> Capabilities { get; } = ["macro", "market", "correlation", "calendar"];

    public Task<IReadOnlyList<MarketDriver>> BuildDriversAsync(
        MarketDriverContext context,
        CancellationToken cancellationToken = default)
    {
        var drivers = _rules
            .SelectMany(rule => rule.Apply(context))
            .OrderByDescending(d => d.Weight)
            .ThenBy(d => d.Name)
            .ToList();

        return Task.FromResult<IReadOnlyList<MarketDriver>>(drivers);
    }
}

public sealed class MarketDriverContextBuilder
{
    private readonly NtBot.MarketIntelligence.Services.IMarketIntelligenceService _market;
    private readonly NtBot.Macro.Services.IMacroIntelligenceService _macro;
    private readonly IMacroRecommendationEngine _macroRecommendations;
    private readonly IDriverCompositionStore _composition;

    public MarketDriverContextBuilder(
        NtBot.MarketIntelligence.Services.IMarketIntelligenceService market,
        NtBot.Macro.Services.IMacroIntelligenceService macro,
        IMacroRecommendationEngine macroRecommendations,
        IDriverCompositionStore composition)
    {
        _market = market;
        _macro = macro;
        _macroRecommendations = macroRecommendations;
        _composition = composition;
    }

    public async Task<MarketDriverContext> BuildAsync(string asset, CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(asset);
        var overview = await _market.GetOverviewAsync(cancellationToken);
        var correlation = await _market.GetCorrelationAsync(cancellationToken);
        var quantScore = await _market.GetQuantScoreAsync(cancellationToken);
        var macro = await _macro.GetCurrentSnapshotAsync(normalized, cancellationToken);
        var macroRec = _macroRecommendations.GetRecommendation(macro, normalized);
        var assetImpact = correlation.AssetImpacts.FirstOrDefault(a =>
            string.Equals(a.Asset, normalized, StringComparison.OrdinalIgnoreCase));
        var driverSources = await _composition.GetSourcesAsync(normalized, cancellationToken: cancellationToken);

        return new MarketDriverContext
        {
            Asset = normalized,
            Overview = overview,
            Correlation = correlation,
            QuantScore = quantScore,
            Macro = macro,
            MacroRecommendation = macroRec,
            AssetImpact = assetImpact,
            DriverSources = driverSources
        };
    }
}
