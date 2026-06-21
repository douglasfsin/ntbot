using NtBot.Shared.MarketData;

namespace NtBot.UnitTests.TradingIntelligence;

public class CandleSymbolAliasesTests
{
    [Theory]
    [InlineData("WINFUT", "WIN")]
    [InlineData("WIN", "WIN")]
    [InlineData("WDOFUT", "WDO")]
    [InlineData("NAS100", "NQ")]
    public void Canonical_MapsStorageSymbols(string input, string expected) =>
        Assert.Equal(expected, CandleSymbolAliases.Canonical(input));

    [Fact]
    public void Expand_WIN_IncludesWinfut()
    {
        var aliases = CandleSymbolAliases.Expand("WIN");
        Assert.Contains("WIN", aliases);
        Assert.Contains("WINFUT", aliases);
    }
}
