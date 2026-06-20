using NtBot.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace NtBot.Api.Services.Interfaces;

public interface ITenantService
{
    Task<Tenant> GetTenantAsync(Guid tenantId);
    Task<bool> ValidateTenantAsync(Guid tenantId);
    Task UpdateTenantLimitsAsync(Guid tenantId, TenantLimits limits);
}

public class TenantLimits
{
    public int MaxConcurrentPositions { get; set; }
    public int MaxDailyTrades { get; set; }
    public decimal MaxRiskPerTrade { get; set; }
    public decimal MaxDailyLoss { get; set; }
}