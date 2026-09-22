namespace Marketdata.Database.Models;

public class PointOfInterestRecord
{
    public Guid PoiId { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double PriceStart { get; set; }
    public double? PriceEnd { get; set; }
    public bool IsActive { get; set; } = true;
}
