using NtBot.Domain.Entities;
using NtBot.Macro.Configuration;
using NtBot.Macro.Services;

namespace NtBot.UnitTests.Macro;

public class MacroSymbolAliasesTests
{
    [Theory]
    [InlineData("WINFUT", "WIN")]
    [InlineData("MNQ", "NQ")]
    [InlineData("MES", "ES")]
    [InlineData("WDOFUT", "WDO")]
    [InlineData("XAUUSD", "XAUUSD")]
    public void Normalize_MapsKnownAliases(string input, string expected) =>
        Assert.Equal(expected, MacroSymbolAliases.Normalize(input));
}

public class FredEconomicCalendarSyncServiceTests
{
    [Theory]
    [InlineData("Employment Situation", EventImpact.HIGH)]
    [InlineData("Consumer Price Index for All Urban Consumers", EventImpact.HIGH)]
    [InlineData("FOMC Press Release", EventImpact.HIGH)]
    [InlineData("Housing Starts", EventImpact.MEDIUM)]
    [InlineData("Weekly Claims", EventImpact.LOW)]
    public void ClassifyImpact_UsesKeywordRules(string releaseName, EventImpact expected) =>
        Assert.Equal(expected, FredEconomicCalendarSyncService.ClassifyImpact(releaseName));
}
