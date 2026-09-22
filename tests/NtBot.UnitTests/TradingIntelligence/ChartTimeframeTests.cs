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
    [InlineData("3", "M3")]
    [InlineData("M3", "M3")]
    [InlineData("1", "M1")]
    [InlineData("240", "H4")]
    [InlineData("1440", "D1")]
    public void Normalize_MapsTradingIntelligenceKeys(string input, string expected) =>
        Assert.Equal(expected, ChartTimeframe.Normalize(input));

    [Theory]
    [InlineData("60", "60")]
    [InlineData("H1", "60")]
    [InlineData("M15", "15")]
    [InlineData("M3", "3")]
    [InlineData("D1", "1440")]
    public void ToChartKey_ReturnsUiTabKey(string input, string expected) =>
        Assert.Equal(expected, ChartTimeframe.ToChartKey(input));

    [Fact]
    public void ToMinutes_ReturnsExpectedValues()
    {
        Assert.Equal(3, ChartTimeframe.ToMinutes("3"));
        Assert.Equal(240, ChartTimeframe.ToMinutes("H4"));
        Assert.Equal(1440, ChartTimeframe.ToMinutes("1440"));
    }

    [Fact]
    public void Aliases_IncludesLegacyDatabaseFormats()
    {
        var aliases = ChartTimeframe.Aliases("5");
        Assert.Contains("M5", aliases);
        Assert.Contains("5m", aliases);
    }
}
