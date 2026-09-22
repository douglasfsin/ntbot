namespace NtBot.Domain.Entities.Portfolio;

/// <summary>
/// Snapshot MtM da carteira em uma data (base para TWR oficial).
/// Capturado a partir das holdings atuais ou importado.
/// </summary>
public class PortfolioValuationSnapshot
{
    public Guid Id { get; set; }
    public Guid GoalPortfolioId { get; set; }
    public GoalPortfolio? GoalPortfolio { get; set; }

    /// <summary>Data do valuation (UTC date normalized).</summary>
    public DateTime SnapshotDate { get; set; }

    public decimal TotalMarketValue { get; set; }
    public decimal CostBasis { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
