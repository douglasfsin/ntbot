namespace NtBot.Mentor.Configuration;

public sealed class MentorOptions
{
    public const string SectionName = "Mentor";

    public int HistoryDays { get; set; } = 180;

    public int MinTradesForRecommendations { get; set; } = 10;

    public int MinTradesPerBucket { get; set; } = 5;
}
