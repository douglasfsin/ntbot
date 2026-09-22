namespace NtBot.Domain.Entities.Portfolio;

/// <summary>Perfil de investidor / suitability do cliente (User role CLIENT).</summary>
public class ClientInvestorProfile
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public InvestorRiskProfile RiskProfile { get; set; } = InvestorRiskProfile.Moderate;
    public InvestorSegment Segment { get; set; } = InvestorSegment.Retail;

    public string? SuitabilityAnswersJson { get; set; }
    public DateTime? AssessedAt { get; set; }
    public Guid? AssessedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum InvestorRiskProfile
{
    Conservative,
    Moderate,
    Aggressive
}

public enum InvestorSegment
{
    Retail,
    Qualified,
    Professional,
    HighIncome,
    Private
}
