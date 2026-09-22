using System.Threading.Channels;

namespace NtBot.Connector.Windows.MarketData;

public interface IMarketDataBus
{
    ValueTask PublishAsync(MarketTick tick, CancellationToken ct = default);

    ChannelReader<MarketTick> Reader { get; }

    int QueuedCount { get; }

    long PublishedCount { get; }

    long DroppedCount { get; }
}

public interface IMarketDataPublisher
{
    ValueTask PublishAsync(MarketTick tick, CancellationToken ct = default);
}
