namespace NtBot.Domain.Entities.Portfolio;

/// <summary>Carteira por objetivo (mental accounting).</summary>
public class GoalPortfolio
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AdvisorUserId { get; set; }
    public Guid ClientUserId { get; set; }

    public string ObjectiveName { get; set; } = string.Empty;
    public PortfolioGoalType GoalType { get; set; } = PortfolioGoalType.Custom;
    public DateTime? TargetDate { get; set; }
    public string BaseCurrency { get; set; } = "BRL";
    public PortfolioStatus Status { get; set; } = PortfolioStatus.Active;

    /// <summary>Prompt AI configurável por carteira (Fase 3).</summary>
    public string? AnalysisPrompt { get; set; }

    /// <summary>Última análise AI persistida.</summary>
    public string? LastAnalysisSummary { get; set; }
    public string? LastAnalysisNextBestAction { get; set; }
    public DateTime? LastAnalysisAt { get; set; }
    public bool LastAnalysisIsStub { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<PortfolioPosition> Positions { get; set; } = new();
    public List<PortfolioRecommendation> Recommendations { get; set; } = new();
    public List<PortfolioValuationSnapshot> ValuationSnapshots { get; set; } = new();
    public List<PortfolioCashflow> Cashflows { get; set; } = new();
}

public enum PortfolioGoalType
{
    Retirement,
    Emergency,
    Growth,
    Education,
    Offshore,
    Custom
}

public enum PortfolioStatus
{
    Draft,
    Active,
    Archived
}
