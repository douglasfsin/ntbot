namespace NtBot.Shared.WhiteLabel;

/// <summary>
/// Compatibility façade over <see cref="TenantPublicSlug"/> for branded-path
/// cookies and reserved-slug checks.
/// </summary>
public static class ReservedTenantSlugs
{
    public const string BrandSlugCookie = TenantPublicSlug.BrandSlugCookie;

    public static bool IsReserved(string? slug) => TenantPublicSlug.IsReserved(slug);

    public static bool IsUsableSlug(string? slug) => TenantPublicSlug.IsValid(slug);

    public static bool TryGetBrandedSlugFromPath(string? path, out string slug)
        => TenantPublicSlug.TryGetBrandedSlugFromPath(path, out slug);

    public static string BrandedLoginPath(string slug) => TenantPublicSlug.BrandedLoginPath(slug);
}
