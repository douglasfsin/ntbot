using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities;
using NtBot.Identity.Services;
using NtBot.Infrastructure.Persistence;
using NtBot.Shared.Hosting;

namespace NtBot.Api.Services.WhiteLabel;

/// <summary>
/// Seed idempotente do tenant Nomos (somosnomos.com.br): branding verde-lima,
/// admin + influencers Douglas/Arthur (ADVISOR com N CLIENT via SponsorUserId).
/// </summary>
public static class NomosTenantSeeder
{
    public static readonly Guid TenantId = Guid.Parse("0a0a0a0a-10e0-4000-8000-000000000001");
    public static readonly Guid AdminUserId = Guid.Parse("0a0a0a0a-10e0-4000-8000-000000000011");
    public static readonly Guid DouglasUserId = Guid.Parse("0a0a0a0a-10e0-4000-8000-000000000012");
    public static readonly Guid ArthurUserId = Guid.Parse("0a0a0a0a-10e0-4000-8000-000000000013");
    public static readonly Guid DouglasDemoClientId = Guid.Parse("0a0a0a0a-10e0-4000-8000-000000000021");
    public static readonly Guid ArthurDemoClientId = Guid.Parse("0a0a0a0a-10e0-4000-8000-000000000022");

    public const string PublicSlug = "nomos";
    public const string LogoUrl = "https://somosnomos.com.br/wp-content/uploads/2025/06/Logo_VerdeLima_XP-negativa-1.png";
    public const string SiteUrl = "https://www.somosnomos.com.br";
    public const string PrimaryLime = "#C8F542";
    public const string SecondaryLime = "#9BE15D";
    public const string BgHolly = "#021E19";
    public const string BgBlack = "#000000";
    public const string TextWhite = "#FFFFFF";

    public static async Task EnsureAsync(
        NtBotDbContext db,
        IPasswordHasher hasher,
        IConfiguration config,
        ILogger logger,
        CancellationToken ct = default)
    {
        try
        {
            const string fallbackPassword = "Nomos!ChangeMe2026";
            var adminPassword = config["NomosSeed:AdminPassword"]
                                ?? Environment.GetEnvironmentVariable("NOMOS_ADMIN_PASSWORD")
                                ?? fallbackPassword;
            var influencerPassword = config["NomosSeed:InfluencerPassword"]
                                     ?? Environment.GetEnvironmentVariable("NOMOS_INFLUENCER_PASSWORD")
                                     ?? fallbackPassword;

            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == TenantId, ct);
            if (tenant is null)
            {
                tenant = new Tenant
                {
                    Id = TenantId,
                    Name = "Nomos",
                    Email = "admin@somosnomos.com.br",
                    Plan = SubscriptionPlan.PARTNER,
                    IsActive = true,
                    IsTrial = false,
                    WhiteLabelEnabled = true,
                    MaxActivePositions = 50,
                    MaxDailyTrades = 200,
                    CreatedAt = DateTime.UtcNow
                };
                db.Tenants.Add(tenant);
            }
            else
            {
                tenant.WhiteLabelEnabled = true;
                tenant.Plan = SubscriptionPlan.PARTNER;
                tenant.IsActive = true;
                tenant.UpdatedAt = DateTime.UtcNow;
            }

            var branding = await db.TenantBrandings.FirstOrDefaultAsync(b => b.TenantId == TenantId, ct);
            if (branding is null)
            {
                branding = new TenantBranding { TenantId = TenantId };
                db.TenantBrandings.Add(branding);
            }

            branding.AppDisplayName = "Nomos";
            branding.LogoUrl = LogoUrl;
            branding.FaviconUrl = LogoUrl;
            branding.PrimaryColor = PrimaryLime;
            branding.SecondaryColor = SecondaryLime;
            branding.BgPrimary = BgHolly;
            branding.BgSecondary = BgBlack;
            branding.TextPrimary = TextWhite;
            branding.LoginHeadline = "Seu norte financeiro";
            branding.LoginSubtext = "A plataforma dos melhores investidores.";
            branding.SupportEmail = "atendimento@nomos.com.br";
            branding.PublicSlug = PublicSlug;
            branding.CustomDomain = CoolifyPublicHosts.WebHost;
            branding.UpdatedAt = DateTime.UtcNow;

            await EnsureUserAsync(db, hasher, adminPassword, AdminUserId, "admin@somosnomos.com.br", "Admin Nomos", UserRole.ADMIN, null, ct);
            await EnsureUserAsync(db, hasher, influencerPassword, DouglasUserId, "douglas@somosnomos.com.br", "Douglas", UserRole.ADVISOR, null, ct);
            await EnsureUserAsync(db, hasher, influencerPassword, ArthurUserId, "arthur@somosnomos.com.br", "Arthur", UserRole.ADVISOR, null, ct);
            await EnsureUserAsync(db, hasher, influencerPassword, DouglasDemoClientId, "demo.douglas@somosnomos.com.br", "Demo Cliente Douglas", UserRole.CLIENT, DouglasUserId, ct);
            await EnsureUserAsync(db, hasher, influencerPassword, ArthurDemoClientId, "demo.arthur@somosnomos.com.br", "Demo Cliente Arthur", UserRole.CLIENT, ArthurUserId, ct);

            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Nomos tenant ready slug={Slug} site={Site} login={Login}",
                PublicSlug,
                SiteUrl,
                $"{CoolifyPublicHosts.WebUrl}/{PublicSlug}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nomos tenant seed failed");
        }
    }

    private static async Task EnsureUserAsync(
        NtBotDbContext db,
        IPasswordHasher hasher,
        string password,
        Guid id,
        string email,
        string fullName,
        UserRole role,
        Guid? sponsorUserId,
        CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            var existing = await db.Users.FirstOrDefaultAsync(u => u.TenantId == TenantId && u.Email == email, ct);
            if (existing is not null)
            {
                existing.Role = role;
                existing.SponsorUserId = sponsorUserId;
                existing.IsActive = true;
                existing.FullName ??= fullName;
                return;
            }

            db.Users.Add(new User
            {
                Id = id,
                TenantId = TenantId,
                Email = email,
                FullName = fullName,
                PasswordHash = hasher.Hash(password),
                Role = role,
                EmailConfirmed = true,
                IsActive = true,
                SponsorUserId = sponsorUserId,
                CreatedAt = DateTime.UtcNow
            });
            return;
        }

        user.Role = role;
        user.SponsorUserId = sponsorUserId;
        user.IsActive = true;
        user.EmailConfirmed = true;
        if (string.IsNullOrWhiteSpace(user.FullName))
            user.FullName = fullName;
    }
}
