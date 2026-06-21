namespace NtBot.Web.Models;

public class MarketSnapshotModel
{
    public DateTime Timestamp { get; set; }
    public string Provider { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public long Volume { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal PreviousClose { get; set; }
    public string MarketStatus { get; set; } = "";
}

public class MarketOverviewModel
{
    public DateTime Timestamp { get; set; }
    public string Provider { get; set; } = "";
    public List<MarketSnapshotModel> Commodities { get; set; } = [];
    public List<MarketSnapshotModel> Indexes { get; set; } = [];
    public List<MarketSnapshotModel> Currencies { get; set; } = [];
    public List<MarketSnapshotModel> Treasury { get; set; } = [];
    public List<MarketSnapshotModel> Sectors { get; set; } = [];
    public string MarketRegime { get; set; } = "Neutral";
    public MarketSnapshotModel? Vix { get; set; }
}

public class ImpactFactorModel
{
    public string Symbol { get; set; } = "";
    public string Label { get; set; } = "";
    public double Correlation { get; set; }
    public double Weight { get; set; }
}

public class AssetImpactModel
{
    public string Asset { get; set; } = "";
    public List<ImpactFactorModel> Factors { get; set; } = [];
    public string Recommendation { get; set; } = "";
    public double ImpactScore { get; set; }
    public double? BasketWeightPercent { get; set; }
}

public class CorrelationResultModel
{
    public DateTime Timestamp { get; set; }
    public List<AssetImpactModel> AssetImpacts { get; set; } = [];
}

public class QuantScoreModel
{
    public int Score { get; set; }
    public string Label { get; set; } = "";
    public Dictionary<string, decimal> ComponentScores { get; set; } = [];
    public Dictionary<string, decimal> Weights { get; set; } = [];
}

public class MarketProviderStatusModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public int RefreshIntervalSeconds { get; set; }
    public string HealthStatus { get; set; } = "";
    public DateTime? LastSync { get; set; }
    public List<string> Capabilities { get; set; } = [];
}
