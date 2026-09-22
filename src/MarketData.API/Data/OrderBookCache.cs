using MarketData.API.Data.Model;
using System.Collections.Concurrent;

namespace MarketData.API.Data
{
    public static class OrderBookCache
    {
        private static readonly ConcurrentDictionary<string, OrderBook> _books = new();

        public static OrderBook GetOrCreate(string ticker)
        {
            return _books.GetOrAdd(ticker, _ => new OrderBook());
        }
    }
}
