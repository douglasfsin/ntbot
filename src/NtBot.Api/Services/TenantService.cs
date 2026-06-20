using NtBot.Api.Services.Interfaces;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace NtBot.Api.Services;

public class TenantService : ITenantService
{
    private readonly NtBotDbContext _context;

    public TenantService(NtBotDbContext context)
    {
        _context = context;
    }

    public async Task<Tenant> GetTenantAsync(Guid tenantId)
    {
        return await _context.Tenants.FindAsync(tenantId);
    }

    public async Task UpdateTenantLimitsAsync(Guid tenantId, TenantLimits limits)
    {
        var tenant = await _context.Tenants.FindAsync(tenantId);
        if (tenant != null)
        {
            tenant.MaxActivePositions = limits.MaxConcurrentPositions;
            tenant.MaxDailyTrades = limits.MaxDailyTrades;
            tenant.MaxRiskPerTrade = limits.MaxRiskPerTrade;
            tenant.MaxDailyLoss = limits.MaxDailyLoss;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ValidateTenantAsync(Guid tenantId)
    {
        var tenant = await _context.Tenants.FindAsync(tenantId);
        return tenant?.IsActive ?? false;
    }
}