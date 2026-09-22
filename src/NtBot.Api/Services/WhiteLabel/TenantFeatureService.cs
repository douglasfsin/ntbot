using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Api.Services.WhiteLabel;

/// <summary>
/// Feature flags: Tenant.WhiteLabelEnabled OU plano slug partner OU FeaturesJson.
/// Decisão v1: sem Stripe Connect; Partner = acesso branded + portfolio.module.
/// </summary>
public interface ITenantFeatureService
{
    Task<bool> IsWhiteLabelEnabledAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> IsPortfolioModuleEnabledAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantFeatureSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed record TenantFeatureSnapshot(
    bool WhiteLabelEnabled,
    bool PortfolioModuleEnabled,
    string? PlanSlug,
    int? MaxClients);

public sealed class TenantFeatureService : ITenantFeatureService
{
    private readonly NtBotDbContext _db;

    public TenantFeatureService(NtBotDbContext db) => _db = db;

    public async Task<bool> IsWhiteLabelEnabledAsync(Guid tenantId, CancellationToken ct = default)
        => (await GetSnapshotAsync(tenantId, ct)).WhiteLabelEnabled;

    public async Task<bool> IsPortfolioModuleEnabledAsync(Guid tenantId, CancellationToken ct = default)
        => (await GetSnapshotAsync(tenantId, ct)).PortfolioModuleEnabled;

    public async Task<TenantFeatureSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants
            .AsNoTracking()
            .Include(t => t.Subscription!)
            .ThenInclude(s => s.Plan)
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        if (tenant is null)
            return new TenantFeatureSnapshot(false, false, null, null);

        var plan = tenant.Subscription?.Plan;
        var slug = plan?.Slug?.ToLowerInvariant();
        var (bools, ints) = ParseFeatures(plan?.FeaturesJson);

        var isPartner = slug == "partner" || tenant.Plan == SubscriptionPlan.PARTNER;
        var brandingFlag = Bool(bools, "branding.enabled") || Bool(bools, "branding.custom_login");
        var portfolioFlag = Bool(bools, "portfolio.module");

        var whiteLabel = tenant.WhiteLabelEnabled || isPartner || brandingFlag;
        var portfolio = isPartner || portfolioFlag;
        if (tenant.WhiteLabelEnabled && tenant.Plan == SubscriptionPlan.ENTERPRISE)
            portfolio = true;

        int? maxClients = ints.TryGetValue("portfolio.max_clients", out var mc) ? mc : null;
        return new TenantFeatureSnapshot(whiteLabel, portfolio, slug, maxClients);
    }

    private static bool Bool(Dictionary<string, bool> map, string key)
        => map.TryGetValue(key, out var v) && v;

    private static (Dictionary<string, bool> Bools, Dictionary<string, int> Ints) ParseFeatures(string? json)
    {
        var bools = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var ints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return (bools, ints);
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    bools[prop.Name] = prop.Value.GetBoolean();
                else if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var n))
                    ints[prop.Name] = n;
            }
        }
        catch
        {
            /* ignore malformed FeaturesJson */
        }
        return (bools, ints);
    }
}
