using Microsoft.Extensions.Logging;
using NtBot.Connector.Dtos;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Services;

public interface IConnectorEventPublisher
{
    Task PublishBatchAsync(Guid tenantId, NormalizedIngestBatch batch, string? clientIp = null, CancellationToken ct = default);
}
public interface IConnectorIngestService
{
    Task<ConnectorAuthResult> IngestAsync(string apiKey, NormalizedIngestBatch batch, string? ip, CancellationToken ct = default);
}

public class ConnectorIngestService : IConnectorIngestService
{
    private readonly IConnectorService _connector;
    private readonly IConnectorEventPublisher _publisher;
    private readonly ILogger<ConnectorIngestService> _logger;

    public ConnectorIngestService(
        IConnectorService connector,
        IConnectorEventPublisher publisher,
        ILogger<ConnectorIngestService> logger)
    {
        _connector = connector;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<ConnectorAuthResult> IngestAsync(string apiKey, NormalizedIngestBatch batch, string? ip, CancellationToken ct = default)
    {
        var auth = await _connector.ValidateApiKeyAsync(apiKey, ip, ct);
        if (!auth.Success || !auth.LicenseActive)
            return auth;

        if (Guid.TryParse(batch.SessionId, out var sessionId))
            await _connector.HeartbeatAsync(sessionId, ip, ct);

        await _publisher.PublishBatchAsync(auth.TenantId, batch, ip, ct);

        return auth;
    }
}
