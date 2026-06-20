using NtBot.Api.Services.Interfaces;
using NtBot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NtBot.Api.Services;

public class RiskManager : IRiskManager
{
    private readonly ITenantService _tenantService;
    private readonly ITradingService _tradingService;

    public RiskManager(ITenantService tenantService, ITradingService tradingService)
    {
        _tenantService = tenantService;
        _tradingService = tradingService;
    }

    public async Task<RiskValidationResult> ValidateOrderAsync(Guid tenantId, OrderRequest request)
    {
        var result = new RiskValidationResult { IsValid = true };
        var metrics = await GetRiskMetricsAsync(tenantId);
        result.Metrics = metrics;

        // Check position limits
        var positions = await _tradingService.GetAllPositionsAsync(tenantId);
        if (positions.Count() >= metrics.OpenPositions)
        {
            result.IsValid = false;
            result.Reason = $"Maximum open positions limit reached ({metrics.OpenPositions})";
            return result;
        }

        // Check exposure per symbol
        var symbolExposure = positions
            .Where(p => p.Symbol == request.Symbol)
            .Sum(p => Math.Abs(p.Volume * p.EntryPrice));

        var newExposure = symbolExposure + Math.Abs(request.Quantity * (request.Price ?? 0));
        if (newExposure > 10000) // Max exposure per symbol
        {
            result.IsValid = false;
            result.Reason = $"Maximum exposure per symbol exceeded (${newExposure:F2} > $10,000)";
            return result;
        }

        // Check risk per trade
        var riskAmount = Math.Abs(request.Quantity * (request.Price ?? 0)) * 0.02m; // 2% risk
        if (riskAmount > 200) // Max risk per trade
        {
            result.IsValid = false;
            result.Reason = $"Risk per trade too high (${riskAmount:F2} > $200)";
            return result;
        }

        // Check correlation
        var correlationRisk = await CheckCorrelationRiskAsync(tenantId, request.Symbol);
        if (correlationRisk > 0.8m)
        {
            result.IsValid = false;
            result.Reason = $"High correlation risk detected ({correlationRisk:P1})";
            return result;
        }

        return result;
    }

    public async Task<RiskValidationResult> ValidateGridOrderAsync(Guid tenantId, CreateGridOrderRequest request)
    {
        var result = new RiskValidationResult { IsValid = true };
        var metrics = await GetRiskMetricsAsync(tenantId);
        result.Metrics = metrics;

        // Check if grid trading is allowed
        var tenant = await _tenantService.GetTenantAsync(tenantId);
        if (!tenant.IsActive)
        {
            result.IsValid = false;
            result.Reason = "Tenant is not active";
            return result;
        }

        // Calculate total exposure
        var totalVolume = request.LotSize;
        if (request.UseMartingale)
        {
            // Calculate total volume with martingale
            for (int i = 1; i < request.MaxLevels; i++)
            {
                totalVolume += request.LotSize * (decimal)Math.Pow((double)request.MartingaleMultiplier, i);
            }
        }
        else
        {
            totalVolume *= request.MaxLevels;
        }

        var totalExposure = totalVolume * request.BasePrice;

        if (totalExposure > metrics.TotalExposure * 0.1m) // Max 10% of total exposure
        {
            result.IsValid = false;
            result.Reason = $"Grid exposure too high (${totalExposure:F2} > {metrics.TotalExposure * 0.1m:F2})";
            return result;
        }

        // Check martingale multiplier
        if (request.UseMartingale && request.MartingaleMultiplier > 3.0m)
        {
            result.IsValid = false;
            result.Reason = "Martingale multiplier too aggressive (>3.0)";
            return result;
        }

        return result;
    }

    public async Task<RiskMetrics> GetRiskMetricsAsync(Guid tenantId)
    {
        var account = await _tradingService.GetAccountInfoAsync(tenantId);
        var positions = await _tradingService.GetAllPositionsAsync(tenantId);

        var metrics = new RiskMetrics
        {
            DailyPnL = account?.DailyNetProfit ?? 0,
            TotalExposure = positions.Sum(p => Math.Abs(p.Volume * p.CurrentPrice)),
            OpenPositions = positions.Count(),
            RiskLimit = 100000, // Default
            DailyLossLimit = 2000,
            CorrelationLimit = 0.7m
        };

        // Calculate max drawdown
        metrics.MaxDrawdown = await CalculateMaxDrawdownAsync(tenantId);
        metrics.MaxDrawdownPercent = account?.Balance > 0 ? (metrics.MaxDrawdown / account.Balance) * 100 : 0;

        return metrics;
    }

    public async Task UpdateRiskLimitsAsync(Guid tenantId, RiskLimits limits)
    {
        var tenantLimits = new TenantLimits
        {
            MaxConcurrentPositions = limits.MaxOpenPositions,
            MaxDailyTrades = 100, // Default
            MaxRiskPerTrade = limits.MaxRiskPerTrade,
            MaxDailyLoss = limits.MaxDailyLoss
        };

        await _tenantService.UpdateTenantLimitsAsync(tenantId, tenantLimits);
    }

    public async Task<bool> CheckDailyLossLimitAsync(Guid tenantId)
    {
        var metrics = await GetRiskMetricsAsync(tenantId);
        return metrics.DailyPnL >= -metrics.DailyLossLimit;
    }

    public async Task<bool> CheckMaxDrawdownAsync(Guid tenantId)
    {
        var metrics = await GetRiskMetricsAsync(tenantId);
        return metrics.MaxDrawdownPercent <= 5.0m; // 5% max drawdown
    }

    private async Task<decimal> CalculateMaxDrawdownAsync(Guid tenantId)
    {
        // Simplified calculation - in real implementation,
        // this would analyze historical P&L data
        var account = await _tradingService.GetAccountInfoAsync(tenantId);
        return account?.Balance * 0.02m ?? 0; // Assume 2% drawdown
    }

    private async Task<decimal> CheckCorrelationRiskAsync(Guid tenantId, string symbol)
    {
        // Simplified correlation check - in real implementation,
        // this would analyze correlation matrix
        var positions = await _tradingService.GetAllPositionsAsync(tenantId);
        var correlatedSymbols = new[] { "EURUSD", "GBPUSD", "USDJPY" };

        if (correlatedSymbols.Contains(symbol))
        {
            return positions.Any(p => correlatedSymbols.Contains(p.Symbol)) ? 0.85m : 0.3m;
        }

        return 0.2m; // Low correlation
    }
}