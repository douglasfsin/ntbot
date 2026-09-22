using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities.Portfolio;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Api.Services.Portfolio;

public interface IPortfolioValuationService
{
    Task<PortfolioValuationSnapshotDto> CaptureAsync(Guid tenantId, Guid portfolioId, string? notes, CancellationToken ct = default);
    Task<List<PortfolioValuationSnapshotDto>> ListSnapshotsAsync(Guid tenantId, Guid portfolioId, CancellationToken ct = default);
    Task<PortfolioCashflowDto> AddCashflowAsync(Guid tenantId, Guid portfolioId, AddCashflowRequest req, CancellationToken ct = default);
    Task<List<PortfolioCashflowDto>> ListCashflowsAsync(Guid tenantId, Guid portfolioId, CancellationToken ct = default);
}

public sealed class PortfolioValuationService : IPortfolioValuationService
{
    private readonly NtBotDbContext _db;

    public PortfolioValuationService(NtBotDbContext db) => _db = db;

    public async Task<PortfolioValuationSnapshotDto> CaptureAsync(
        Guid tenantId, Guid portfolioId, string? notes, CancellationToken ct = default)
    {
        var portfolio = await _db.GoalPortfolios
            .Include(p => p.Positions)
            .FirstOrDefaultAsync(p => p.Id == portfolioId && p.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Carteira não encontrada.");

        var cost = portfolio.Positions.Sum(p => p.Quantity * p.AvgPrice);
        var mtm = portfolio.Positions.Sum(p => p.MtmValue ?? (p.Quantity * p.AvgPrice));
        var day = DateTime.UtcNow.Date;

        var existing = await _db.PortfolioValuationSnapshots
            .FirstOrDefaultAsync(s => s.GoalPortfolioId == portfolioId && s.SnapshotDate == day, ct);

        if (existing is not null)
        {
            existing.TotalMarketValue = mtm;
            existing.CostBasis = cost;
            existing.Notes = notes ?? existing.Notes ?? "Recapturado das holdings.";
            await _db.SaveChangesAsync(ct);
            return MapSnap(existing);
        }

        var snap = new PortfolioValuationSnapshot
        {
            Id = Guid.NewGuid(),
            GoalPortfolioId = portfolioId,
            SnapshotDate = day,
            TotalMarketValue = mtm,
            CostBasis = cost,
            Notes = notes ?? "Capturado das holdings atuais.",
            CreatedAt = DateTime.UtcNow
        };
        _db.PortfolioValuationSnapshots.Add(snap);
        await _db.SaveChangesAsync(ct);
        return MapSnap(snap);
    }

    public async Task<List<PortfolioValuationSnapshotDto>> ListSnapshotsAsync(
        Guid tenantId, Guid portfolioId, CancellationToken ct = default)
    {
        var ok = await _db.GoalPortfolios.AsNoTracking()
            .AnyAsync(p => p.Id == portfolioId && p.TenantId == tenantId, ct);
        if (!ok) return [];

        return await _db.PortfolioValuationSnapshots.AsNoTracking()
            .Where(s => s.GoalPortfolioId == portfolioId)
            .OrderByDescending(s => s.SnapshotDate)
            .Take(120)
            .Select(s => new PortfolioValuationSnapshotDto
            {
                Id = s.Id,
                GoalPortfolioId = s.GoalPortfolioId,
                SnapshotDate = s.SnapshotDate,
                TotalMarketValue = s.TotalMarketValue,
                CostBasis = s.CostBasis,
                Notes = s.Notes,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<PortfolioCashflowDto> AddCashflowAsync(
        Guid tenantId, Guid portfolioId, AddCashflowRequest req, CancellationToken ct = default)
    {
        var ok = await _db.GoalPortfolios.AsNoTracking()
            .AnyAsync(p => p.Id == portfolioId && p.TenantId == tenantId, ct);
        if (!ok) throw new InvalidOperationException("Carteira não encontrada.");

        var type = string.Equals(req.Type, "Withdrawal", StringComparison.OrdinalIgnoreCase)
            ? PortfolioCashflowType.Withdrawal
            : PortfolioCashflowType.Deposit;

        var abs = Math.Abs(req.Amount);
        if (abs <= 0) throw new InvalidOperationException("Valor do fluxo deve ser > 0.");

        var signed = type == PortfolioCashflowType.Deposit ? abs : -abs;
        var date = (req.CashflowDate ?? DateTime.UtcNow).ToUniversalTime().Date;

        var cf = new PortfolioCashflow
        {
            Id = Guid.NewGuid(),
            GoalPortfolioId = portfolioId,
            CashflowDate = date,
            Amount = signed,
            Type = type,
            Notes = req.Notes,
            CreatedAt = DateTime.UtcNow
        };
        _db.PortfolioCashflows.Add(cf);
        await _db.SaveChangesAsync(ct);
        return MapCf(cf);
    }

    public async Task<List<PortfolioCashflowDto>> ListCashflowsAsync(
        Guid tenantId, Guid portfolioId, CancellationToken ct = default)
    {
        var ok = await _db.GoalPortfolios.AsNoTracking()
            .AnyAsync(p => p.Id == portfolioId && p.TenantId == tenantId, ct);
        if (!ok) return [];

        return await _db.PortfolioCashflows.AsNoTracking()
            .Where(c => c.GoalPortfolioId == portfolioId)
            .OrderByDescending(c => c.CashflowDate)
            .Take(200)
            .Select(c => new PortfolioCashflowDto
            {
                Id = c.Id,
                GoalPortfolioId = c.GoalPortfolioId,
                CashflowDate = c.CashflowDate,
                Amount = c.Amount,
                Type = c.Type.ToString(),
                Notes = c.Notes,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(ct);
    }

    private static PortfolioValuationSnapshotDto MapSnap(PortfolioValuationSnapshot s) => new()
    {
        Id = s.Id,
        GoalPortfolioId = s.GoalPortfolioId,
        SnapshotDate = s.SnapshotDate,
        TotalMarketValue = s.TotalMarketValue,
        CostBasis = s.CostBasis,
        Notes = s.Notes,
        CreatedAt = s.CreatedAt
    };

    private static PortfolioCashflowDto MapCf(PortfolioCashflow c) => new()
    {
        Id = c.Id,
        GoalPortfolioId = c.GoalPortfolioId,
        CashflowDate = c.CashflowDate,
        Amount = c.Amount,
        Type = c.Type.ToString(),
        Notes = c.Notes,
        CreatedAt = c.CreatedAt
    };
}

public sealed class PortfolioValuationSnapshotDto
{
    public Guid Id { get; set; }
    public Guid GoalPortfolioId { get; set; }
    public DateTime SnapshotDate { get; set; }
    public decimal TotalMarketValue { get; set; }
    public decimal CostBasis { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class PortfolioCashflowDto
{
    public Guid Id { get; set; }
    public Guid GoalPortfolioId { get; set; }
    public DateTime CashflowDate { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = "";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AddCashflowRequest
{
    public decimal Amount { get; set; }
    public string? Type { get; set; }
    public DateTime? CashflowDate { get; set; }
    public string? Notes { get; set; }
}
