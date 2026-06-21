namespace NtBot.Api.Dtos;

public sealed class CandleIngestBatch
{
    public string Timeframe { get; set; } = "M5";
    public List<CandleIngestItem> Candles { get; set; } = [];
}

public sealed class CandleIngestItem
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime OpenTime { get; set; }
    public DateTime CloseTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}
