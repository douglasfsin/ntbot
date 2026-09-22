namespace Marketdata.Database.Configuration;

public class QuestDbOptions
{
    public const string SectionName = "QuestDB";
    
    public string Host { get; set; } = "localhost";
    public int IlpPort { get; set; } = 9009;
    public bool Enabled { get; set; } = true;
}
