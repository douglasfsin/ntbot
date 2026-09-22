namespace NtBot.Domain.Entities.Portfolio;

/// <summary>
/// Oferta / recomendação assessor → cliente.
/// Aceite = intenção apenas (sem execução em corretora) — decisão v1.
/// </summary>
public class PortfolioRecommendation
{
    public Guid Id { get; set; }
    public Guid GoalPortfolioId { get; set; }
    public GoalPortfolio? GoalPortfolio { get; set; }
    public Guid ProductId { get; set; }
    public ProductCatalogItem? Product { get; set; }
    public Guid AdvisorUserId { get; set; }

    public decimal? EstimatedSharpeImpact { get; set; }
    public string? SimulatedMetricsJson { get; set; }
    public string? Notes { get; set; }

    public OfferStatus Status { get; set; } = OfferStatus.Pending;
    public DateTime? ClientRespondedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}

public enum OfferStatus
{
    Pending,
    Accepted,
    Rejected,
    Expired
}
