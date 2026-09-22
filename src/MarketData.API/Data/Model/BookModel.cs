namespace MarketData.API.Data.Model
{
    public class OrderBookLevel
    {
        public double Price { get; set; }
        public int Quantity { get; set; }
        public long OfferId { get; set; }
    }

    public class OrderBook
    {
        public SortedDictionary<int, OrderBookLevel> Bids { set; get; } = new();
        public SortedDictionary<int, OrderBookLevel> Asks { set; get; } = new();
    }
}
