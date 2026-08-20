namespace NtBot.Domain.Entities.Quant;

public class QuantOpeningRange
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string RangeWindow { get; set; } = "5m";

    public DateTime RangeStart { get; set; }
    public DateTime RangeEnd { get; set; }

    public decimal? OpeningPrice { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal RangePoints { get; set; }
    public decimal? RangePercent { get; set; }

    public long Volume { get; set; }
    public decimal? Delta { get; set; }
    public long? BuyVolume { get; set; }
    public long? SellVolume { get; set; }
    public decimal? Vwap { get; set; }

    public bool BreakoutUp { get; set; }
    public bool BreakoutDown { get; set; }
    public DateTime? BreakoutTime { get; set; }
    public bool OpeningDrive { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
