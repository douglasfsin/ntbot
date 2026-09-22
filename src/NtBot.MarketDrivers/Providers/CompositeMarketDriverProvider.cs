using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory _scopeFactory;

    public MarketDriverContextBuilder(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<MarketDriverContext> BuildAsync(string asset, CancellationToken cancellationToken = default)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(asset);

        // Fontes independentes em paralelo, cada uma com DbContext próprio (scope isolado).
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(45));
        var ct = timeoutCts.Token;

        var overviewTask = InScopeAsync(
            sp => sp.GetRequiredService<NtBot.MarketIntelligence.Services.IMarketIntelligenceService>()
                .GetOverviewAsync(ct),
            new MarketOverview());

        var correlationTask = InScopeAsync(
            sp => sp.GetRequiredService<NtBot.MarketIntelligence.Services.IMarketIntelligenceService>()
                .GetCorrelationAsync(ct),
            new CorrelationResult { Timestamp = DateTime.UtcNow });

        var quantScoreTask = InScopeAsync(
            sp => sp.GetRequiredService<NtBot.MarketIntelligence.Services.IMarketIntelligenceService>()
                .GetQuantScoreAsync(ct),
            new QuantScore());

        var macroTask = InScopeAsync(
            sp => sp.GetRequiredService<NtBot.Macro.Services.IMacroIntelligenceService>()
                .GetCurrentSnapshotAsync(normalized, ct),
            new MacroSnapshot());

        var sourcesTask = InScopeAsync(
            async sp => await sp.GetRequiredService<IDriverCompositionStore>()
                .GetSourcesAsync(normalized, cancellationToken: ct),
            (IReadOnlyList<DriverSourceDefinition>)[]);

        await Task.WhenAll(overviewTask, correlationTask, quantScoreTask, macroTask, sourcesTask);

        var overview = await overviewTask;
        var correlation = await correlationTask;
        var quantScore = await quantScoreTask;
        var macro = await macroTask;
        var driverSources = await sourcesTask;

        MacroRecommendation? macroRec = null;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IMacroRecommendationEngine>();
            macroRec = engine.GetRecommendation(macro, normalized);
        }
        catch
        {
            // optional
        }

        var assetImpact = correlation.AssetImpacts.FirstOrDefault(a =>
            string.Equals(a.Asset, normalized, StringComparison.OrdinalIgnoreCase));

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

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action, T fallback)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            return await action(scope.ServiceProvider);
        }
        catch (OperationCanceledException)
        {
            return fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
