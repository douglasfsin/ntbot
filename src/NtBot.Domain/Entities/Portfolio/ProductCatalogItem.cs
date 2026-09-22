namespace NtBot.Domain.Entities.Portfolio;

/// <summary>Item do catálogo de produtos (FIF / FII / FIDC seed).</summary>
public class ProductCatalogItem
{
    public Guid Id { get; set; }

    /// <summary>Null = catálogo plataforma.</summary>
    public Guid? TenantId { get; set; }

    public string CnpjOrTicker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? AssetClassNodeId { get; set; }
    public AssetClassNode? AssetClassNode { get; set; }

    public ProductFamily ProductFamily { get; set; } = ProductFamily.FIF;
    public bool FlagRetailAllowed { get; set; } = true;
    public string? CreditRating { get; set; }

    /// <summary>Classificação de risco opcional (1=baixo … 5=alto) — CVM 175 / suitability.</summary>
    public int? RiskRating { get; set; }

    public decimal? OffshoreLimitPct { get; set; }
    public string? MetadataJson { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum ProductFamily
{
    FIF,
    FII,
    FIDC,
    FIAGRO,
    FIP,
    Other
}
