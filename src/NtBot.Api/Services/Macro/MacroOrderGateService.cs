using Microsoft.EntityFrameworkCore;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;
using NtBot.Macro.Configuration;
using NtBot.Macro.DTO;
using NtBot.Macro.Engine;
using NtBot.Macro.Services;

namespace NtBot.Api.Services.Macro;

public sealed class MacroOrderGateService : IMacroOrderGate
{
    private readonly IMacroIntelligenceService _macro;
    private readonly IMacroRecommendationEngine _recommendations;
    private readonly NtBotDbContext _db;
    private readonly ILogger<MacroOrderGateService> _logger;

    public MacroOrderGateService(
        IMacroIntelligenceService macro,
        IMacroRecommendationEngine recommendations,
        NtBotDbContext db,
        ILogger<MacroOrderGateService> logger)
    {
        _macro = macro;
        _recommendations = recommendations;
        _db = db;
        _logger = logger;
    }

    public async Task<MacroOrderGateResult> EvaluateAsync(
        Guid tenantId,
        string symbol,
        TradeDirection direction,
        CancellationToken cancellationToken = default)
    {
        var config = await ResolveAssetConfigAsync(tenantId, symbol, cancellationToken);
        if (config is null || (!config.EnableMacroFilter && !config.EnableEconomicCalendar))
            return new MacroOrderGateResult();

        var normalized = MacroSymbolAliases.Normalize(symbol);
        var snapshot = await _macro.GetCurrentSnapshotAsync(normalized, cancellationToken);
        var multiplier = 1m;
        var economicActive = false;

        if (config.EnableEconomicCalendar)
        {
            var calendarBlock = CheckCalendarBlock(snapshot.UpcomingEvents);
            if (calendarBlock is not null)
            {
                _logger.LogInformation(
                    "Macro calendar block for {Symbol}: {Reason}", symbol, calendarBlock);
                return new MacroOrderGateResult
                {
                    Allowed = false,
                    Reason = calendarBlock,
                    EconomicEventActive = true
                };
            }

            economicActive = snapshot.UpcomingEvents.Any(e =>
                IsHighImpact(e.Impact) &&
                Math.Abs((e.EventTime - DateTime.UtcNow).TotalMinutes) <= 60);
        }

        if (!config.EnableMacroFilter)
        {
            return new MacroOrderGateResult
            {
                Allowed = true,
                SizeMultiplier = multiplier,
                EconomicEventActive = economicActive
            };
        }

        if (snapshot.Confidence < config.MinConfidenceScore)
        {
            return new MacroOrderGateResult
            {
                Allowed = false,
                Reason = $"Confiança macro insuficiente ({snapshot.Confidence:F0}% < {config.MinConfidenceScore:F0}%)"
            };
        }

        if (snapshot.Volatility is MacroLevel.High or MacroLevel.VeryHigh)
        {
            multiplier = 0.5m;
            if (snapshot.Volatility is MacroLevel.VeryHigh)
            {
                return new MacroOrderGateResult
                {
                    Allowed = false,
                    Reason = "Volatilidade macro extremamente elevada"
                };
            }
        }

        var recommendation = snapshot.Recommendations
            .FirstOrDefault(r => r.Ticker.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            ?? _recommendations.GetRecommendation(snapshot, normalized);

        if (ConflictsWithDirection(recommendation.Action, direction))
        {
            return new MacroOrderGateResult
            {
                Allowed = false,
                Reason = $"Recomendação macro {recommendation.Action} conflita com {direction}"
            };
        }

        if (recommendation.RiskLevel is MacroRiskLevel.Extreme)
        {
            return new MacroOrderGateResult
            {
                Allowed = false,
                Reason = "Risco macro extremo para o ativo"
            };
        }

        if (recommendation.RiskLevel is MacroRiskLevel.High)
            multiplier = Math.Min(multiplier, 0.5m);

        return new MacroOrderGateResult
        {
            Allowed = true,
            SizeMultiplier = multiplier,
            EconomicEventActive = economicActive
        };
    }

    private async Task<AssetConfiguration?> ResolveAssetConfigAsync(
        Guid tenantId,
        string symbol,
        CancellationToken cancellationToken)
    {
        var normalized = MacroSymbolAliases.Normalize(symbol);
        var configs = await _db.AssetConfigurations
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .ToListAsync(cancellationToken);

        return configs.FirstOrDefault(a =>
                   a.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) ||
                   a.Symbol.Equals(normalized, StringComparison.OrdinalIgnoreCase))
               ?? configs.FirstOrDefault();
    }

    private static string? CheckCalendarBlock(IReadOnlyList<MacroCalendarEventDto> events)
    {
        foreach (var evt in events)
        {
            if (!IsHighImpact(evt.Impact))
                continue;

            var minutes = (evt.EventTime - DateTime.UtcNow).TotalMinutes;
            if (minutes is >= -15 and <= 30)
                return $"Evento de alto impacto: {evt.EventName} ({evt.EventTime:yyyy-MM-dd HH:mm} UTC)";
        }

        return null;
    }

    private static bool IsHighImpact(string impact) =>
        impact.Equals("HIGH", StringComparison.OrdinalIgnoreCase);

    private static bool ConflictsWithDirection(MacroRecommendationAction action, TradeDirection direction) =>
        direction switch
        {
            TradeDirection.LONG => action is MacroRecommendationAction.StrongSell or MacroRecommendationAction.ModerateSell,
            TradeDirection.SHORT => action is MacroRecommendationAction.StrongBuy or MacroRecommendationAction.ModerateBuy,
            _ => false
        };
}
