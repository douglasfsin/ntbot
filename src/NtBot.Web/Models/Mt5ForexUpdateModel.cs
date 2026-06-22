namespace NtBot.Web.Models;

public class Mt5ForexUpdateModel
{
    public bool IsLive { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<MarketSnapshotModel> Currencies { get; set; } = [];
}
