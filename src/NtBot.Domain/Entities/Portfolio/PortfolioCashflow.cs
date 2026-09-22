namespace NtBot.Domain.Entities.Portfolio;

/// <summary>
/// Fluxo externo na carteira (aporte / resgate) para TWR/MWR.
/// Amount positivo = entrada na carteira (aporte); negativo = saída (resgate).
/// </summary>
public class PortfolioCashflow
{
    public Guid Id { get; set; }
    public Guid GoalPortfolioId { get; set; }
    public GoalPortfolio? GoalPortfolio { get; set; }

    public DateTime CashflowDate { get; set; }
    public decimal Amount { get; set; }
    public PortfolioCashflowType Type { get; set; } = PortfolioCashflowType.Deposit;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum PortfolioCashflowType
{
    Deposit = 0,
    Withdrawal = 1
}
