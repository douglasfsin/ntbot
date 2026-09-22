using System.Threading.Channels;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;

namespace NtBot.Connector.Windows.MarketData;

public sealed class MarketDataBus : IMarketDataBus, IMarketDataPublisher
{
    private readonly Channel<MarketTick> _channel;
    private long _published;
    private long _dropped;

    public MarketDataBus(IOptions<MarketDataGatewayOptions> options)
    {
        var capacity = Math.Max(256, options.Value.ChannelCapacity);
        _channel = Channel.CreateBounded<MarketTick>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ChannelReader<MarketTick> Reader => _channel.Reader;

    public int QueuedCount => _channel.Reader.Count;

    public long PublishedCount => Interlocked.Read(ref _published);

    public long DroppedCount => Interlocked.Read(ref _dropped);

    public ValueTask PublishAsync(MarketTick tick, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _published);

        if (_channel.Writer.TryWrite(tick))
            return ValueTask.CompletedTask;

        Interlocked.Increment(ref _dropped);
        return ValueTask.CompletedTask;
    }
}
