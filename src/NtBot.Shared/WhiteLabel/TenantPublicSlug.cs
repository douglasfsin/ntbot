using System.Text.RegularExpressions;

namespace NtBot.Shared.WhiteLabel;

/// <summary>
/// Slug público do tenant: <c>/{slug}</c> e <c>/{slug}/login</c>.
/// Literais do Blazor (<c>/app</c>, <c>/login</c>) já ganham do parâmetro;
/// esta lista evita gravar um slug que colida com rotas internas.
/// </summary>
public static class TenantPublicSlug
{
    private static readonly Regex Valid = new(
        @"^[a-z0-9]([a-z0-9-]{0,78}[a-z0-9])?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "app", "login", "register", "t", "api", "health", "swagger", "css", "js",
        "_framework", "_content", "signin", "signout", "forgot-password", "reset-password",
        "pricing", "billing", "error", "not-found", "auth", "hubs", "settings",
        "branding", "advisory", "portfolio", "connector", "quant", "wyckoff", "macro",
        "mentor", "profitchart", "verify-register", "account", "admin", "static",
        "favicon.ico", "robots.txt"
    };

    public static string? Normalize(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var value = slug.Trim().Trim('/').ToLowerInvariant();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static bool IsReserved(string? slug)
    {
        var value = Normalize(slug);
        return value is not null && Reserved.Contains(value);
    }

    public static bool IsValid(string? slug)
    {
        var value = Normalize(slug);
        return value is not null && Valid.IsMatch(value) && !Reserved.Contains(value);
    }

    public const string BrandSlugCookie = "nt_brand_slug";

    /// <summary>
    /// Extracts a candidate branding slug from <c>/{slug}</c>, <c>/{slug}/login</c>
    /// or <c>/t/{slug}/login</c>. Reserved first segments are ignored.
    /// </summary>
    public static bool TryGetBrandedSlugFromPath(string? path, out string slug)
    {
        slug = "";
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var parts = path.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        if (parts[0].Equals("t", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 2)
                return false;
            var nested = Normalize(parts[1]);
            if (nested is null || IsReserved(nested))
                return false;
            slug = nested;
            return true;
        }

        var candidate = Normalize(parts[0]);
        if (candidate is null || IsReserved(candidate))
            return false;

        if (parts.Length == 1
            || (parts.Length == 2 && parts[1].Equals("login", StringComparison.OrdinalIgnoreCase)))
        {
            slug = candidate;
            return true;
        }

        return false;
    }

    public static string BrandedLoginPath(string slug)
        => $"/{Normalize(slug) ?? slug.Trim().Trim('/').ToLowerInvariant()}/login";
}

/// <summary>Influencer (ADVISOR) vê só os CLIENT com SponsorUserId = o próprio id.</summary>
public static class InfluencerClientScope
{
    public static bool CanListClient(string role, Guid actorUserId, Guid? sponsorUserId)
    {
        if (string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(role, "ADVISOR", StringComparison.OrdinalIgnoreCase))
            return sponsorUserId.HasValue && sponsorUserId.Value == actorUserId;
        return false;
    }
}
