namespace Marketdata.Database.Models;

public class TradeRecord
{
    public DateTime Timestamp { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public double Price { get; set; }
    public long Quantity { get; set; }
    public string Side { get; set; } = string.Empty;
    public string BuyAgent { get; set; } = string.Empty;
    public string SellAgent { get; set; } = string.Empty;
    public int TradeType { get; set; }
    public long TradeNumber { get; set; }
    public bool IsAuction { get; set; }
}
