using ProfitDLLClient;
using System.Threading.Channels;

using Marketdata.Database.Models;

namespace MarketData.API.Worker;

/// <summary>
/// Canal singleton compartilhado entre MarketDataWorker (producer)
/// e consumidores (PersistenceWorker).
/// </summary>
public static class ChannelProvider
{
    public static readonly Channel<TradeRecord> Channel =
        System.Threading.Channels.Channel.CreateBounded<TradeRecord>(
            new BoundedChannelOptions(100_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleWriter = true,
                SingleReader = true
            });
}

/// <summary>
/// Constante NL_OK para uso no MarketData.API.
/// </summary>
internal static class ProfitConstants
{
    public const int NL_OK = (int)NResult.NL_OK;
}