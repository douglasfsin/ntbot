namespace NtBot.Domain.Entities;

/// <summary>
/// Branding white-label 1:1 com Tenant.
/// Decisão v1: CustomDomain é apenas armazenamento (sem DNS/TLS automático).
/// SSO fora do v1. Stripe Connect fora do v1 (Partner vende só acesso branded).
/// </summary>
public class TenantBranding
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string AppDisplayName { get; set; } = "NTBot";
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? LoginBackgroundUrl { get; set; }

    /// <summary>Cor primária / accent → --nt-accent</summary>
    public string PrimaryColor { get; set; } = "#0ea5e9";

    /// <summary>Cor secundária / gold → --nt-accent-gold</summary>
    public string SecondaryColor { get; set; } = "#f0b90b";

    public string? BgPrimary { get; set; }
    public string? BgSecondary { get; set; }
    public string? TextPrimary { get; set; }

    public string? LoginHeadline { get; set; }
    public string? LoginSubtext { get; set; }
    public string? SupportEmail { get; set; }

    /// <summary>Slug público único: /t/{slug}/login</summary>
    public string PublicSlug { get; set; } = string.Empty;

    /// <summary>v1: campo apenas — sem automação DNS.</summary>
    public string? CustomDomain { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
