using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Api.Services.WhiteLabel;

public interface ITenantBrandingService
{
    Task<TenantBrandingDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<TenantBrandingDto?> GetForTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantBrandingDto> UpsertAsync(Guid tenantId, UpdateTenantBrandingRequest request, CancellationToken ct = default);
}

public sealed class TenantBrandingService : ITenantBrandingService
{
    private static readonly Regex HexColor = new(@"^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$", RegexOptions.Compiled);
    private readonly NtBotDbContext _db;
    private readonly ITenantFeatureService _features;

    public TenantBrandingService(NtBotDbContext db, ITenantFeatureService features)
    {
        _db = db;
        _features = features;
    }

    public async Task<TenantBrandingDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var entity = await _db.TenantBrandings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.PublicSlug == slug.Trim().ToLowerInvariant(), ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<TenantBrandingDto?> GetForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var entity = await _db.TenantBrandings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId, ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<TenantBrandingDto> UpsertAsync(Guid tenantId, UpdateTenantBrandingRequest request, CancellationToken ct = default)
    {
        if (!await _features.IsWhiteLabelEnabledAsync(tenantId, ct))
            throw new InvalidOperationException("White-label não habilitado para este tenant. Requer plano Partner ou flag WhiteLabelEnabled.");

        ValidateColors(request);

        var slug = (request.PublicSlug ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidOperationException("PublicSlug é obrigatório.");
        if (!Regex.IsMatch(slug, @"^[a-z0-9]([a-z0-9-]{0,78}[a-z0-9])?$"))
            throw new InvalidOperationException("PublicSlug inválido (use letras minúsculas, números e hífen).");

        var clash = await _db.TenantBrandings.AnyAsync(b => b.PublicSlug == slug && b.TenantId != tenantId, ct);
        if (clash)
            throw new InvalidOperationException("PublicSlug já em uso.");

        var entity = await _db.TenantBrandings.FirstOrDefaultAsync(b => b.TenantId == tenantId, ct);
        if (entity is null)
        {
            entity = new TenantBranding { TenantId = tenantId };
            _db.TenantBrandings.Add(entity);
        }

        entity.AppDisplayName = string.IsNullOrWhiteSpace(request.AppDisplayName) ? "NTBot" : request.AppDisplayName.Trim();
        entity.LogoUrl = SanitizeUrl(request.LogoUrl);
        entity.FaviconUrl = SanitizeUrl(request.FaviconUrl);
        entity.LoginBackgroundUrl = SanitizeUrl(request.LoginBackgroundUrl);
        entity.PrimaryColor = request.PrimaryColor?.Trim() ?? "#0ea5e9";
        entity.SecondaryColor = request.SecondaryColor?.Trim() ?? "#f0b90b";
        entity.BgPrimary = SanitizeOptionalColor(request.BgPrimary);
        entity.BgSecondary = SanitizeOptionalColor(request.BgSecondary);
        entity.TextPrimary = SanitizeOptionalColor(request.TextPrimary);
        entity.LoginHeadline = Truncate(request.LoginHeadline, 200);
        entity.LoginSubtext = Truncate(request.LoginSubtext, 400);
        entity.SupportEmail = Truncate(request.SupportEmail, 255);
        entity.PublicSlug = slug;
        entity.CustomDomain = string.IsNullOrWhiteSpace(request.CustomDomain) ? null : request.CustomDomain.Trim().ToLowerInvariant();
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    private static void ValidateColors(UpdateTenantBrandingRequest request)
    {
        foreach (var (name, value) in new[]
                 {
                     ("PrimaryColor", request.PrimaryColor),
                     ("SecondaryColor", request.SecondaryColor),
                     ("BgPrimary", request.BgPrimary),
                     ("BgSecondary", request.BgSecondary),
                     ("TextPrimary", request.TextPrimary)
                 })
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (!HexColor.IsMatch(value.Trim()))
                throw new InvalidOperationException($"{name} deve ser hex (#RGB ou #RRGGBB).");
        }
    }

    private static string? SanitizeOptionalColor(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? SanitizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var t = url.Trim();
        if (t.Length > 512) throw new InvalidOperationException("URL muito longa.");
        // Bloqueia javascript: etc.
        if (t.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("URL não permitida.");
        return t;
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrWhiteSpace(s) ? null : (s.Trim().Length <= max ? s.Trim() : s.Trim()[..max]);

    private static TenantBrandingDto Map(TenantBranding e) => new()
    {
        TenantId = e.TenantId,
        AppDisplayName = e.AppDisplayName,
        LogoUrl = e.LogoUrl,
        FaviconUrl = e.FaviconUrl,
        LoginBackgroundUrl = e.LoginBackgroundUrl,
        PrimaryColor = e.PrimaryColor,
        SecondaryColor = e.SecondaryColor,
        BgPrimary = e.BgPrimary,
        BgSecondary = e.BgSecondary,
        TextPrimary = e.TextPrimary,
        LoginHeadline = e.LoginHeadline,
        LoginSubtext = e.LoginSubtext,
        SupportEmail = e.SupportEmail,
        PublicSlug = e.PublicSlug,
        CustomDomain = e.CustomDomain,
        UpdatedAt = e.UpdatedAt
    };
}

public sealed class TenantBrandingDto
{
    public Guid TenantId { get; set; }
    public string AppDisplayName { get; set; } = "NTBot";
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? LoginBackgroundUrl { get; set; }
    public string PrimaryColor { get; set; } = "#0ea5e9";
    public string SecondaryColor { get; set; } = "#f0b90b";
    public string? BgPrimary { get; set; }
    public string? BgSecondary { get; set; }
    public string? TextPrimary { get; set; }
    public string? LoginHeadline { get; set; }
    public string? LoginSubtext { get; set; }
    public string? SupportEmail { get; set; }
    public string PublicSlug { get; set; } = string.Empty;
    public string? CustomDomain { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class UpdateTenantBrandingRequest
{
    public string? AppDisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? LoginBackgroundUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? BgPrimary { get; set; }
    public string? BgSecondary { get; set; }
    public string? TextPrimary { get; set; }
    public string? LoginHeadline { get; set; }
    public string? LoginSubtext { get; set; }
    public string? SupportEmail { get; set; }
    public string? PublicSlug { get; set; }
    public string? CustomDomain { get; set; }
}
