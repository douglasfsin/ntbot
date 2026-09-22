namespace NtBot.Domain.Entities.Portfolio;

/// <summary>
/// Posição / holding. v1: Manual + CSV.
/// Open Finance via IOpenFinanceProvider (stub) em fases posteriores.
/// </summary>
public class PortfolioPosition
{
    public Guid Id { get; set; }
    public Guid GoalPortfolioId { get; set; }
    public GoalPortfolio? GoalPortfolio { get; set; }
    public Guid? ProductId { get; set; }
    public ProductCatalogItem? Product { get; set; }

    public string Symbol { get; set; } = string.Empty;
    public string? CustodianLabel { get; set; }
    public decimal Quantity { get; set; }
    public decimal AvgPrice { get; set; }
    public decimal? WeightPct { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? MtmValue { get; set; }

    public PositionSource Source { get; set; } = PositionSource.Manual;
    public SuitabilityStatus SuitabilityStatus { get; set; } = SuitabilityStatus.Unknown;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum PositionSource
{
    Manual,
    CsvImport,
    OpenFinance,
    Proposal
}

public enum SuitabilityStatus
{
    Aligned,
    Misaligned,
    Unknown
}
