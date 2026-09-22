namespace NtBot.Domain.Entities.Portfolio;

/// <summary>Hierarquia classe / subclasse (CVM 175 simplificada).</summary>
public class AssetClassNode
{
    public Guid Id { get; set; }

    /// <summary>Null = catálogo plataforma; preenchido = override por tenant.</summary>
    public Guid? TenantId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public AssetClassNode? Parent { get; set; }
    public int SortOrder { get; set; }
}
