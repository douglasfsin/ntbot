namespace NtBot.Domain.Services.Portfolio;

/// <summary>
/// Matemática de rentabilidade (TWR / MWR) — pura, sem I/O.
/// <para>
/// <b>TWR (Time-Weighted Return)</b> — produto dos retornos de subperíodos entre fluxos:
/// <c>r_i = (EMV_i − BMV_i − CF_i) / BMV_i</c>, depois <c>TWR = Π(1+r_i) − 1</c>.
/// CF positivo = aporte externo na carteira.
/// </para>
/// <para>
/// <b>MWR (Money-Weighted / IRR)</b> — taxa que zera o NPV dos cashflows
/// (aportes negativos do ponto de vista do investidor; valor final positivo).
/// </para>
/// Sem histórico de valuations periódicas, o serviço de performance usa aproximação
/// de um único período a partir do custo das posições vs MtM atual.
/// </summary>
public static class PortfolioReturnMath
{
    /// <summary>Evento de valuation + fluxo externo no mesmo instante (fluxo após a valuation de abertura).</summary>
    public sealed record SubPeriod(
        decimal BeginningMarketValue,
        decimal EndingMarketValue,
        decimal ExternalCashflow);

    /// <summary>Cashflow datado (valor positivo = aporte na carteira).</summary>
    public sealed record DatedCashflow(DateTime Date, decimal Amount);

    /// <summary>
    /// TWR clássico por subperíodos. Retorna decimal (ex.: 0.12 = 12%).
    /// Ignora subperíodos com BMV ≤ 0 (sem base).
    /// </summary>
    public static decimal? CalculateTwr(IReadOnlyList<SubPeriod> periods)
    {
        if (periods is null || periods.Count == 0) return null;

        var product = 1m;
        var any = false;
        foreach (var p in periods)
        {
            if (p.BeginningMarketValue <= 0) continue;
            var r = (p.EndingMarketValue - p.BeginningMarketValue - p.ExternalCashflow) / p.BeginningMarketValue;
            product *= 1m + r;
            any = true;
        }

        return any ? product - 1m : null;
    }

    /// <summary>
    /// Aproximação TWR de um período: (EMV − custo) / custo, quando não há snapshots.
    /// </summary>
    public static decimal? SinglePeriodReturn(decimal totalCostBasis, decimal endingMarketValue)
    {
        if (totalCostBasis <= 0) return null;
        return (endingMarketValue - totalCostBasis) / totalCostBasis;
    }

    /// <summary>Ponto de valuation datado (MtM).</summary>
    public sealed record DatedValuation(DateTime Date, decimal MarketValue);

    /// <summary>
    /// Monta subperíodos TWR a partir de snapshots MtM + cashflows externos entre eles.
    /// Cashflows com Amount &gt; 0 = aporte; &lt; 0 = resgate. Soma no intervalo (prev, curr].
    /// </summary>
    public static IReadOnlyList<SubPeriod> BuildSubPeriodsFromSnapshots(
        IReadOnlyList<DatedValuation> valuations,
        IReadOnlyList<DatedCashflow> externalCashflows)
    {
        if (valuations is null || valuations.Count < 2)
            return Array.Empty<SubPeriod>();

        var ordered = valuations.OrderBy(v => v.Date).ToList();
        var cfs = (externalCashflows ?? Array.Empty<DatedCashflow>())
            .OrderBy(c => c.Date)
            .ToList();

        var periods = new List<SubPeriod>();
        for (var i = 1; i < ordered.Count; i++)
        {
            var prev = ordered[i - 1];
            var curr = ordered[i];
            var cfSum = cfs
                .Where(c => c.Date > prev.Date && c.Date <= curr.Date)
                .Sum(c => c.Amount);
            periods.Add(new SubPeriod(prev.MarketValue, curr.MarketValue, cfSum));
        }

        return periods;
    }

    /// <summary>
    /// IRR / MWR anualizado em fração (não %): resolve NPV=0 com Newton-Raphson.
    /// Cashflows: aportes como negativos (saída do investidor), valor final positivo.
    /// Datas relativas ao primeiro fluxo; taxa é por dia composto → anualizado (365d).
    /// </summary>
    public static decimal? CalculateMwrIrr(
        IReadOnlyList<DatedCashflow> cashflows,
        decimal endingMarketValue,
        DateTime endingDate,
        int maxIterations = 64)
    {
        if (cashflows is null || cashflows.Count == 0) return null;
        if (endingMarketValue < 0) return null;

        var points = new List<(DateTime Date, decimal Amount)>();
        foreach (var c in cashflows)
            points.Add((c.Date, -Math.Abs(c.Amount))); // investidor aportou
        points.Add((endingDate, endingMarketValue));
        points.Sort((a, b) => a.Date.CompareTo(b.Date));

        if (points.Count < 2) return null;
        var t0 = points[0].Date;

        // Guess: single-period style
        var invested = cashflows.Sum(c => Math.Abs(c.Amount));
        if (invested <= 0) return null;

        double r = (double)((endingMarketValue - invested) / invested); // rough period return
        // Convert rough period return to daily rate guess
        var days = Math.Max((endingDate - t0).TotalDays, 1);
        r = Math.Pow(1 + r, 1.0 / days) - 1;
        if (double.IsNaN(r) || double.IsInfinity(r)) r = 0.0001;

        for (var i = 0; i < maxIterations; i++)
        {
            double npv = 0, dNpv = 0;
            foreach (var point in points)
            {
                var t = (point.Date - t0).TotalDays;
                var factor = Math.Pow(1 + r, t);
                if (factor == 0 || double.IsNaN(factor) || double.IsInfinity(factor))
                    return null;
                npv += (double)point.Amount / factor;
                dNpv -= t * (double)point.Amount / (factor * (1 + r));
            }

            if (Math.Abs(dNpv) < 1e-12) break;
            var next = r - npv / dNpv;
            if (double.IsNaN(next) || double.IsInfinity(next)) break;
            if (Math.Abs(next - r) < 1e-10)
            {
                r = next;
                break;
            }
            r = next;
        }

        // Annualize daily rate
        var annual = Math.Pow(1 + r, 365) - 1;
        if (double.IsNaN(annual) || double.IsInfinity(annual)) return null;
        return (decimal)annual;
    }

    /// <summary>
    /// IRR/MWR com fluxos assinados: Amount&gt;0 = aporte (saída investidor), Amount&lt;0 = resgate (entrada).
    /// </summary>
    public static decimal? CalculateMwrFromSignedFlows(
        IReadOnlyList<(DateTime Date, decimal Amount)> flows,
        decimal endingMarketValue,
        DateTime endingDate,
        int maxIterations = 64)
    {
        if (endingMarketValue < 0) return null;

        var points = new List<(DateTime Date, decimal Amount)>();
        foreach (var f in flows)
        {
            if (f.Amount == 0) continue;
            points.Add((f.Date, -f.Amount));
        }
        points.Add((endingDate, endingMarketValue));
        points.Sort((a, b) => a.Date.CompareTo(b.Date));
        if (points.Count < 2) return null;

        var deposits = flows.Where(f => f.Amount > 0).Select(f => f.Amount).Sum();
        var invested = deposits > 0 ? deposits : Math.Abs(flows.Sum(f => f.Amount));
        if (invested <= 0) invested = endingMarketValue > 0 ? endingMarketValue : 1;

        var t0 = points[0].Date;
        double r = (double)((endingMarketValue - invested) / invested);
        var days = Math.Max((endingDate - t0).TotalDays, 1);
        r = Math.Pow(1 + Math.Max(r, -0.99), 1.0 / days) - 1;
        if (double.IsNaN(r) || double.IsInfinity(r)) r = 0.0001;

        for (var i = 0; i < maxIterations; i++)
        {
            double npv = 0, dNpv = 0;
            foreach (var point in points)
            {
                var t = (point.Date - t0).TotalDays;
                var factor = Math.Pow(1 + r, t);
                if (factor == 0 || double.IsNaN(factor) || double.IsInfinity(factor))
                    return null;
                npv += (double)point.Amount / factor;
                dNpv -= t * (double)point.Amount / (factor * (1 + r));
            }
            if (Math.Abs(dNpv) < 1e-12) break;
            var next = r - npv / dNpv;
            if (double.IsNaN(next) || double.IsInfinity(next)) break;
            if (Math.Abs(next - r) < 1e-10) { r = next; break; }
            r = next;
        }

        var annual = Math.Pow(1 + r, 365) - 1;
        if (double.IsNaN(annual) || double.IsInfinity(annual)) return null;
        return (decimal)annual;
    }
}
