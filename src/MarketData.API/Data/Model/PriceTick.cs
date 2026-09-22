namespace MarketData.API.Data.Model
{
    public class PriceTick
    {
        public string Ticker { get; set; } = string.Empty;
        public double LastPrice { get; set; }

        public double Bid { get; set; }
        public double Ask { get; set; }

        public double BidQty { get; set; }
        public double AskQty { get; set; }

        public double BidQty5 { get; set; }
        public double AskQty5 { get; set; }

        // =========================
        // PROFUNDIDADE
        // =========================
        // Soma de N/2 níveis (configurável via NIVEIS_BOOK)
        public double BidQtyN { get; set; }
        public double AskQtyN { get; set; }
        public double BidFullQty { get; set; }
        public double AskFullQty { get; set; }

        // =========================
        // LEILÃO
        // =========================
        // Dados de leilão (preço teórico / call auction)
        public bool   IsAuction         { get; set; }
        public double TheoreticalPrice   { get; set; }
        public long   TheoreticalQty     { get; set; }

        public int AuctionState { get; set; }



        public DateTime Timestamp { get; set; }
    }
    public class AuctionDto
    {
        public string Ticker { get; set; } = "";

        public bool IsAuction { get; set; }

        public double TheoreticalPrice { get; set; }

        public long TheoreticalQty { get; set; }

        public int State { get; set; }

        public DateTime Timestamp { get; set; }
    }

}
