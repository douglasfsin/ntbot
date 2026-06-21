namespace NtBot.Domain.Entities;

/// <summary>
/// Configuração de provider do Market Intelligence Framework.
/// </summary>
public class MarketIntelligenceProvider
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int RefreshIntervalSeconds { get; set; } = 60;
    public string Status { get; set; } = "idle";
    public DateTime? LastSync { get; set; }
    public string Capabilities { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
