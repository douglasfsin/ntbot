using NtBot.Domain.Services.Portfolio;

namespace NtBot.UnitTests.Portfolio;

public class PortfolioReturnMathTests
{
    [Fact]
    public void Twr_TwoSubPeriods_LinksCorrectly()
    {
        // Period 1: 100 → 110, CF=0 → +10%
        // Period 2: 110 → 121, CF=0 → +10%
        // Linked TWR = 1.1 * 1.1 - 1 = 21%
        var periods = new List<PortfolioReturnMath.SubPeriod>
        {
            new(100m, 110m, 0m),
            new(110m, 121m, 0m)
        };

        var twr = PortfolioReturnMath.CalculateTwr(periods);
        Assert.NotNull(twr);
        Assert.Equal(0.21m, twr.Value, 6);
    }

    [Fact]
    public void Twr_WithExternalContribution_IsolatesMarketReturn()
    {
        // Start 100, end before CF 110 (+10%), then +50 CF → MV 160, end 176
        // Sub1: BMV=100, EMV=110, CF=0 → 10%
        // Sub2: BMV=160 (110+50), EMV=176, CF=0 → 10%
        // (Classic split: contribution at boundary)
        var periods = new List<PortfolioReturnMath.SubPeriod>
        {
            new(100m, 110m, 0m),
            new(160m, 176m, 0m)
        };

        var twr = PortfolioReturnMath.CalculateTwr(periods);
        Assert.NotNull(twr);
        Assert.Equal(0.21m, twr.Value, 6);
    }

    [Fact]
    public void Twr_ContributionDuringPeriod_UsesFormula()
    {
        // BMV=100, EMV=160, CF=+50 → r = (160-100-50)/100 = 10%
        var twr = PortfolioReturnMath.CalculateTwr([new(100m, 160m, 50m)]);
        Assert.NotNull(twr);
        Assert.Equal(0.10m, twr.Value, 6);
    }

    [Fact]
    public void SinglePeriodReturn_FromCostAndMtm()
    {
        var r = PortfolioReturnMath.SinglePeriodReturn(1000m, 1100m);
        Assert.Equal(0.10m, r);
    }

    [Fact]
    public void SinglePeriodReturn_ZeroCost_ReturnsNull()
    {
        Assert.Null(PortfolioReturnMath.SinglePeriodReturn(0m, 100m));
    }

    [Fact]
    public void Mwr_SyntheticCashflows_PositiveReturn()
    {
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddDays(365);
        var cashflows = new List<PortfolioReturnMath.DatedCashflow>
        {
            new(t0, 1000m)
        };

        var mwr = PortfolioReturnMath.CalculateMwrIrr(cashflows, 1100m, t1);
        Assert.NotNull(mwr);
        // ~10% annualized
        Assert.InRange(mwr.Value, 0.08m, 0.12m);
    }

    [Fact]
    public void Twr_Empty_ReturnsNull()
    {
        Assert.Null(PortfolioReturnMath.CalculateTwr([]));
    }

    [Fact]
    public void BuildSubPeriods_FromSnapshotsAndCashflows()
    {
        var vals = new List<PortfolioReturnMath.DatedValuation>
        {
            new(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), 100m),
            new(new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc), 160m),
            new(new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), 176m)
        };
        var cfs = new List<PortfolioReturnMath.DatedCashflow>
        {
            new(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), 50m) // aporte mid first period
        };

        var periods = PortfolioReturnMath.BuildSubPeriodsFromSnapshots(vals, cfs);
        Assert.Equal(2, periods.Count);
        // Period1: BMV=100, EMV=160, CF=50 → r=(160-100-50)/100=10%
        Assert.Equal(100m, periods[0].BeginningMarketValue);
        Assert.Equal(160m, periods[0].EndingMarketValue);
        Assert.Equal(50m, periods[0].ExternalCashflow);
        // Period2: no CF → 160→176 = 10%
        Assert.Equal(0m, periods[1].ExternalCashflow);

        var twr = PortfolioReturnMath.CalculateTwr(periods);
        Assert.NotNull(twr);
        Assert.Equal(0.21m, twr.Value, 6);
    }

    [Fact]
    public void BuildSubPeriods_NeedsTwoValuations()
    {
        var vals = new List<PortfolioReturnMath.DatedValuation>
        {
            new(DateTime.UtcNow, 100m)
        };
        Assert.Empty(PortfolioReturnMath.BuildSubPeriodsFromSnapshots(vals, []));
    }

    [Fact]
    public void Mwr_WithWithdrawal_StillComputes()
    {
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var flows = new List<(DateTime, decimal)>
        {
            (t0, 1000m),
            (t0.AddDays(180), -200m)
        };
        var mwr = PortfolioReturnMath.CalculateMwrFromSignedFlows(flows, 900m, t0.AddDays(365));
        Assert.NotNull(mwr);
    }
}
