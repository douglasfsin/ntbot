using NtBot.Shared.MarketData;

namespace NtBot.MarketData.Parsing;

public static class ProfitDllTopicParser
{
    public static bool TryParseTick(string ticker, string topic, object? value, out ProfitDllTick tick)
    {
        tick = default!;
        if (string.IsNullOrWhiteSpace(ticker) || string.IsNullOrWhiteSpace(topic) || value is null)
            return false;

        if (!TryToDouble(value, out var numeric))
            return false;

        var canonical = CandleSymbolAliases.Canonical(ticker);
        tick = new ProfitDllTick(
            Ticker: ticker.Trim().ToUpperInvariant(),
            CanonicalSymbol: canonical,
            Topic: topic.Trim().ToUpperInvariant(),
            Value: numeric,
            TimestampUtc: DateTime.UtcNow);

        return true;
    }

    public static bool IsPriceTopic(string topic) =>
        topic.Equals("ULT", StringComparison.OrdinalIgnoreCase)
        || topic.Equals("FEC", StringComparison.OrdinalIgnoreCase);

    public static bool IsDailyOhlcTopic(string topic) =>
        topic is "ABE" or "MAX" or "MIN" or "FEC" or "AJU";

    private static bool TryToDouble(object value, out double result)
    {
        result = 0;
        return value switch
        {
            double d => (result = d) == d,
            float f => (result = f) == f,
            decimal m => (result = (double)m) == (double)m,
            int i => (result = i) == i,
            long l => (result = l) == l,
            string s => double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out result),
            _ => false
        };
    }
}

public readonly record struct ProfitDllTick(
    string Ticker,
    string CanonicalSymbol,
    string Topic,
    double Value,
    DateTime TimestampUtc);
