using NtBot.Analytics.Configuration;

namespace NtBot.Analytics.Services;

public static class QuantSessionClock
{
    public static DateTimeOffset ToSession(DateTime utc, QuantStatisticsOptions options)
    {
        var tz = Resolve(options.SessionTimezone);
        return TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), tz);
    }

    public static string SessionLabel(DateTime utc, QuantStatisticsOptions options)
    {
        var local = ToSession(utc, options);
        var start = Parse(options.SessionStart);
        var end = Parse(options.SessionEnd);
        var auctionStart = Parse(options.AuctionStart);
        var auctionEnd = Parse(options.AuctionEnd);
        var t = local.TimeOfDay;
        if (t >= auctionStart && t < auctionEnd)
            return "AUCTION";
        if (t >= start && t <= end)
            return "REGULAR";
        return "OFF_HOURS";
    }

    public static DateTime SessionOpenUtc(DateOnly date, QuantStatisticsOptions options)
        => LocalDateTimeToUtc(date, Parse(options.SessionStart), options);

    public static DateTime AuctionStartUtc(DateOnly date, QuantStatisticsOptions options)
        => LocalDateTimeToUtc(date, Parse(options.AuctionStart), options);

    public static DateTime AuctionEndUtc(DateOnly date, QuantStatisticsOptions options)
        => LocalDateTimeToUtc(date, Parse(options.AuctionEnd), options);

    public static TimeZoneInfo Resolve(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static TimeSpan Parse(string hhmm)
        => TimeSpan.ParseExact(hhmm, @"hh\:mm", null);

    private static DateTime LocalDateTimeToUtc(DateOnly date, TimeSpan time, QuantStatisticsOptions options)
    {
        var tz = Resolve(options.SessionTimezone);
        var unspecified = date.ToDateTime(TimeOnly.FromTimeSpan(time), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
    }
}
