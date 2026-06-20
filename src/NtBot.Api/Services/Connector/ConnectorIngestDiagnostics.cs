using System.Collections.Concurrent;
using NtBot.Connector.Services;
using NtBot.Shared.Normalized;

namespace NtBot.Api.Services.Connector;

internal static class ConnectorIngestDiagnostics
{
    private static readonly ConcurrentDictionary<Guid, DateTime> LastHeartbeatLogUtc = new();

    public static void LogBatch(
        ILogger logger,
        Guid tenantId,
        NormalizedIngestBatch batch,
        string? ip)
    {
        var tickCount = batch.Ticks?.Count ?? 0;
        var symbols = batch.Ticks?.Select(t => t.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];

        if (tickCount > 0)
        {
            var tickSummary = string.Join(", ",
                batch.Ticks!.Select(t =>
                    $"{t.Symbol} last={t.Last} bid={t.Bid} ask={t.Ask} vol={t.Volume} src={t.Source}"));

            logger.LogInformation(
                "Connector ingest assets tenant={TenantId} delta={IsDelta} session={SessionId} ip={Ip} tickCount={TickCount} symbols=[{Symbols}] data=[{TickSummary}]",
                tenantId,
                batch.IsDelta,
                batch.SessionId,
                ip,
                tickCount,
                string.Join(',', symbols),
                tickSummary);
            return;
        }

        if (!ShouldLogHeartbeat(tenantId))
            return;

        var brokerSummary = batch.BrokerStatuses?.Count > 0
            ? string.Join(", ", batch.BrokerStatuses.Select(b => $"{b.Source}={b.Status}"))
            : "none";

        logger.LogWarning(
            "Connector ingest heartbeat-only tenant={TenantId} delta={IsDelta} session={SessionId} ip={Ip} brokers=[{Brokers}] — nenhum tick de ativo neste batch",
            tenantId,
            batch.IsDelta,
            batch.SessionId,
            ip,
            brokerSummary);
    }

    public static void LogLiveState(ILogger logger, Guid tenantId, ConnectorLiveSnapshot? snapshot)
    {
        if (snapshot == null || snapshot.Ticks.Count == 0)
        {
            logger.LogWarning("Connector live state vazio tenant={TenantId}", tenantId);
            return;
        }

        var summary = string.Join(", ",
            snapshot.Ticks.Select(kv =>
                $"{kv.Key} last={kv.Value.Last} bid={kv.Value.Bid} ask={kv.Value.Ask} ts={kv.Value.TimestampUtc:O}"));

        logger.LogInformation(
            "Connector live cache tenant={TenantId} live={IsLive} total={TotalTicks} ageSec={AgeSec:F1} assets=[{Assets}]",
            tenantId,
            snapshot.IsLive,
            snapshot.TotalTicksReceived,
            snapshot.SecondsSinceLastData,
            summary);
    }

    private static bool ShouldLogHeartbeat(Guid tenantId)
    {
        var now = DateTime.UtcNow;
        if (LastHeartbeatLogUtc.TryGetValue(tenantId, out var last) && now - last < TimeSpan.FromSeconds(15))
            return false;

        LastHeartbeatLogUtc[tenantId] = now;
        return true;
    }
}
