using NtBot.Domain.Entities;

namespace NtBot.Domain.WhiteLabel;

/// <summary>
/// ADVISOR influencers only see CLIENT subscribers they sponsor.
/// ADMIN sees every CLIENT in the tenant.
/// </summary>
public static class ClientSponsorFilter
{
    public static bool RestrictToSponsor(string? role)
        => string.Equals(role, nameof(UserRole.ADVISOR), StringComparison.OrdinalIgnoreCase);

    public static IQueryable<User> Apply(
        IQueryable<User> users,
        Guid tenantId,
        string? role,
        Guid callerUserId)
    {
        var q = users.Where(u =>
            u.TenantId == tenantId
            && u.Role == UserRole.CLIENT
            && u.IsActive);

        if (RestrictToSponsor(role))
            q = q.Where(u => u.SponsorUserId == callerUserId);

        return q;
    }
}
