using MediatR;
using NtBot.MarketDrivers.Services;
using NtBot.TradingIntelligence.Cache;
using NtBot.TradingIntelligence.Models;
using NtBot.TradingIntelligence.Services;

namespace NtBot.TradingIntelligence.Commands;

public sealed record RefreshTradingIntelligenceCommand(
    string? Asset = null,
    Guid? TenantId = null,
    bool NotifyClients = true) : IRequest<RefreshTradingIntelligenceResult>;

public sealed record RefreshTradingIntelligenceResult(
    int Refreshed,
    IReadOnlyList<TradingIntelligenceSnapshot> Snapshots);

public sealed class RefreshTradingIntelligenceHandler
    : IRequestHandler<RefreshTradingIntelligenceCommand, RefreshTradingIntelligenceResult>
{
    private readonly ITradingIntelligenceService _service;
    private readonly ITradingIntelligenceUpdateNotifier? _notifier;

    public RefreshTradingIntelligenceHandler(
        ITradingIntelligenceService service,
        ITradingIntelligenceUpdateNotifier? notifier = null)
    {
        _service = service;
        _notifier = notifier;
    }

    public async Task<RefreshTradingIntelligenceResult> Handle(
        RefreshTradingIntelligenceCommand request,
        CancellationToken cancellationToken)
    {
        var snapshots = new List<TradingIntelligenceSnapshot>();

        if (!string.IsNullOrWhiteSpace(request.Asset))
        {
            var snapshot = await _service.RefreshSnapshotAsync(
                request.Asset, request.TenantId, cancellationToken);
            if (snapshot is not null)
            {
                snapshots.Add(snapshot);
                if (request.NotifyClients && _notifier is not null)
                    await _notifier.NotifySnapshotUpdatedAsync(snapshot, cancellationToken);
            }
        }
        else
        {
            var refreshed = await _service.RefreshAllAsync(
                request.TenantId, request.NotifyClients, cancellationToken);
            snapshots.AddRange(refreshed);
        }

        return new RefreshTradingIntelligenceResult(snapshots.Count, snapshots);
    }
}

public sealed record InvalidateTradingIntelligenceCacheCommand(
    string TargetAsset,
    Guid? TenantId = null) : IRequest<Unit>;

public sealed class InvalidateTradingIntelligenceCacheHandler
    : IRequestHandler<InvalidateTradingIntelligenceCacheCommand, Unit>
{
    private readonly ITradingIntelligenceCacheService _cache;
    private readonly IMarketDriversService _drivers;

    public InvalidateTradingIntelligenceCacheHandler(
        ITradingIntelligenceCacheService cache,
        IMarketDriversService drivers)
    {
        _cache = cache;
        _drivers = drivers;
    }

    public async Task<Unit> Handle(
        InvalidateTradingIntelligenceCacheCommand request,
        CancellationToken cancellationToken)
    {
        var normalized = Macro.Configuration.MacroSymbolAliases.Normalize(request.TargetAsset);
        await _cache.RemoveSnapshotAsync(normalized, request.TenantId, cancellationToken);
        await _drivers.ForceRefreshAsync(cancellationToken);
        return Unit.Value;
    }
}
