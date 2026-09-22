using NinjaTrader.NinjaScript.Indicators.NtBot;

namespace NtBot.UnitTests.MnqFlow;

public class MnqFlowMathTests
{
    [Fact]
    public void AggressionRatio_UsesMaxSellOne()
    {
        Assert.Equal(5, FlowMath.AggressionRatio(5, 0), 5);
        Assert.True(FlowMath.AggressionRatio(1000, 1) <= 20);
    }

    [Fact]
    public void AggressionScore_BuyerHeavy_Positive()
    {
        Assert.True(FlowMath.AggressionScore(90, 10) > 50);
        Assert.True(FlowMath.AggressionScore(10, 90) < -50);
        Assert.Equal(0, FlowMath.AggressionScore(0, 0), 5);
    }

    [Fact]
    public void ZScore_ConstantSeries_Zero()
    {
        var w = new[] { 2.0, 2.0, 2.0, 2.0 };
        Assert.Equal(0, FlowMath.ZScore(2, w, 4), 5);
    }

    [Fact]
    public void ZScore_Outlier_Large()
    {
        var w = new[] { 1.0, 1.1, 0.9, 1.0, 5.0 };
        Assert.True(FlowMath.ZScore(5, w, 5) > 1.5);
    }

    [Fact]
    public void Pearson_PerfectPositive()
    {
        var x = new[] { 1.0, 2, 3, 4, 5 };
        var y = new[] { 2.0, 4, 6, 8, 10 };
        Assert.Equal(1, FlowMath.Pearson(x, y, 5), 5);
    }

    [Fact]
    public void Pearson_Negative()
    {
        var x = new[] { 1.0, 2, 3, 4, 5 };
        var y = new[] { 10.0, 8, 6, 4, 2 };
        Assert.True(FlowMath.Pearson(x, y, 5) < -0.99);
    }

    [Fact]
    public void WeightedScore_BullishComponents()
    {
        var s = FlowMath.WeightedFlowScore(80, 80, 80, 50, 50, 20, 40, 40, 40, 25, 20, 15, 10, 10, 5, 5, 5, 5);
        Assert.True(s > 50);
        Assert.True(s <= 100);
    }

    [Fact]
    public void WeightedScore_BearishComponents()
    {
        var s = FlowMath.WeightedFlowScore(-80, -80, -80, -50, -50, -20, -40, -40, -40, 25, 20, 15, 10, 10, 5, 5, 5, 5);
        Assert.True(s < -50);
    }

    [Fact]
    public void WeightedScore_RangeNearZero()
    {
        var s = FlowMath.WeightedFlowScore(5, -5, 0, 0, 0, 0, 0, 0, 0, 25, 20, 15, 10, 10, 5, 5, 5, 5);
        Assert.True(Math.Abs(s) < 15);
    }

    [Fact]
    public void ApplyOrderFlowQuality_ReducesWhenLow()
    {
        var full = FlowMath.ApplyOrderFlowQuality(80, 90, 55);
        var low = FlowMath.ApplyOrderFlowQuality(80, 20, 55);
        Assert.Equal(80, full, 5);
        Assert.True(low < full);
    }

    [Fact]
    public void Confidence_HighWhenQualityAndConfirms()
    {
        var c = FlowMath.ConfidenceScore(90, 7, 8, false, 80, 5, 0.9, 70);
        Assert.True(c > 60);
    }

    [Fact]
    public void Confidence_DropsOnDivergenceAndLowQuality()
    {
        var hi = FlowMath.ConfidenceScore(90, 7, 8, false, 80, 0, 0.9, 70);
        var lo = FlowMath.ConfidenceScore(30, 1, 8, true, 20, 30, 0.1, 70);
        Assert.True(lo < hi);
    }

    [Fact]
    public void BuyerAbsorption_WhenSellAggressionFlatPrice()
    {
        var e = new AbsorptionEngine();
        e.Evaluate(10, 200, 50, 0.2, 10, 500, 200, true, false, 1.8);
        Assert.Equal(AbsorptionKind.BuyerAbsorption, e.Kind);
        Assert.True(e.Score > 0);
    }

    [Fact]
    public void SellerAbsorption_WhenBuyAggressionFlatPrice()
    {
        var e = new AbsorptionEngine();
        e.Evaluate(200, 10, 50, 0.2, 10, 500, 200, false, true, 1.8);
        Assert.Equal(AbsorptionKind.SellerAbsorption, e.Kind);
        Assert.True(e.Score < 0);
    }

    [Fact]
    public void Imbalance_BuyAndStacked()
    {
        var e = new ImbalanceEngine();
        e.EvaluateLevel(30, 5, 3);
        Assert.Equal(ImbalanceKind.BuyImbalance, e.Kind);
        e.EvaluateLevel(30, 5, 3);
        Assert.True(e.StackedImbalance >= 2);
        e.EvaluateLevel(5, 30, 3);
        Assert.Equal(ImbalanceKind.SellImbalance, e.Kind);
    }

    [Fact]
    public void RelativeStrength_Leadership()
    {
        Assert.True(FlowMath.RelativeStrength(0.008, 0.002) > 0);
        var lead = FlowMath.LeadershipScore(0.008, 0.002, 0.002, -0.004, true, true, true);
        Assert.True(lead > 0);
    }

    [Fact]
    public void OrderFlow_LastAtAskIsBuy()
    {
        var of = new OrderFlowEngine(20);
        of.ProcessLast(100.25, 10, 100.00, 100.25, out var classified);
        Assert.True(classified);
        Assert.Equal(10, of.AggressiveBuyVolume);
        of.ProcessLast(100.00, 7, 100.00, 100.25, out classified);
        Assert.True(classified);
        Assert.Equal(7, of.AggressiveSellVolume);
        Assert.Equal(3, of.BarDelta);
    }

    [Fact]
    public void OrderFlow_InsideSpread_NotClassified()
    {
        var of = new OrderFlowEngine(20);
        of.ProcessLast(100.12, 5, 100.00, 100.25, out var classified);
        Assert.False(classified);
        Assert.Equal(0, of.BarDelta);
    }

    [Fact]
    public void Divergence_BullishPriceLowDeltaHigher()
    {
        var d = new DivergenceEngine(8);
        for (var i = 0; i < 6; i++)
            d.Update(100 - i, -10 - i, 100, 0, 0, 0, false);
        d.Update(90, -5, 100, 0, 0, 0, false);
        Assert.True(d.BullishDelta);
    }

    [Fact]
    public void DataQuality_HistoricalTapeMissing_Low()
    {
        var q = new DataQualityEngine();
        q.SetHistoricalWithoutTape(true);
        Assert.True(q.Score() < 50);
        Assert.False(q.VolumeConsistencyCheck(-1, 0, 0));
    }

    [Fact]
    public void Correlation_MissingInstrument_DoesNotThrow()
    {
        var c = new CorrelationEngine(10);
        c.Push(100, 0, 0, 0, 0, false, false, false, false);
        Assert.Equal(0, c.CombinedScore);
        Assert.False(c.HasNq);
    }

    [Fact]
    public void Signal_SetupValid_BuyRequiresConfluence()
    {
        var s = new SignalEngine();
        Assert.False(s.IsTradeSetupValid(true, 80, 40, 60, -60, 70, true, true, true, true, true));
        Assert.True(s.IsTradeSetupValid(true, 80, 80, 60, -60, 70, true, true, true, true, true));
        Assert.False(s.IsTradeSetupValid(false, 80, 80, 60, -60, 70, true, true, true, true, true));
        Assert.True(s.IsTradeSetupValid(false, -80, 80, 60, -60, 70, true, true, true, true, true));
    }

    [Fact]
    public void Signal_DisplayBias_DirectionalWait_NotGold()
    {
        var s = new SignalEngine();
        // Trade gate stays WAIT; UI shows sell bias for score -20 (Markdown-ish).
        s.Classify(-20, 53, 75, 60, -60, -75, 70);
        Assert.Equal(SignalState.WaitConfirmation, s.State);
        Assert.Equal(DisplayBias.BiasSell, s.Bias);
        Assert.Equal("BIAS_SELL", s.BiasText);
        Assert.Equal("VIÉS VENDA", DashboardBuilder.ToPortugueseBias(s.BiasText));

        // |score| small → gold AGUARDAR
        s.Classify(-9, 55, 75, 60, -60, -75, 70);
        Assert.Equal(SignalState.WaitConfirmation, s.State);
        Assert.Equal(DisplayBias.Wait, s.Bias);

        // conf < minConf*0.6 → SEM TRADE (gates stay closed)
        s.Classify(-9, 41, 75, 60, -60, -75, 70);
        Assert.Equal(SignalState.NoTrade, s.State);
        Assert.Equal(DisplayBias.NoTrade, s.Bias);

        s.Classify(25, 50, 75, 60, -60, -75, 70);
        Assert.Equal(DisplayBias.BiasBuy, s.Bias);

        s.Classify(65, 50, 75, 60, -60, -75, 70);
        Assert.Equal(SignalState.WeakBuy, s.State);
        Assert.Equal(DisplayBias.BiasBuy, s.Bias);
        Assert.False(s.IsTradeSetupValid(true, 65, 50, 60, -60, 70, true, true, true, true, true));
    }

    [Fact]
    public void MultiTimeframe_Weights()
    {
        var m = FlowMath.MultiTimeframeScore(60, 70, 80, 80, 90, 30, 25, 20, 15, 10);
        Assert.True(m > 60 && m < 90);
    }

    [Fact]
    public void AbnormalVolume_SpikeFlag()
    {
        var q = new DataQualityEngine();
        Assert.True(q.IsSpike(1000, 10));
        Assert.False(q.IsSpike(12, 10));
    }

    [Fact]
    public void CsvBuffer_DoesNotFlushTiny()
    {
        var b = new CsvExportBuffer(100000);
        b.Enqueue("t", "MNQ", 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 50, "BUY", "MARKUP", "TREND_UP");
        Assert.True(b.HasPending);
        Assert.False(b.ShouldFlush);
        Assert.Contains("InstitutionalFlowScore", b.Drain());
    }
}
