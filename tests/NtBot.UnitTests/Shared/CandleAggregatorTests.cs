using NtBot.Shared.MarketData;

namespace NtBot.UnitTests.Shared;

public class CandleAggregatorTests
{
    [Fact]
    public void FromM1_AggregatesFiveMinuteBars()
    {
        var start = new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);
        var m1 = Enumerable.Range(0, 10)
            .Select(i => new OhlcvBar
            {
                OpenTime = start.AddMinutes(i),
                Open = 100 + i,
                High = 110 + i,
                Low = 90 + i,
                Close = 105 + i,
                Volume = 10
            })
            .ToList();

        var m5 = CandleAggregator.FromM1(m1, 5, 10);

        Assert.Equal(2, m5.Count);
        Assert.Equal(start, m5[0].OpenTime);
        Assert.Equal(100, m5[0].Open);
        Assert.Equal(114, m5[0].High); // 110+4
        Assert.Equal(90, m5[0].Low);
        Assert.Equal(109, m5[0].Close); // 105+4
        Assert.Equal(50, m5[0].Volume);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(240)]
    [InlineData(1440)]
    public void FromM1_SupportsAllConfiguredTimeframes(int minutes)
    {
        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var m1 = Enumerable.Range(0, minutes * 3)
            .Select(i => new OhlcvBar
            {
                OpenTime = start.AddMinutes(i),
                Open = 1,
                High = 2,
                Low = 0.5m,
                Close = 1.5m,
                Volume = 1
            })
            .ToList();

        var bars = CandleAggregator.FromM1(m1, minutes, 10);
        Assert.True(bars.Count >= 2);
        Assert.All(bars, b => Assert.Equal(DateTimeKind.Utc, b.OpenTime.Kind));
    }

    [Fact]
    public void RequiredM1Count_ScalesWithTimeframe()
    {
        Assert.True(CandleAggregator.RequiredM1Count(5, 80) >= 80 * 5);
        Assert.True(CandleAggregator.RequiredM1Count(1, 80) >= 80);
        // Large TFs are capped so MT5 seed never requests tens of thousands of M1 bars.
        Assert.Equal(1_500, CandleAggregator.RequiredM1Count(60, 80));
        Assert.Equal(500, CandleAggregator.RequiredM1Count(60, 80, max: 500));
    }
}
