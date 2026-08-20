namespace NtBot.Analytics.Maths;

public static class OutcomeMath
{
    public static decimal DirectionalReturn(string direction, decimal entry, decimal futurePrice)
        => IsBuy(direction) ? futurePrice - entry : entry - futurePrice;

    public static decimal Mfe(string direction, decimal entry, decimal maxPrice, decimal minPrice)
        => IsBuy(direction) ? maxPrice - entry : entry - minPrice;

    /// <summary>
    /// MAE is stored as a non-positive number representing adverse excursion.
    /// BUY: minPrice - entry; SELL: entry - maxPrice.
    /// </summary>
    public static decimal Mae(string direction, decimal entry, decimal maxPrice, decimal minPrice)
        => IsBuy(direction) ? minPrice - entry : entry - maxPrice;

    public static decimal? ReturnR(decimal directionalReturn, decimal? stopDistance)
    {
        if (stopDistance is null or 0)
            return null;
        return directionalReturn / Math.Abs(stopDistance.Value);
    }

    public static bool IsBuy(string direction)
        => direction.Equals("BUY", StringComparison.OrdinalIgnoreCase)
           || direction.Equals("LONG", StringComparison.OrdinalIgnoreCase)
           || direction.Equals("STRONG_BUY", StringComparison.OrdinalIgnoreCase);

    public static bool IsSell(string direction)
        => direction.Equals("SELL", StringComparison.OrdinalIgnoreCase)
           || direction.Equals("SHORT", StringComparison.OrdinalIgnoreCase)
           || direction.Equals("STRONG_SELL", StringComparison.OrdinalIgnoreCase);
}

public static class ExpectancyMath
{
    public static decimal Expectancy(decimal pWin, decimal averageWin, decimal pLoss, decimal averageLoss)
        => pWin * averageWin - pLoss * Math.Abs(averageLoss);

    public static decimal? ProfitFactor(IReadOnlyList<decimal> returns)
    {
        var grossProfit = returns.Where(r => r > 0).Sum();
        var grossLoss = Math.Abs(returns.Where(r => r < 0).Sum());
        if (grossLoss == 0)
            return grossProfit > 0 ? null : 0m;
        return grossProfit / grossLoss;
    }

    public static decimal? SharpeLike(IReadOnlyList<decimal> returns)
    {
        var mean = DescriptiveStats.Mean(returns);
        var std = DescriptiveStats.StdDev(returns);
        if (mean is null || std is null || std == 0)
            return null;
        return mean.Value / std.Value;
    }

    public static decimal? SortinoLike(IReadOnlyList<decimal> returns)
    {
        var mean = DescriptiveStats.Mean(returns);
        var downside = returns.Where(r => r < 0).Select(r => r).ToArray();
        var downStd = DescriptiveStats.StdDev(downside);
        if (mean is null || downStd is null || downStd == 0)
            return null;
        return mean.Value / downStd.Value;
    }

    public static decimal? MaxDrawdown(IReadOnlyList<decimal> returns)
    {
        if (returns.Count == 0)
            return null;
        decimal equity = 0;
        decimal peak = 0;
        decimal drawdown = 0;
        foreach (var value in returns)
        {
            equity += value;
            if (equity > peak)
                peak = equity;
            var current = equity - peak;
            if (current < drawdown)
                drawdown = current;
        }
        return drawdown;
    }
}
