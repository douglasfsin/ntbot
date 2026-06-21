namespace NtBot.Domain.Entities;

/// <summary>
/// Configuração dinâmica de drivers por ativo-alvo (Trading Intelligence).
/// </summary>
public class DriverComposition
{
    public Guid Id { get; set; }
    /// <summary>Null = composição global padrão da plataforma.</summary>
    public Guid? TenantId { get; set; }
    public string TargetAsset { get; set; } = string.Empty;
    public string DriverAsset { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public bool Enabled { get; set; } = true;
    public int DisplayOrder { get; set; }
    public string? Description { get; set; }
    public string Category { get; set; } = "Correlacao";
    public bool Inverse { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
