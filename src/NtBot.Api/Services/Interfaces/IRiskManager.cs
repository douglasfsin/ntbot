using NtBot.Domain.Entities;
using NtBot.Api.Services;
using System.Threading.Tasks;

namespace NtBot.Api.Services.Interfaces;

public interface IRiskManager
{
    Task<RiskValidationResult> ValidateOrderAsync(Guid tenantId, OrderRequest request);
    Task<RiskValidationResult> ValidateGridOrderAsync(Guid tenantId, CreateGridOrderRequest request);
    Task<RiskMetrics> GetRiskMetricsAsync(Guid tenantId);
    Task UpdateRiskLimitsAsync(Guid tenantId, RiskLimits limits);
    Task<bool> CheckDailyLossLimitAsync(Guid tenantId);
    Task<bool> CheckMaxDrawdownAsync(Guid tenantId);
}

public class RiskValidationResult
{
    public bool IsValid { get; set; }
    public string Reason { get; set; }
    public RiskMetrics Metrics { get; set; }
}

public class RiskLimits
{
    public decimal MaxDailyLoss { get; set; }
    public decimal MaxDrawdown { get; set; }
    public int MaxOpenPositions { get; set; }
    public decimal MaxRiskPerTrade { get; set; }
    public decimal MaxExposurePerSymbol { get; set; }
}

public class RiskMetrics
{
    public decimal DailyPnL { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public decimal TotalExposure { get; set; }
    public int OpenPositions { get; set; }
    public decimal RiskLimit { get; set; }
    public decimal DailyLossLimit { get; set; }
    public decimal CorrelationLimit { get; set; }
}