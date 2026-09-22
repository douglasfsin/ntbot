using NtBot.Shared.Trading;
using Xunit;

namespace NtBot.UnitTests.Boletagem;

public class MarketDriversDirectionGuardTests
{
    [Theory]
    [InlineData("COMPRA MODERADA", "Sell", true)]
    [InlineData("COMPRA FORTE", "Sell", true)]
    [InlineData("COMPRA FRACA", "venda", true)]
    [InlineData("COMPRA MODERADA", "Buy", false)]
    [InlineData("VENDA MODERADA", "Buy", true)]
    [InlineData("VENDA FORTE", "Sell", false)]
    [InlineData("NEUTRO", "Sell", false)]
    [InlineData("AGUARDAR DADOS", "Buy", false)]
    [InlineData(null, "Sell", false)]
    public void BlockReason_matches_drivers_bias(string? md, string direction, bool blocked)
    {
        var reason = MarketDriversDirectionGuard.BlockReason(md, direction);
        Assert.Equal(blocked, reason is not null);
    }

    [Fact]
    public void AllowedDirection_compra_is_buy()
    {
        Assert.Equal("Buy", MarketDriversDirectionGuard.AllowedDirectionOrNull("COMPRA MODERADA"));
        Assert.Equal("Sell", MarketDriversDirectionGuard.AllowedDirectionOrNull("VENDA FRACA"));
        Assert.Null(MarketDriversDirectionGuard.AllowedDirectionOrNull("NEUTRO"));
    }
}
