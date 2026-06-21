using NtBot.Shared.MarketData;

namespace NtBot.UnitTests.TradingIntelligence;

public class ChartTimeframeTests
{
    [Theory]
    [InlineData("5", "M5")]
    [InlineData("15", "M15")]
    [InlineData("60", "H1")]
    [InlineData("M5", "M5")]
    [InlineData("1h", "H1")]
    [InlineData("5m", "M5")]
    public void Normalize_MapsTradingIntelligenceKeys(string input, string expected) =>
        Assert.Equal(expected, ChartTimeframe.Normalize(input));

    [Theory]
    [InlineData("60", "60")]
    [InlineData("H1", "60")]
    [InlineData("M15", "15")]
    public void ToChartKey_ReturnsUiTabKey(string input, string expected) =>
        Assert.Equal(expected, ChartTimeframe.ToChartKey(input));

    [Fact]
    public void Aliases_IncludesLegacyDatabaseFormats()
    {
        var aliases = ChartTimeframe.Aliases("5");
        Assert.Contains("M5", aliases);
        Assert.Contains("5m", aliases);
    }
}
