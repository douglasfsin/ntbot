namespace MarketData.API.Worker.Simulator
{
    public class MockMarketData
    {
        public Dictionary<string, MockTicker> Tickers { get; set; } = new();
    }

    public class MockTicker
    {
        public List<double> Prices { get; set; } = new();
        public List<MockBook> Book { get; set; } = new();
    }

    public class MockBook
    {
        public List<MockLevel> Bids { get; set; } = new();
        public List<MockLevel> Asks { get; set; } = new();
    }

    public class MockLevel
    {
        public double Price { get; set; }
        public int Qty { get; set; }
    }
}
