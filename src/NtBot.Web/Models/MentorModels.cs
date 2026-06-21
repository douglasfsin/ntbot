namespace NtBot.Web.Models;

public class MentorSnapshotModel
{
    public DateTime GeneratedAt { get; set; }
    public int TradeCount { get; set; }
    public int SessionDays { get; set; }
    public int HistoryDays { get; set; }
    public bool HasSufficientData { get; set; }
    public PerformanceScoreModel Score { get; set; } = new();
    public List<PersonalRecommendationModel> Recommendations { get; set; } = [];
    public List<TimeSlotStatModel> TimeSlots { get; set; } = [];
    public List<AssetStatModel> Assets { get; set; } = [];
    public string? DailyPlanSummary { get; set; }
}

public class PerformanceScoreModel
{
    public int Discipline { get; set; }
    public int RiskManagement { get; set; }
    public int Consistency { get; set; }
    public int Payoff { get; set; }
    public int Expectancy { get; set; }
    public int DrawdownControl { get; set; }
    public int Total { get; set; }
    public string Classification { get; set; } = "";
}

public class PersonalRecommendationModel
{
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Action { get; set; } = "";
    public int Confidence { get; set; }
    public List<string> Evidence { get; set; } = [];
}

public class TimeSlotStatModel
{
    public int Hour { get; set; }
    public int Trades { get; set; }
    public int Wins { get; set; }
    public decimal WinRate { get; set; }
    public decimal AvgPnL { get; set; }
    public decimal Payoff { get; set; }
}

public class AssetStatModel
{
    public string Symbol { get; set; } = "";
    public int Trades { get; set; }
    public decimal WinRate { get; set; }
    public decimal NetPnL { get; set; }
    public decimal Payoff { get; set; }
}
