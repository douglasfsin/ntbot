using NtBot.Shared.Hosting;

namespace NtBot.UnitTests.WhiteLabel;

public class CoolifyPublicHostsTests
{
    [Fact]
    public void WebHost_MatchesThinkautomatedPatternLikeBarberAi()
    {
        Assert.Equal("mfdash.thinkautomated.com.br", CoolifyPublicHosts.WebHost);
        Assert.EndsWith(".thinkautomated.com.br", CoolifyPublicHosts.BarberAiHost);
        Assert.Contains(CoolifyPublicHosts.CorsOrigins, o => o == CoolifyPublicHosts.WebUrl);
    }
}
