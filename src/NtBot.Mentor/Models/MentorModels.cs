namespace NtBot.Mentor.Models;

public enum RecommendationCategory
{
    Operational,
    Risk,
    Discipline,
    Timing,
    Asset,
    Session
}

public sealed class PersonalRecommendation
{
    public RecommendationCategory Category { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public int Confidence { get; init; }
    public IReadOnlyList<string> Evidence { get; init; } = [];
}

public sealed class PerformanceScoreBreakdown
{
    public int Discipline { get; init; }
    public int RiskManagement { get; init; }
    public int Consistency { get; init; }
    public int Payoff { get; init; }
    public int Expectancy { get; init; }
    public int DrawdownControl { get; init; }
    public int Total { get; init; }
    public string Classification { get; init; } = string.Empty;
}

public sealed class TimeSlotStat
{
    public int Hour { get; init; }
    public int Trades { get; init; }
    public int Wins { get; init; }
    public decimal WinRate { get; init; }
    public decimal AvgPnL { get; init; }
    public decimal Payoff { get; init; }
}

public sealed class AssetStat
{
    public string Symbol { get; init; } = string.Empty;
    public int Trades { get; init; }
    public decimal WinRate { get; init; }
    public decimal NetPnL { get; init; }
    public decimal Payoff { get; init; }
}

public sealed class MentorSnapshot
{
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public int TradeCount { get; init; }
    public int SessionDays { get; init; }
    public int HistoryDays { get; init; }
    public bool HasSufficientData { get; init; }
    public PerformanceScoreBreakdown Score { get; init; } = new();
    public IReadOnlyList<PersonalRecommendation> Recommendations { get; init; } = [];
    public IReadOnlyList<TimeSlotStat> TimeSlots { get; init; } = [];
    public IReadOnlyList<AssetStat> Assets { get; init; } = [];
    public string? DailyPlanSummary { get; init; }
}

public sealed class AnalyzedTrade
{
    public string Symbol { get; init; } = string.Empty;
    public DateTime EntryTime { get; init; }
    public DateTime? ExitTime { get; init; }
    public decimal NetPnL { get; init; }
    public bool IsWin { get; init; }
    public int EntryHourLocal { get; init; }
    public DayOfWeek DayOfWeek { get; init; }
    public string? ExitReason { get; init; }
}
