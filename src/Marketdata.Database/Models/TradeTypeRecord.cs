namespace Marketdata.Database.Models;

public class TradeTypeRecord
{
    public int TypeId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}
