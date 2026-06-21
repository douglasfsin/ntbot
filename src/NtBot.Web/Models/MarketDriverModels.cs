namespace NtBot.Web.Models;

public class MarketDriverModel
{
    public string Symbol { get; set; } = "";
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal? CurrentValue { get; set; }
    public decimal? PreviousValue { get; set; }
    public decimal Variation { get; set; }
    public string Impact { get; set; } = "";
    public decimal Weight { get; set; }
    public string Direction { get; set; } = "";
    public string Description { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public DateTime LastUpdate { get; set; }
    public decimal Confidence { get; set; }
}

public class DriverScoreModel
{
    public int Score { get; set; }
    public string Label { get; set; } = "";
    public string Classification { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public decimal Confidence { get; set; }
    public decimal? QuantProbability { get; set; }
    public Dictionary<string, decimal> ComponentScores { get; set; } = [];
}

public class MarketDriverHeatCellModel
{
    public string Group { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Label { get; set; } = "";
    public int Score { get; set; }
    public string Impact { get; set; } = "";
    public string Tooltip { get; set; } = "";
    public decimal Variation { get; set; }
}

public class MarketDriversAISummaryModel
{
    public List<string> PositiveFactors { get; set; } = [];
    public List<string> NegativeFactors { get; set; } = [];
    public List<string> RecentChanges { get; set; } = [];
    public List<string> RelevantEvents { get; set; } = [];
    public string ExpectedImpact { get; set; } = "";
}

public class MarketDriversSnapshotModel
{
    public string Asset { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public List<MarketDriverModel> Drivers { get; set; } = [];
    public DriverScoreModel Score { get; set; } = new();
    public string Explanation { get; set; } = "";
    public List<MarketDriverHeatCellModel> HeatMap { get; set; } = [];
    public MarketDriversAISummaryModel? AiSummary { get; set; }
}

public class MarketDriversDashboardItemModel
{
    public string Asset { get; set; } = "";
    public DriverScoreModel Score { get; set; } = new();
    public List<MarketDriverModel> TopDrivers { get; set; } = [];
    public string ExplanationPreview { get; set; } = "";
}
