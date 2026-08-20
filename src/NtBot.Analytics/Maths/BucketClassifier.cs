namespace NtBot.Analytics.Maths;

public static class BucketClassifier
{
    public static string VolumeZ(decimal? z) => z switch
    {
        null => "UNKNOWN",
        < -2m => "LT_MINUS_2",
        < -1m => "MINUS_2_TO_MINUS_1",
        < 0m => "MINUS_1_TO_0",
        < 1m => "0_TO_1",
        < 2m => "1_TO_2",
        < 3m => "2_TO_3",
        _ => "GT_3"
    };

    public static string Delta(decimal? z) => z switch
    {
        null => "UNKNOWN",
        < -2m => "EXTREME_SELL",
        < -1.2m => "STRONG_SELL",
        < -0.4m => "SELL",
        <= 0.4m => "NEUTRAL",
        < 1.2m => "BUY",
        < 2m => "STRONG_BUY",
        _ => "EXTREME_BUY"
    };

    public static string BookImbalance(decimal? imbalance) => imbalance switch
    {
        null => "UNKNOWN",
        < 0.20m => "0.00-0.20",
        < 0.30m => "0.20-0.30",
        < 0.40m => "0.30-0.40",
        < 0.60m => "0.40-0.60",
        < 0.70m => "0.60-0.70",
        < 0.80m => "0.70-0.80",
        _ => "0.80-1.00"
    };

    public static string Volatility(decimal? percentile) => percentile switch
    {
        null => "UNKNOWN",
        < 20m => "VERY_LOW",
        < 40m => "LOW",
        < 60m => "NORMAL",
        < 80m => "HIGH",
        _ => "VERY_HIGH"
    };

    public static string Auction(int score) => score switch
    {
        >= 60 => "STRONG_BUY",
        >= 20 => "BUY",
        > -20 => "NEUTRAL",
        > -60 => "SELL",
        _ => "STRONG_SELL"
    };

    public static string SampleSize(int n, int insufficient, int low, int medium) => n switch
    {
        _ when n < insufficient => "INSUFFICIENT_SAMPLE",
        _ when n < low => "LOW_SAMPLE",
        _ when n < medium => "MEDIUM_SAMPLE",
        _ => "HIGH_SAMPLE"
    };

    public static string LargeTrade(decimal? percentile) => percentile switch
    {
        null => "UNKNOWN",
        >= 99m => "EXTREME",
        >= 95m => "VERY_LARGE",
        >= 90m => "LARGE",
        _ => "NORMAL"
    };

    public static string PriceVsVwap(decimal close, decimal? vwap)
    {
        if (vwap is null)
            return "UNKNOWN";
        if (close > vwap)
            return "ABOVE";
        if (close < vwap)
            return "BELOW";
        return "AT";
    }
}
