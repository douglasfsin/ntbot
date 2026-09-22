using NtBot.Shared.Trading;
using Xunit;

namespace NtBot.UnitTests.Boletagem;

public class ScalpShortRiskRulesTests
{
    private const decimal XauTick = 0.01m;

    [Fact]
    public void ComputeTechnicalStopLoss_buy_is_prev_low_minus_2_ticks()
    {
        // prev candle: high 2652 / low 2648 → Buy SL = 2648 - 0.02 = 2647.98
        var sl = ScalpShortRiskRules.ComputeTechnicalStopLoss(
            "Buy", previousHigh: 2652m, previousLow: 2648m, tickSize: XauTick);

        Assert.Equal(2647.98m, sl);
    }

    [Fact]
    public void ComputeTechnicalStopLoss_sell_is_prev_high_plus_2_ticks()
    {
        // Sell SL = 2652 + 0.02 = 2652.02
        var sl = ScalpShortRiskRules.ComputeTechnicalStopLoss(
            "Sell", previousHigh: 2652m, previousLow: 2648m, tickSize: XauTick);

        Assert.Equal(2652.02m, sl);
    }

    [Theory]
    [InlineData(0, 2648, 0.01)]
    [InlineData(2652, 0, 0.01)]
    [InlineData(2652, 2648, 0)]
    [InlineData(2640, 2650, 0.01)] // low > high
    public void ComputeTechnicalStopLoss_returns_null_on_invalid_inputs(
        decimal high, decimal low, decimal tick)
    {
        Assert.Null(ScalpShortRiskRules.ComputeTechnicalStopLoss("Buy", high, low, tick));
    }

    [Fact]
    public void BuildLevels_buy_uses_technical_sl_shared_across_levels_and_tp10()
    {
        // Candle anterior com range amplo para os níveis adversos permanecerem acima do SL.
        var levels = ScalpShortRiskRules.BuildLevels(
            "Buy", 2650m, 0.10m,
            previousHigh: 2645m, previousLow: 2630m, tickSize: XauTick);

        Assert.Equal(3, levels.Count);
        var expectedSl = 2629.98m; // 2630 - 2*0.01

        // L1 mais adverso (abaixo), menor volume
        Assert.Equal(1, levels[0].Level);
        Assert.Equal(2644m, levels[0].Entry); // 2650 - 6
        Assert.Equal(expectedSl, levels[0].StopLoss);
        Assert.Equal(2654m, levels[0].TakeProfit); // entry + 10
        Assert.Equal(0.02m, levels[0].Volume); // 20%

        Assert.Equal(2647m, levels[1].Entry); // 2650 - 3
        Assert.Equal(expectedSl, levels[1].StopLoss); // mesmo SL técnico
        Assert.Equal(0.03m, levels[1].Volume); // 30%

        Assert.Equal(2650m, levels[2].Entry); // referência
        Assert.Equal(expectedSl, levels[2].StopLoss);
        Assert.Equal(2660m, levels[2].TakeProfit);
        Assert.Equal(0.05m, levels[2].Volume); // 50%

        Assert.True(levels[0].Volume < levels[1].Volume);
        Assert.True(levels[1].Volume < levels[2].Volume);
        Assert.True(levels[0].Entry < levels[2].Entry);
        Assert.All(levels, l => Assert.True(l.StopLoss < l.Entry));
    }

    [Fact]
    public void BuildLevels_sell_staggers_above_reference_with_technical_sl()
    {
        var levels = ScalpShortRiskRules.BuildLevels(
            "Sell", 2650m, 0.10m,
            previousHigh: 2670m, previousLow: 2655m, tickSize: XauTick);

        Assert.Equal(3, levels.Count);
        var expectedSl = 2670.02m; // 2670 + 2*0.01
        Assert.Equal(2656m, levels[0].Entry); // mais adverso (acima)
        Assert.Equal(expectedSl, levels[0].StopLoss);
        Assert.Equal(2646m, levels[0].TakeProfit); // entry - 10
        Assert.Equal(2650m, levels[2].Entry);
        Assert.Equal(expectedSl, levels[2].StopLoss);
        Assert.Equal(2640m, levels[2].TakeProfit);
        Assert.True(levels[0].Entry > levels[2].Entry);
        Assert.True(levels[0].Volume < levels[2].Volume);
        Assert.All(levels, l => Assert.True(l.StopLoss > l.Entry));
    }

    [Fact]
    public void BuildLevels_skips_adverse_entries_on_wrong_side_of_technical_sl()
    {
        // prevLow próximo do preço: L1/L2 cairiam no/abaixo do SL e são omitidos.
        var levels = ScalpShortRiskRules.BuildLevels(
            "Buy", 2650m, 0.10m,
            previousHigh: 2652m, previousLow: 2648m, tickSize: XauTick);

        Assert.Single(levels);
        Assert.Equal(2650m, levels[0].Entry);
        Assert.Equal(2647.98m, levels[0].StopLoss);
    }

    [Fact]
    public void BuildLevels_win_tick_size_rounds_to_5_points()
    {
        // WIN tick = 5; prev low 120_000 → Buy SL = 120_000 - 10 = 119_990
        var levels = ScalpShortRiskRules.BuildLevels(
            "Buy", 120_050m, 1m,
            previousHigh: 120_100m, previousLow: 120_000m, tickSize: 5m);

        Assert.Equal(119_990m, levels[0].StopLoss);
        Assert.All(levels, l => Assert.Equal(119_990m, l.StopLoss));
    }

    [Theory]
    [InlineData("Buy", 2650, 2657, true, 2652)]   // +7 → SL entry+2
    [InlineData("Buy", 2650, 2656.99, false, 0)]  // ainda não
    [InlineData("Sell", 2650, 2643, true, 2648)]  // -7 → SL entry-2
    [InlineData("Sell", 2650, 2643.01, false, 0)]
    public void BreakevenLock_triggers_at_7_locks_2(
        string direction, decimal entry, decimal price, bool expectApply, decimal expectedSl)
    {
        // SL técnico inicial abaixo/acima da entrada — BE só aperta após +$7
        var technicalSl = direction == "Buy" ? entry - 5m : entry + 5m;
        var update = ScalpShortRiskRules.TryBreakevenLock(
            direction, price, entry, previousPeak: null, currentStopLoss: technicalSl);

        Assert.NotNull(update);
        Assert.Equal(expectApply, update.Value.ShouldApply);
        if (expectApply)
            Assert.Equal(expectedSl, update.Value.StopLoss);
    }

    [Fact]
    public void BreakevenLock_does_not_loosen_existing_tighter_stop()
    {
        // Buy já com SL em +3 (melhor que +2) — não afrouxa
        var update = ScalpShortRiskRules.TryBreakevenLock(
            "Buy", 2660m, 2650m, previousPeak: 2660m, currentStopLoss: 2653m);

        Assert.NotNull(update);
        Assert.False(update.Value.ShouldApply);
    }

    [Fact]
    public void BreakevenLock_does_not_apply_before_trigger_leaving_technical_sl()
    {
        var technicalSl = 2647.98m;
        var update = ScalpShortRiskRules.TryBreakevenLock(
            "Buy", 2655m, 2650m, previousPeak: null, currentStopLoss: technicalSl);

        Assert.NotNull(update);
        Assert.False(update.Value.ShouldApply);
        Assert.Equal(technicalSl, update.Value.StopLoss);
    }

    [Fact]
    public void MarketDrivers_still_blocks_opposite_side_for_scalp_contract()
    {
        // Contrato usado por ScalpShortBoletaStrategy via ResolveDirection / ExecuteAsync
        Assert.False(MarketDriversDirectionGuard.IsAllowed("COMPRA MODERADA", "Sell"));
        Assert.False(MarketDriversDirectionGuard.IsAllowed("VENDA FORTE", "Buy"));
        Assert.True(MarketDriversDirectionGuard.IsAllowed("COMPRA MODERADA", "Buy"));
        Assert.True(MarketDriversDirectionGuard.IsAllowed("VENDA FRACA", "Sell"));
        Assert.True(MarketDriversDirectionGuard.IsAllowed("NEUTRO", "Buy"));
        Assert.Equal("Buy", MarketDriversDirectionGuard.AllowedDirectionOrNull("COMPRA MODERADA"));
        Assert.Equal("Sell", MarketDriversDirectionGuard.AllowedDirectionOrNull("VENDA MODERADA"));
    }

    [Fact]
    public void SymbolTickSize_resolves_known_fallbacks_and_broker_override()
    {
        Assert.Equal(0.01m, SymbolTickSize.TryResolve("XAUUSD"));
        Assert.Equal(5m, SymbolTickSize.TryResolve("WIN"));
        Assert.Equal(0.5m, SymbolTickSize.TryResolve("WDO"));
        Assert.Equal(0.05m, SymbolTickSize.TryResolve("XAUUSD", brokerTickSize: 0.05m));
        Assert.Null(SymbolTickSize.TryResolve("UNKNOWNXYZ"));
    }
}
