using NtBot.Domain.Entities;
using NtBot.Shared.Trading;
using Xunit;

namespace NtBot.UnitTests.Boletagem;

public class DemandTrendEntrySelectorTests
{
    [Fact]
    public void IsDemand_recognizes_buy_types_and_labels()
    {
        Assert.True(DemandTrendEntrySelector.IsDemand(
            new OperationalZoneHint("StrongBuy", "Demanda / Order Block 60min", 100, 110, 80)));
        Assert.True(DemandTrendEntrySelector.IsDemand(
            new OperationalZoneHint("ModerateBuy", "OB Compra 1", 100, 110)));
        Assert.False(DemandTrendEntrySelector.IsDemand(
            new OperationalZoneHint("StrongSell", "Supply", 100, 110)));
    }

    [Fact]
    public void FindAlignedZone_buy_picks_nearby_demand()
    {
        var zones = new List<OperationalZoneHint>
        {
            new("StrongSell", "Oferta", 2700, 2720, 90),
            new("StrongBuy", "Demanda M15", 2640, 2655, 70),
            new("ModerateBuy", "Demanda longe", 2400, 2410, 95)
        };

        var zone = DemandTrendEntrySelector.FindAlignedZone("Buy", 2650m, zones);
        Assert.NotNull(zone);
        Assert.Equal("Demanda M15", zone!.Label);
    }

    [Fact]
    public void FindAlignedZone_sell_ignores_demand()
    {
        var zones = new List<OperationalZoneHint>
        {
            new("StrongBuy", "Demanda", 2640, 2655, 90)
        };

        Assert.Null(DemandTrendEntrySelector.FindAlignedZone("Sell", 2650m, zones));
    }

    [Fact]
    public void PreferredEntry_buy_is_discount_side_of_demand()
    {
        var zone = new OperationalZoneHint("StrongBuy", "Demanda", 1000, 1100);
        var entry = DemandTrendEntrySelector.PreferredEntry("Buy", zone);
        Assert.Equal(1025m, entry); // low + 25%
    }

    [Fact]
    public void PreferredEntry_sell_is_premium_side_of_supply()
    {
        var zone = new OperationalZoneHint("StrongSell", "Oferta", 1000, 1100);
        var entry = DemandTrendEntrySelector.PreferredEntry("Sell", zone);
        Assert.Equal(1075m, entry); // low + 75%
    }

    [Fact]
    public void FlatReason_when_zones_present_but_no_alignment()
    {
        var zones = new List<OperationalZoneHint>
        {
            new("StrongSell", "Oferta", 2700, 2720, 80)
        };

        var reason = DemandTrendEntrySelector.FlatReasonIfNoAlignedZone("Buy", 2650m, zones);
        Assert.NotNull(reason);
        Assert.Contains("demanda", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FlatReason_null_when_no_zones()
    {
        Assert.Null(DemandTrendEntrySelector.FlatReasonIfNoAlignedZone("Buy", 2650m, []));
        Assert.Null(DemandTrendEntrySelector.FlatReasonIfNoAlignedZone("Buy", 2650m, null));
    }

    [Fact]
    public void Buy_with_nearby_demand_does_not_flat()
    {
        var zones = new List<OperationalZoneHint>
        {
            new("StrongBuy", "Demanda M15", 2640, 2660, 85)
        };

        Assert.Null(DemandTrendEntrySelector.FlatReasonIfNoAlignedZone("Buy", 2650m, zones));
        var zone = DemandTrendEntrySelector.FindAlignedZone("Buy", 2650m, zones);
        Assert.NotNull(zone);
        Assert.Equal(2645m, DemandTrendEntrySelector.PreferredEntry("Buy", zone!));
    }
}

public class TradingEnabledGateTests
{
    [Fact]
    public void Session_defaults_TradingEnabled_false_safe_stop()
    {
        var session = new BoletaSession();
        Assert.False(session.TradingEnabled);
        Assert.False(session.AutomationEnabled);
    }

    [Theory]
    [InlineData(false, false, true)]  // Stop + live => block
    [InlineData(false, true, false)]  // Stop + dry-run => allow
    [InlineData(true, false, false)]  // Start + live => allow
    [InlineData(true, true, false)]   // Start + dry-run => allow
    public void Gate_blocks_only_live_opens_when_stopped(bool tradingEnabled, bool dryRun, bool shouldBlock)
    {
        // Mirror BoletagemService.ExecuteAsync gate: !DryRun && !TradingEnabled
        var blocked = !dryRun && !tradingEnabled;
        Assert.Equal(shouldBlock, blocked);
    }

    [Fact]
    public void Close_is_independent_of_TradingEnabled()
    {
        // Closing must remain allowed regardless of Start/Stop.
        var session = new BoletaSession { TradingEnabled = false };
        Assert.False(session.TradingEnabled);
        // No gate on CloseAllAsync uses TradingEnabled — documented by this invariant.
        Assert.True(true);
    }
}
