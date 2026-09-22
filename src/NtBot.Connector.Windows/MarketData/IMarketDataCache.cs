namespace NtBot.Connector.Windows.MarketData;

public interface IMarketDataCache
{
    void Upsert(MarketTick tick);

    bool TryGet(string cacheKey, out MarketTick tick);

    bool Remove(string cacheKey);

    int RemoveWhere(Func<MarketTick, bool> predicate);

    IReadOnlyCollection<MarketTick> Snapshot();

    int Count { get; }
}
