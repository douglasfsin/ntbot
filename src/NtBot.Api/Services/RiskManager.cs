using Microsoft.EntityFrameworkCore;
using NtBot.Api.Services.Interfaces;
using NtBot.Api.Services.Macro;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;
using NtBot.Macro.Configuration;

namespace NtBot.Api.Services;

public class RiskManager : IRiskManager
{
    private readonly ITenantService _tenantService;
    private readonly Lazy<ITradingService> _tradingService;
    private readonly IMacroOrderGate _macroGate;
    private readonly NtBotDbContext _db;

    public RiskManager(
        ITenantService tenantService,
        Lazy<ITradingService> tradingService,
        IMacroOrderGate macroGate,
        NtBotDbContext db)
    {
        _tenantService = tenantService;
        _tradingService = tradingService;
        _macroGate = macroGate;
        _db = db;
    }

    public async Task<RiskValidationResult> ValidateOrderAsync(Guid tenantId, OrderRequest request)
    {
        var result = new RiskValidationResult { IsValid = true };
        var metrics = await GetRiskMetricsAsync(tenantId);
        result.Metrics = metrics;

        if (!await CheckDailyLossLimitAsync(tenantId))
        {
            result.IsValid = false;
            result.Reason = $"Limite de perda diária atingido (PnL: ${metrics.DailyPnL:F2})";
            return result;
        }

        if (!await CheckMaxDrawdownAsync(tenantId))
        {
            result.IsValid = false;
            result.Reason = $"Drawdown máximo excedido ({metrics.MaxDrawdownPercent:F1}%)";
            return result;
        }

        var assetConfig = await ResolveAssetConfigAsync(tenantId, request.Symbol);
        var maxPositions = assetConfig?.MaxPositionSize ?? 3;
        var maxExposure = 10000m;
        var maxRiskPerTrade = assetConfig?.RiskPerTrade * 100 ?? 200m;

        var positions = await _tradingService.Value.GetAllPositionsAsync(tenantId);
        if (positions.Count() >= maxPositions)
        {
            result.IsValid = false;
            result.Reason = $"Limite de posições abertas atingido ({maxPositions})";
            return result;
        }

        var symbolExposure = positions
            .Where(p => p.Symbol == request.Symbol)
            .Sum(p => Math.Abs(p.Volume * p.EntryPrice));

        var newExposure = symbolExposure + Math.Abs(request.Quantity * (request.Price ?? 0));
        if (newExposure > maxExposure)
        {
            result.IsValid = false;
            result.Reason = $"Exposição máxima por símbolo excedida (${newExposure:F2} > ${maxExposure:F2})";
            return result;
        }

        var riskAmount = Math.Abs(request.Quantity * (request.Price ?? 0)) * 0.02m;
        if (riskAmount > maxRiskPerTrade)
        {
            result.IsValid = false;
            result.Reason = $"Risco por operação muito alto (${riskAmount:F2} > ${maxRiskPerTrade:F2})";
            return result;
        }

        var correlationRisk = await CheckCorrelationRiskAsync(tenantId, request.Symbol);
        if (correlationRisk > metrics.CorrelationLimit)
        {
            result.IsValid = false;
            result.Reason = $"Risco de correlação elevado ({correlationRisk:P1})";
            return result;
        }

        var macroCheck = await _macroGate.EvaluateAsync(tenantId, request.Symbol, request.Direction);
        if (!macroCheck.Allowed)
        {
            result.IsValid = false;
            result.Reason = macroCheck.Reason ?? "Bloqueado pelo filtro macro";
            return result;
        }

        if (macroCheck.SizeMultiplier < 1m && request.Quantity > 0)
            request.Quantity *= macroCheck.SizeMultiplier;

        return result;
    }

    public async Task<RiskValidationResult> ValidateGridOrderAsync(Guid tenantId, CreateGridOrderRequest request)
    {
        var result = new RiskValidationResult { IsValid = true };
        var metrics = await GetRiskMetricsAsync(tenantId);
        result.Metrics = metrics;

        var tenant = await _tenantService.GetTenantAsync(tenantId);
        if (!tenant.IsActive)
        {
            result.IsValid = false;
            result.Reason = "Tenant inativo";
            return result;
        }

        if (!await CheckDailyLossLimitAsync(tenantId))
        {
            result.IsValid = false;
            result.Reason = "Limite de perda diária atingido";
            return result;
        }

        var macroCheck = await _macroGate.EvaluateAsync(
            tenantId,
            request.Symbol,
            request.Direction == GridDirection.Sell ? TradeDirection.SHORT : TradeDirection.LONG);

        if (!macroCheck.Allowed)
        {
            result.IsValid = false;
            result.Reason = macroCheck.Reason ?? "Grid bloqueado pelo filtro macro";
            return result;
        }

        var totalVolume = request.LotSize;
        if (request.UseMartingale)
        {
            for (var i = 1; i < request.MaxLevels; i++)
                totalVolume += request.LotSize * (decimal)Math.Pow((double)request.MartingaleMultiplier, i);
        }
        else
        {
            totalVolume *= request.MaxLevels;
        }

        totalVolume *= macroCheck.SizeMultiplier;
        var totalExposure = totalVolume * request.BasePrice;

        if (totalExposure > metrics.TotalExposure * 0.1m)
        {
            result.IsValid = false;
            result.Reason = $"Exposição do grid muito alta (${totalExposure:F2})";
            return result;
        }

        if (request.UseMartingale && request.MartingaleMultiplier > 3.0m)
        {
            result.IsValid = false;
            result.Reason = "Multiplicador martingale muito agressivo (>3.0)";
            return result;
        }

        return result;
    }

    public async Task<RiskMetrics> GetRiskMetricsAsync(Guid tenantId)
    {
        var account = await _tradingService.Value.GetAccountInfoAsync(tenantId);
        var positions = await _tradingService.Value.GetAllPositionsAsync(tenantId);
        var assetConfig = await _db.AssetConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.IsActive);

        var metrics = new RiskMetrics
        {
            DailyPnL = account?.DailyNetProfit ?? 0,
            TotalExposure = positions.Sum(p => Math.Abs(p.Volume * p.CurrentPrice)),
            OpenPositions = positions.Count(),
            RiskLimit = 100000,
            DailyLossLimit = assetConfig?.MaxDailyLoss * 1000 ?? 2000,
            CorrelationLimit = 0.7m
        };

        metrics.MaxDrawdown = await CalculateMaxDrawdownAsync(tenantId);
        metrics.MaxDrawdownPercent = account?.Balance > 0 ? (metrics.MaxDrawdown / account.Balance) * 100 : 0;

        return metrics;
    }

    public async Task UpdateRiskLimitsAsync(Guid tenantId, RiskLimits limits)
    {
        var tenantLimits = new TenantLimits
        {
            MaxConcurrentPositions = limits.MaxOpenPositions,
            MaxDailyTrades = 100,
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
        return metrics.MaxDrawdownPercent <= 5.0m;
    }

    private async Task<AssetConfiguration?> ResolveAssetConfigAsync(Guid tenantId, string symbol)
    {
        var normalized = MacroSymbolAliases.Normalize(symbol);
        var configs = await _db.AssetConfigurations
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .ToListAsync();

        return configs.FirstOrDefault(a =>
                   a.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) ||
                   a.Symbol.Equals(normalized, StringComparison.OrdinalIgnoreCase))
               ?? configs.FirstOrDefault();
    }

    private async Task<decimal> CalculateMaxDrawdownAsync(Guid tenantId)
    {
        var account = await _tradingService.Value.GetAccountInfoAsync(tenantId);
        return account?.Balance * 0.02m ?? 0;
    }

    private async Task<decimal> CheckCorrelationRiskAsync(Guid tenantId, string symbol)
    {
        var positions = await _tradingService.Value.GetAllPositionsAsync(tenantId);
        var correlatedSymbols = new[] { "EURUSD", "GBPUSD", "USDJPY" };

        if (correlatedSymbols.Contains(symbol))
            return positions.Any(p => correlatedSymbols.Contains(p.Symbol)) ? 0.85m : 0.3m;

        return 0.2m;
    }
}
