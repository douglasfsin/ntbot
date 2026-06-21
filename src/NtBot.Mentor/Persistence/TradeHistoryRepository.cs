using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Mentor.Persistence;

public interface ITradeHistoryRepository
{
    Task<IReadOnlyList<Trade>> GetClosedTradesAsync(Guid tenantId, int days, CancellationToken cancellationToken = default);
}

public sealed class TradeHistoryRepository : ITradeHistoryRepository
{
    private readonly NtBotDbContext _db;

    public TradeHistoryRepository(NtBotDbContext db) => _db = db;

    public async Task<IReadOnlyList<Trade>> GetClosedTradesAsync(
        Guid tenantId,
        int days,
        CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Max(1, days));

        return await _db.Trades
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                        && t.Status == TradeStatus.CLOSED
                        && t.ExitTime >= since)
            .OrderBy(t => t.EntryTime)
            .ToListAsync(cancellationToken);
    }
}
