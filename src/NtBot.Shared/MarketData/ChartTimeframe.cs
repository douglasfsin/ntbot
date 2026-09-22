namespace NtBot.Shared.MarketData;

public static class ChartTimeframe
{
    /// <summary>Timeframes that can be built by aggregating stored M1 bars.</summary>
    public static readonly int[] AggregatableMinutes = [1, 3, 5, 15, 30, 60, 240, 1440];

    public static string Normalize(string? timeframe)
    {
        if (string.IsNullOrWhiteSpace(timeframe))
            return "M5";

        var tf = timeframe.Trim().ToUpperInvariant();
        return tf switch
        {
            "1" or "1M" or "M1" => "M1",
            "3" or "3M" or "M3" => "M3",
            "5" or "5M" or "M5" => "M5",
            "15" or "15M" or "M15" => "M15",
            "30" or "30M" or "M30" => "M30",
            "60" or "1H" or "H1" => "H1",
            "240" or "4H" or "H4" => "H4",
            "1440" or "1D" or "D1" => "D1",
            _ => tf
        };
    }

    public static string ToChartKey(string? timeframe) =>
        Normalize(timeframe) switch
        {
            "M1" => "1",
            "M3" => "3",
            "M5" => "5",
            "M15" => "15",
            "M30" => "30",
            "H1" => "60",
            "H4" => "240",
            "D1" => "1440",
            _ => timeframe?.Trim() ?? "5"
        };

    public static int ToMinutes(string? timeframe) =>
        Normalize(timeframe) switch
        {
            "M1" => 1,
            "M3" => 3,
            "M5" => 5,
            "M15" => 15,
            "M30" => 30,
            "H1" => 60,
            "H4" => 240,
            "D1" => 1440,
            _ => 5
        };

    public static bool CanAggregateFromM1(string? timeframe)
    {
        var minutes = ToMinutes(timeframe);
        return AggregatableMinutes.Contains(minutes);
    }

    public static IReadOnlyList<string> Aliases(string? timeframe)
    {
        return Normalize(timeframe) switch
        {
            "M1" => ["M1", "1", "1M", "1m"],
            "M3" => ["M3", "3", "3M", "3m"],
            "M5" => ["M5", "5", "5M", "5m"],
            "M15" => ["M15", "15", "15M", "15m"],
            "M30" => ["M30", "30", "30M", "30m"],
            "H1" => ["H1", "60", "1H", "1h"],
            "H4" => ["H4", "240", "4H", "4h"],
            "D1" => ["D1", "1440", "1D", "1d"],
            _ => [Normalize(timeframe)]
        };
    }
}
