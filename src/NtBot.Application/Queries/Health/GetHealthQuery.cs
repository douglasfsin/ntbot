using MediatR;

namespace NtBot.Application.Queries.Health;

public record GetHealthQuery : IRequest<HealthResponse>;

public record HealthResponse(
    string Status,
    DateTime Timestamp,
    string Version,
    IReadOnlyDictionary<string, string> Services);

public class GetHealthQueryHandler : IRequestHandler<GetHealthQuery, HealthResponse>
{
    public Task<HealthResponse> Handle(GetHealthQuery request, CancellationToken cancellationToken)
    {
        var response = new HealthResponse(
            Status: "healthy",
            Timestamp: DateTime.UtcNow,
            Version: "3.0.0",
            Services: new Dictionary<string, string>
            {
                ["database"] = "connected",
                ["architecture"] = "clean",
                ["realtime"] = "signalr"
            });

        return Task.FromResult(response);
    }
}
