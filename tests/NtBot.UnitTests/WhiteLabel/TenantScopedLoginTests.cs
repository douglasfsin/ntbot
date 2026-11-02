using NtBot.Domain.Entities;
using NtBot.Domain.WhiteLabel;

namespace NtBot.UnitTests.WhiteLabel;

public class TenantScopedLoginTests
{
    private static readonly Guid NomosId = Guid.Parse("0a0a0a0a-10e0-4000-8000-000000000001");
    private static readonly Guid OtherId = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000001");

    [Fact]
    public void TenantSlug_ResolvesUserInThatTenantOnly()
    {
        var users = new[]
        {
            new User { Id = Guid.NewGuid(), TenantId = NomosId, Email = "admin@somosnomos.com.br" },
            new User { Id = Guid.NewGuid(), TenantId = OtherId, Email = "admin@somosnomos.com.br" }
        }.AsQueryable();
        var brandings = new[]
        {
            new TenantBranding { TenantId = NomosId, PublicSlug = "nomos" },
            new TenantBranding { TenantId = OtherId, PublicSlug = "other" }
        }.AsQueryable();

        var match = TenantScopedLogin.QueryUsers(users, brandings, "Admin@somosnomos.com.br", "nomos").ToList();

        Assert.Single(match);
        Assert.Equal(NomosId, match[0].TenantId);
    }

    [Fact]
    public void MissingSlug_KeepsGlobalEmailLookup()
    {
        var users = new[]
        {
            new User { Id = Guid.NewGuid(), TenantId = NomosId, Email = "douglas@somosnomos.com.br" }
        }.AsQueryable();
        var brandings = Array.Empty<TenantBranding>().AsQueryable();

        var match = TenantScopedLogin.QueryUsers(users, brandings, "douglas@somosnomos.com.br", null).ToList();

        Assert.Single(match);
    }

    [Fact]
    public void UnknownSlug_YieldsNoUser()
    {
        var users = new[]
        {
            new User { Id = Guid.NewGuid(), TenantId = NomosId, Email = "admin@somosnomos.com.br" }
        }.AsQueryable();
        var brandings = new[]
        {
            new TenantBranding { TenantId = NomosId, PublicSlug = "nomos" }
        }.AsQueryable();

        var match = TenantScopedLogin.QueryUsers(users, brandings, "admin@somosnomos.com.br", "missing").ToList();

        Assert.Empty(match);
    }
}

public class ClientSponsorFilterTests
{
    private static readonly Guid TenantId = Guid.Parse("0a0a0a0a-10e0-4000-8000-000000000001");
    private static readonly Guid Douglas = Guid.Parse("0a0a0a0a-10e0-4000-8000-000000000012");
    private static readonly Guid Arthur = Guid.Parse("0a0a0a0a-10e0-4000-8000-000000000013");

    private static IQueryable<User> Clients() => new[]
    {
        new User { Id = Guid.NewGuid(), TenantId = TenantId, Role = UserRole.CLIENT, IsActive = true, SponsorUserId = Douglas, Email = "demo.douglas@somosnomos.com.br" },
        new User { Id = Guid.NewGuid(), TenantId = TenantId, Role = UserRole.CLIENT, IsActive = true, SponsorUserId = Arthur, Email = "demo.arthur@somosnomos.com.br" },
        new User { Id = Guid.NewGuid(), TenantId = TenantId, Role = UserRole.ADVISOR, IsActive = true, Email = "douglas@somosnomos.com.br" },
        new User { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Role = UserRole.CLIENT, IsActive = true, SponsorUserId = Douglas, Email = "other@example.com" }
    }.AsQueryable();

    [Fact]
    public void Advisor_SeesOnlyOwnSponsoredClients()
    {
        var list = ClientSponsorFilter.Apply(Clients(), TenantId, "ADVISOR", Douglas).ToList();

        Assert.Single(list);
        Assert.Equal("demo.douglas@somosnomos.com.br", list[0].Email);
        Assert.Equal(Douglas, list[0].SponsorUserId);
    }

    [Fact]
    public void Admin_SeesEveryClientInTenant()
    {
        var list = ClientSponsorFilter.Apply(Clients(), TenantId, "ADMIN", Guid.NewGuid()).ToList();

        Assert.Equal(2, list.Count);
        Assert.All(list, c => Assert.Equal(UserRole.CLIENT, c.Role));
    }
}
