using NtBot.Shared.WhiteLabel;

namespace NtBot.UnitTests.WhiteLabel;

public class TenantPublicSlugTests
{
    [Theory]
    [InlineData("nomos")]
    [InlineData("Nomos")]
    [InlineData("/nomos/")]
    public void Nomos_IsValidPublicSlug(string raw)
    {
        Assert.True(TenantPublicSlug.IsValid(raw));
        Assert.Equal("nomos", TenantPublicSlug.Normalize(raw));
        Assert.False(TenantPublicSlug.IsReserved(raw));
    }

    [Theory]
    [InlineData("app")]
    [InlineData("login")]
    [InlineData("register")]
    [InlineData("api")]
    [InlineData("pricing")]
    public void AppRoutes_AreReserved(string slug)
    {
        Assert.True(TenantPublicSlug.IsReserved(slug));
        Assert.False(TenantPublicSlug.IsValid(slug));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bad Slug")]
    [InlineData("-nomos")]
    [InlineData("nomos_x")]
    public void InvalidShapes_Rejected(string slug)
    {
        Assert.False(TenantPublicSlug.IsValid(slug));
    }

    [Theory]
    [InlineData("/nomos", "nomos")]
    [InlineData("/nomos/login", "nomos")]
    [InlineData("/t/nomos/login", "nomos")]
    [InlineData("/Nomos/", "nomos")]
    public void BrandedPaths_ExtractSlug(string path, string expected)
    {
        Assert.True(TenantPublicSlug.TryGetBrandedSlugFromPath(path, out var slug));
        Assert.Equal(expected, slug);
        Assert.Equal($"/{expected}/login", TenantPublicSlug.BrandedLoginPath(slug));
    }

    [Theory]
    [InlineData("/app")]
    [InlineData("/login")]
    [InlineData("/register")]
    [InlineData("/t")]
    [InlineData("/api")]
    [InlineData("/health")]
    [InlineData("/pricing")]
    [InlineData("/forgot-password")]
    [InlineData("/reset-password")]
    [InlineData("/app/settings")]
    public void ReservedOrAppPaths_AreNotBrandedSlugs(string path)
    {
        Assert.False(TenantPublicSlug.TryGetBrandedSlugFromPath(path, out _));
    }
}

public class InfluencerClientScopeTests
{
    private static readonly Guid Douglas = Guid.Parse("0a0a0a0a-10e0-4000-8000-000000000012");
    private static readonly Guid Arthur = Guid.Parse("0a0a0a0a-10e0-4000-8000-000000000013");

    [Fact]
    public void Advisor_SeesOnlyOwnSubscribers()
    {
        Assert.True(InfluencerClientScope.CanListClient("ADVISOR", Douglas, Douglas));
        Assert.False(InfluencerClientScope.CanListClient("ADVISOR", Douglas, Arthur));
        Assert.False(InfluencerClientScope.CanListClient("ADVISOR", Douglas, null));
    }

    [Fact]
    public void Admin_SeesEverySubscriber()
    {
        Assert.True(InfluencerClientScope.CanListClient("ADMIN", Guid.NewGuid(), Douglas));
        Assert.True(InfluencerClientScope.CanListClient("ADMIN", Guid.NewGuid(), null));
    }
}
