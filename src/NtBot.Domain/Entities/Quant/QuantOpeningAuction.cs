namespace NtBot.Domain.Entities.Quant;

public class QuantOpeningAuction
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public string Symbol { get; set; } = string.Empty;

    public DateTime AuctionStart { get; set; }
    public DateTime AuctionEnd { get; set; }

    public decimal? PreviousClose { get; set; }
    public decimal? IndicativePrice { get; set; }
    public decimal? EquilibriumPrice { get; set; }
    public decimal? OpeningPrice { get; set; }

    public decimal? GapPoints { get; set; }
    public decimal? GapPercent { get; set; }

    public long Volume { get; set; }
    public long? BuyVolume { get; set; }
    public long? SellVolume { get; set; }
    public decimal? Delta { get; set; }
    public decimal? DeltaRatio { get; set; }

    public long? BidVolume { get; set; }
    public long? AskVolume { get; set; }
    public decimal? BookImbalance { get; set; }
    public decimal? Spread { get; set; }
    public decimal? Vwap { get; set; }

    public int AuctionScore { get; set; }
    public string AuctionClassification { get; set; } = "NEUTRAL";
    public bool IndicativeDataAvailable { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
