namespace NtBot.Domain.Entities;

/// <summary>
/// Configuração de provider do Macro Intelligence Framework.
/// </summary>
public class MacroProvider
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int Priority { get; set; }
    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public int RefreshIntervalMinutes { get; set; } = 30;
    public string Status { get; set; } = "idle";
    public DateTime? LastSync { get; set; }
    /// <summary>JSON array de capabilities (ex: ["rates","inflation","calendar"]).</summary>
    public string Capabilities { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
