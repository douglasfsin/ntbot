using NtBot.Api.Services.Profit;
using NtBot.Connector.Services;
using NtBot.Shared.Normalized;

namespace NtBot.Api.Services.Connector;

internal static class ConnectorLiveMapper
{
    public static RtdStatistics ToStatistics(ConnectorLiveSnapshot live)
    {
        var topicsWithData = live.Ticks.Count(kv => kv.Value.Last > 0);
        return new RtdStatistics
        {
            TotalDataReceived = live.TotalTicksReceived,
            LastDataReceived = live.UpdatedUtc,
            ServiceStarted = live.UpdatedUtc,
            TotalTopicsConnected = Math.Max(topicsWithData, 1),
            TopicsWithData = topicsWithData,
            DataRatePerSecond = live.IsLive ? 1 : 0,
            IsConnected = live.IsLive,
            SecondsSinceLastData = live.SecondsSinceLastData
        };
    }

    public static Dictionary<string, TickerStatus> ToTickers(ConnectorLiveSnapshot live)
    {
        var result = new Dictionary<string, TickerStatus>(StringComparer.OrdinalIgnoreCase);
        foreach (var (symbol, tick) in live.Ticks)
        {
            result[symbol] = new TickerStatus
            {
                Ticker = symbol,
                LogicalName = symbol,
                IsReceivingData = live.IsLive && tick.Last > 0,
                TotalTopics = 4,
                TopicsWithData = tick.Last > 0 ? 1 : 0,
                LastUpdate = tick.TimestampUtc,
                LastPrice = tick.Last.HasValue ? (double)tick.Last.Value : null,
                Volume = tick.Volume
            };
        }

        return result;
    }

    public static bool TryGetTick(ConnectorLiveSnapshot live, string ticker, out NormalizedMarketTick tick) =>
        live.Ticks.TryGetValue(ticker, out tick!);
}
