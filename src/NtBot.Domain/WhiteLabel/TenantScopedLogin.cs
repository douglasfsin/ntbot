using NtBot.Domain.Entities;

namespace NtBot.Domain.WhiteLabel;

/// <summary>
/// Tenant-scoped login lookup. Extracted so unit tests can exercise the query
/// against in-memory <see cref="IQueryable{T}"/> without a real database.
/// </summary>
public static class TenantScopedLogin
{
    public static string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    public static string? NormalizeSlug(string? slug)
        => string.IsNullOrWhiteSpace(slug) ? null : slug.Trim().ToLowerInvariant();

    /// <summary>
    /// If <paramref name="tenantSlug"/> is set, resolve the tenant via
    /// <see cref="TenantBranding.PublicSlug"/> then match email within that tenant.
    /// Unknown slug yields an empty sequence. Missing slug keeps global email lookup.
    /// </summary>
    public static IQueryable<User> QueryUsers(
        IQueryable<User> users,
        IQueryable<TenantBranding> brandings,
        string email,
        string? tenantSlug)
    {
        email = NormalizeEmail(email);
        var slug = NormalizeSlug(tenantSlug);
        if (slug is null)
            return users.Where(u => u.Email == email);

        return from u in users
               join b in brandings on u.TenantId equals b.TenantId
               where u.Email == email && b.PublicSlug == slug
               select u;
    }
}
