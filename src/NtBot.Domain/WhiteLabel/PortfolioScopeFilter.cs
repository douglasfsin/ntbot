using NtBot.Domain.Entities;
using NtBot.Domain.Entities.Portfolio;

namespace NtBot.Domain.WhiteLabel;

/// <summary>
/// Role-scoped portfolio visibility. ADVISOR sees own + sponsored-client portfolios;
/// CLIENT sees only theirs; ADMIN sees the tenant (optionally filtered by clientUserId).
/// </summary>
public static class PortfolioScopeFilter
{
    public static IQueryable<GoalPortfolio> Apply(
        IQueryable<GoalPortfolio> portfolios,
        IQueryable<User> users,
        Guid tenantId,
        string? role,
        Guid callerUserId,
        Guid? clientUserId)
    {
        var q = portfolios.Where(p => p.TenantId == tenantId);

        if (string.Equals(role, nameof(UserRole.CLIENT), StringComparison.OrdinalIgnoreCase))
            return q.Where(p => p.ClientUserId == callerUserId);

        if (clientUserId.HasValue)
            q = q.Where(p => p.ClientUserId == clientUserId.Value);

        if (string.Equals(role, nameof(UserRole.ADVISOR), StringComparison.OrdinalIgnoreCase))
        {
            var sponsoredIds = users
                .Where(u =>
                    u.TenantId == tenantId
                    && u.Role == UserRole.CLIENT
                    && u.IsActive
                    && u.SponsorUserId == callerUserId)
                .Select(u => u.Id);

            q = q.Where(p =>
                p.AdvisorUserId == callerUserId
                || sponsoredIds.Contains(p.ClientUserId));
        }

        return q;
    }
}
