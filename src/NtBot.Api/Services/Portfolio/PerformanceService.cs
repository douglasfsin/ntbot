using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Services.Portfolio;
using NtBot.Infrastructure.Persistence;

namespace NtBot.Api.Services.Portfolio;

/// <summary>
/// Performance TWR/MWR com caminho real via snapshots MtM + cashflows;
/// fallback para aproximação por custo das posições quando há &lt; 2 snapshots.
/// </summary>
public interface IPerformanceService
{
    Task<PortfolioPerformanceDto> GetPerformanceAsync(Guid tenantId, Guid portfolioId, CancellationToken ct = default);
}

public sealed class PerformanceService : IPerformanceService
{
    private readonly NtBotDbContext _db;

    public PerformanceService(NtBotDbContext db) => _db = db;

    public async Task<PortfolioPerformanceDto> GetPerformanceAsync(Guid tenantId, Guid portfolioId, CancellationToken ct = default)
    {
        var portfolio = await _db.GoalPortfolios.AsNoTracking()
            .Include(p => p.Positions)
            .FirstOrDefaultAsync(p => p.Id == portfolioId && p.TenantId == tenantId, ct);

        if (portfolio is null)
        {
            return new PortfolioPerformanceDto
            {
                GoalPortfolioId = portfolioId,
                MethodNote = "Carteira não encontrada.",
                IsStub = true,
                Disclaimer = Disclaimer
            };
        }

        var positions = portfolio.Positions;
        var cost = positions.Sum(p => p.Quantity * p.AvgPrice);
        var emv = positions.Sum(p => p.MtmValue ?? (p.Quantity * p.AvgPrice));

        var snapshots = await _db.PortfolioValuationSnapshots.AsNoTracking()
            .Where(s => s.GoalPortfolioId == portfolioId)
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync(ct);

        var cashflows = await _db.PortfolioCashflows.AsNoTracking()
            .Where(c => c.GoalPortfolioId == portfolioId)
            .OrderBy(c => c.CashflowDate)
            .ToListAsync(ct);

        if (positions.Count == 0 && snapshots.Count == 0)
        {
            return new PortfolioPerformanceDto
            {
                GoalPortfolioId = portfolioId,
                MethodNote = "Sem posições nem snapshots — TWR/MWR indisponíveis.",
                SnapshotCount = 0,
                CashflowCount = cashflows.Count,
                UsedSnapshots = false,
                IsStub = false,
                Disclaimer = Disclaimer
            };
        }

        decimal? twr;
        decimal? mwr;
        string methodNote;
        var usedSnapshots = snapshots.Count >= 2;

        if (usedSnapshots)
        {
            var valuations = snapshots
                .Select(s => new PortfolioReturnMath.DatedValuation(s.SnapshotDate, s.TotalMarketValue))
                .ToList();

            // Inclui MtM atual se mais recente que o último snapshot
            var lastSnap = snapshots[^1].SnapshotDate;
            if (emv > 0 && DateTime.UtcNow.Date > lastSnap)
                valuations.Add(new PortfolioReturnMath.DatedValuation(DateTime.UtcNow.Date, emv));

            var datedCfs = cashflows
                .Select(c => new PortfolioReturnMath.DatedCashflow(c.CashflowDate, c.Amount))
                .ToList();

            var periods = PortfolioReturnMath.BuildSubPeriodsFromSnapshots(valuations, datedCfs);
            twr = PortfolioReturnMath.CalculateTwr(periods);

            mwr = PortfolioReturnMath.CalculateMwrFromSignedFlows(
                cashflows.Select(c => (c.CashflowDate, c.Amount)).ToList(), emv, DateTime.UtcNow);

            methodNote =
                $"TWR: {periods.Count} subperíodo(s) a partir de {snapshots.Count} snapshot(s) MtM" +
                (valuations.Count > snapshots.Count ? " + MtM atual" : "") +
                $" e {cashflows.Count} fluxo(s) externo(s). " +
                "MWR: IRR anualizado com aportes/resgates registrados vs MtM atual.";
        }
        else
        {
            // Fallback: aproximação (Fase 3)
            twr = PortfolioReturnMath.SinglePeriodReturn(cost, emv);

            var ordered = positions.OrderBy(p => p.CreatedAt).ToList();
            var subPeriods = new List<PortfolioReturnMath.SubPeriod>();
            var runningCost = 0m;
            var runningMtm = 0m;
            foreach (var pos in ordered)
            {
                var posCost = pos.Quantity * pos.AvgPrice;
                var posMtm = pos.MtmValue ?? posCost;
                if (runningCost > 0)
                    subPeriods.Add(new PortfolioReturnMath.SubPeriod(runningCost, runningMtm, 0));
                runningCost += posCost;
                runningMtm += posMtm;
            }
            if (runningCost > 0)
                subPeriods.Add(new PortfolioReturnMath.SubPeriod(runningCost, emv, 0));

            var linkedTwr = PortfolioReturnMath.CalculateTwr(subPeriods);
            if (linkedTwr.HasValue) twr = linkedTwr;

            List<PortfolioReturnMath.DatedCashflow> cfForMwr;
            if (cashflows.Count > 0)
            {
                cfForMwr = cashflows
                    .Where(c => c.Amount > 0)
                    .Select(c => new PortfolioReturnMath.DatedCashflow(c.CashflowDate, c.Amount))
                    .ToList();
                if (cfForMwr.Count == 0 && cost > 0)
                    cfForMwr.Add(new PortfolioReturnMath.DatedCashflow(portfolio.CreatedAt, cost));
            }
            else
            {
                cfForMwr = positions
                    .Select(p => new PortfolioReturnMath.DatedCashflow(p.CreatedAt, p.Quantity * p.AvgPrice))
                    .ToList();
            }

            mwr = PortfolioReturnMath.CalculateMwrIrr(cfForMwr, emv, DateTime.UtcNow);

            methodNote =
                snapshots.Count == 0
                    ? "TWR/MWR aproximados (sem snapshots MtM). Capture valuations na página de performance para o caminho oficial. "
                    : "Apenas 1 snapshot — TWR multi-período requer ≥2. Usando aproximação por custo/posições. ";
            methodNote += "MWR: IRR com cashflows registrados ou custo sintético das posições.";
        }

        return new PortfolioPerformanceDto
        {
            GoalPortfolioId = portfolioId,
            TwrPercent = RoundPct(twr),
            MwrPercent = RoundPct(mwr),
            TotalCostBasis = cost > 0 ? cost : snapshots.LastOrDefault()?.CostBasis,
            EndingMarketValue = emv > 0 ? emv : snapshots.LastOrDefault()?.TotalMarketValue,
            SnapshotCount = snapshots.Count,
            CashflowCount = cashflows.Count,
            UsedSnapshots = usedSnapshots,
            MethodNote = methodNote,
            IsStub = false,
            Disclaimer = Disclaimer
        };
    }

    private static decimal? RoundPct(decimal? fraction) =>
        fraction.HasValue ? Math.Round(fraction.Value * 100m, 4) : null;

    private const string Disclaimer =
        "Indicadores ilustrativos. Não constituem recomendação de investimento. " +
        "Rentabilidade passada não garante resultados futuros. " +
        "Open Finance produção (FAPI-BR / XP/BTG/Safra) requer parceiro ITP — modo simulado no produto.";
}

public sealed class PortfolioPerformanceDto
{
    public Guid GoalPortfolioId { get; set; }
    public decimal? TwrPercent { get; set; }
    public decimal? MwrPercent { get; set; }
    public decimal? TotalCostBasis { get; set; }
    public decimal? EndingMarketValue { get; set; }
    public int SnapshotCount { get; set; }
    public int CashflowCount { get; set; }
    public bool UsedSnapshots { get; set; }
    public string MethodNote { get; set; } = "";
    public string Disclaimer { get; set; } = "";
    public bool IsStub { get; set; }
}
