using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Models;

namespace NtBot.UnitTests.TradingIntelligence;

public class Mt5ZoneMarkupBuilderTests
{
    [Fact]
    public void Build_Xauusd_DropsValueAreaAndCapsZones()
    {
        var snapshot = new TradingIntelligenceSnapshot
        {
            Asset = "XAUUSD",
            OperationalZones =
            [
                new OperationalZone
                {
                    Type = OperationalZoneType.StrongBuy,
                    Label = "Demand / Order Block 60min",
                    PriceLow = 2650,
                    PriceHigh = 2655,
                    ConfluenceScore = 80,
                    Timeframe = "60"
                },
                new OperationalZone
                {
                    Type = OperationalZoneType.Neutral,
                    Label = "Value Area 15min",
                    PriceLow = 2600,
                    PriceHigh = 2700,
                    ConfluenceScore = 50,
                    Timeframe = "15"
                },
                new OperationalZone
                {
                    Type = OperationalZoneType.Neutral,
                    Label = "POC / Value Area (60min)",
                    PriceLow = 2648,
                    PriceHigh = 2652,
                    ConfluenceScore = 70,
                    Timeframe = "60"
                },
                new OperationalZone
                {
                    Type = OperationalZoneType.Neutral,
                    Label = "VWAP (proxy multi-TF)",
                    PriceLow = 2649,
                    PriceHigh = 2651,
                    ConfluenceScore = 40,
                    Timeframe = "multi"
                }
            ],
            SmcOverlays =
            [
                new SmcOverlayBundle
                {
                    Timeframe = "60",
                    Overlays =
                    [
                        new SmcChartZoneDto { Type = "Premium", Label = "Premium", PriceLow = 2660, PriceHigh = 2680 },
                        new SmcChartZoneDto { Type = "Discount", Label = "Discount ★", PriceLow = 2620, PriceHigh = 2640 },
                        new SmcChartZoneDto { Type = "FvgBuy", Label = "FVG ↑", PriceLow = 2645, PriceHigh = 2648 }
                    ]
                }
            ],
            TimeframeAnalyses =
            [
                new TimeframeAnalysis { Timeframe = "60", Low = 2630, High = 2670, Mid = 2650 }
            ]
        };

        var marks = Mt5ZoneMarkupBuilder.Build("XAUUSD", snapshot, "60", lastPrice: 2650m);

        Assert.DoesNotContain(marks, m => m.Label.Contains("Value Area", StringComparison.OrdinalIgnoreCase)
                                          && !m.Kind.Equals("poc", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(marks, m => m.Label.Contains("Área de valor", StringComparison.OrdinalIgnoreCase)
                                          && !m.Kind.Equals("poc", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(marks, m => m.Kind == "vwap"); // weak VWAP filtered on XAU
        Assert.DoesNotContain(marks, m => m.Kind == "premium"); // inactive Premium skipped
        Assert.Contains(marks, m => m.Kind is "demand" or "discount" or "fvg" or "poc");
        Assert.Contains(marks, m => m.Label.StartsWith("Demanda", StringComparison.Ordinal)
                                    || m.Label.StartsWith("Desconto", StringComparison.Ordinal)
                                    || m.Label.StartsWith("FVG", StringComparison.Ordinal)
                                    || m.Label.StartsWith("POC", StringComparison.Ordinal));
        Assert.True(marks.Count <= Mt5ZoneMarkupBuilder.XauMaxZones);
    }

    [Fact]
    public void Build_DedupesHeavyOverlaps()
    {
        var snapshot = new TradingIntelligenceSnapshot
        {
            Asset = "XAUUSD",
            OperationalZones =
            [
                new OperationalZone
                {
                    Type = OperationalZoneType.StrongBuy,
                    Label = "Demand / Order Block 60min",
                    PriceLow = 100,
                    PriceHigh = 110,
                    ConfluenceScore = 90
                },
                new OperationalZone
                {
                    Type = OperationalZoneType.ModerateBuy,
                    Label = "Demand / Order Block 15min",
                    PriceLow = 101,
                    PriceHigh = 109,
                    ConfluenceScore = 70
                }
            ]
        };

        var marks = Mt5ZoneMarkupBuilder.Build("XAUUSD", snapshot, lastPrice: 105m);
        Assert.Single(marks);
        Assert.Equal("demand", marks[0].Kind);
    }
}
