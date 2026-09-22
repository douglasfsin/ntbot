namespace MarketData.API.Worker
{
    public class BookSnapshot
    {
        public string Ticker { get; set; } = string.Empty;

        public double BestBid { get; set; }
        public int BestBidQty { get; set; }

        public double BestAsk { get; set; }
        public int BestAskQty { get; set; }

        public int BestBidQty5 { get; set; }
        public int BestAskQty5 { get; set; }

        // Dados de leilão (call auction / preço teórico)
        public bool   IsAuction       { get; set; }
        public double TheoreticalPrice { get; set; }
        public long   TheoreticalQty   { get; set; }

        public int Niveis { get; set; }

        // Soma dinâmica de N/2 níveis (Proteção)
        public double BidQtyN { get; set; }
        public double AskQtyN { get; set; }
        public double BidFullQty { get; set; }
        public double AskFullQty { get; set; }

        // DENSIDADE DO BOOK - COMPRA (BID)
        public int BidLevels10 { get; set; }
        public int BidTicks10 { get; set; }
        public int BidLevels25 { get; set; }
        public int BidTicks25 { get; set; }
        public int BidLevels50 { get; set; }
        public int BidTicks50 { get; set; }
        public int BidLevels100 { get; set; }
        public int BidTicks100 { get; set; }

        // DENSIDADE DO BOOK - VENDA (ASK)
        public int AskLevels10 { get; set; }
        public int AskTicks10 { get; set; }
        public int AskLevels25 { get; set; }
        public int AskTicks25 { get; set; }
        public int AskLevels50 { get; set; }
        public int AskTicks50 { get; set; }
        public int AskLevels100 { get; set; }
        public int AskTicks100 { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
