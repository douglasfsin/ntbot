using MediatR;
using NtBot.Application.Queries.Health;

namespace NtBot.UnitTests.Application;

public class GetHealthQueryTests
{
    [Fact]
    public async Task Handle_ReturnsHealthyStatus()
    {
        var handler = new GetHealthQueryHandler();
        var result = await handler.Handle(new GetHealthQuery(), CancellationToken.None);

        Assert.Equal("healthy", result.Status);
        Assert.Equal("3.0.0", result.Version);
        Assert.Contains("architecture", result.Services.Keys);
    }
}
