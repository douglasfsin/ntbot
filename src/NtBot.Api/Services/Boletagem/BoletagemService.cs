using Microsoft.EntityFrameworkCore;
using NtBot.Api.Services.MarketData;
using NtBot.Domain.Entities;
using NtBot.Infrastructure.Persistence;
using NtBot.MarketDrivers.Services;
using NtBot.Shared.Trading;

namespace NtBot.Api.Services.Boletagem;

public interface IBoletagemService
{
    Task<BoletaSessionDto?> GetActiveAsync(Guid tenantId, string symbol, CancellationToken ct = default);
    Task<BoletaSessionDto> UpsertSessionAsync(Guid tenantId, UpsertBoletaRequest request, CancellationToken ct = default);
    Task<BoletaSessionDto> ExecuteAsync(Guid tenantId, ExecuteBoletaRequest request, CancellationToken ct = default);
    Task<BoletaSessionDto> CloseAllAsync(Guid tenantId, string symbol, string reason, CancellationToken ct = default);
    Task<BoletaSessionDto?> MonitorAsync(Guid tenantId, string symbol, CancellationToken ct = default);
    Task<IReadOnlyList<BoletaStrategyPlanDto>> PreviewPlanAsync(UpsertBoletaRequest request, CancellationToken ct = default);
    IReadOnlyList<string> ListStrategies();
}

public sealed class BoletagemService : IBoletagemService
{
    private readonly NtBotDbContext _db;
    private readonly IMt5TradeGateway _mt5;
    private readonly IMarketCandleService _candles;
    private readonly IMarketDriversService _marketDrivers;
    private readonly IReadOnlyDictionary<string, IBoletaStrategy> _strategies;
    private readonly ILogger<BoletagemService> _logger;

    public BoletagemService(
        NtBotDbContext db,
        IMt5TradeGateway mt5,
        IMarketCandleService candles,
        IMarketDriversService marketDrivers,
        IEnumerable<IBoletaStrategy> strategies,
        ILogger<BoletagemService> logger)
    {
        _db = db;
        _mt5 = mt5;
        _candles = candles;
        _marketDrivers = marketDrivers;
        _strategies = strategies.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public IReadOnlyList<string> ListStrategies() => Domain.Entities.BoletaStrategies.All;

    public async Task<BoletaSessionDto?> GetActiveAsync(Guid tenantId, string symbol, CancellationToken ct = default)
    {
        var session = await FindTodaySessionAsync(tenantId, symbol, ct);
        if (session is null) return null;
        await RefreshPnlAsync(session, ct);
        await ApplyTrailingStopsAsync(session, ct);
        await TrySaveChangesAsync(ct);
        var lastPrice = await _mt5.GetLastPriceAsync(session.Symbol, ct);
        var mdRec = await ResolveMarketDriversRecommendationAsync(session.Symbol, null, ct);
        var plan = await BuildPlanAsync(new BoletaStrategyContext
        {
            Symbol = session.Symbol,
            LotSize = session.LotSize,
            ConfluenceScore = session.ScenarioConfluenceScore,
            Recommendation = session.ScenarioRecommendation,
            Bias = session.ScenarioBias,
            RiskLevel = session.RiskIndication,
            LastPrice = lastPrice,
            ForcedDirection = session.PreferredDirection,
            MarketDriversRecommendation = mdRec,
            AllowNeutralEntries = session.AllowNeutralEntries,
            PreferDemandWithTrend = true
        }, session.Strategy, ct);
        return await MapAsync(session, plan, ct);
    }

    public async Task<BoletaSessionDto> UpsertSessionAsync(Guid tenantId, UpsertBoletaRequest request, CancellationToken ct = default)
    {
        var symbol = NormalizeSymbol(request.Symbol);
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("SÃ­mbolo obrigatÃ³rio.");

        var strategy = NormalizeStrategy(request.Strategy);
        var session = await FindTodaySessionAsync(tenantId, symbol, ct);
        var balance = request.ReferenceBalance
            ?? await _mt5.GetAccountBalanceAsync(ct)
            ?? session?.ReferenceBalance
            ?? 10_000m;

        if (session is null)
        {
            session = new BoletaSession
            {
                TenantId = tenantId,
                Symbol = symbol,
                SessionDate = DateTime.UtcNow.Date,
                ReferenceBalance = balance,
                PeakEquity = balance,
                Status = BoletaSessionStatus.Active
            };
            _db.BoletaSessions.Add(session);
        }

        session.Strategy = strategy;
        session.DailyProfitTarget = Math.Max(0, request.DailyProfitTarget);
        session.MaxDrawdownPercent = Math.Clamp(request.MaxDrawdownPercent, 0.1m, 50m);
        session.TrailingStopPercent = Math.Clamp(request.TrailingStopPercent, 0m, 20m);
        session.LotSize = Math.Max(0.01m, request.LotSize);
        session.AutomationEnabled = request.AutomationEnabled;
        session.TradingEnabled = request.TradingEnabled;
        session.AllowNeutralEntries = request.AllowNeutralEntries;
        session.PreferredDirection = BoletaStrategyContext.NormalizeDirection(request.Direction);
        session.RiskIndication = string.IsNullOrWhiteSpace(request.RiskLevel) ? session.RiskIndication : request.RiskLevel!;
        session.ScenarioBias = string.IsNullOrWhiteSpace(request.Bias) ? session.ScenarioBias : request.Bias!;
        session.ScenarioRecommendation = string.IsNullOrWhiteSpace(request.Recommendation)
            ? session.ScenarioRecommendation
            : request.Recommendation!;
        session.ScenarioConfluenceScore = request.ConfluenceScore ?? session.ScenarioConfluenceScore;
        if (request.ReferenceBalance is > 0)
        {
            session.ReferenceBalance = request.ReferenceBalance.Value;
            if (session.PeakEquity < session.ReferenceBalance)
                session.PeakEquity = session.ReferenceBalance;
        }

        if (session.Status is BoletaSessionStatus.ClosedMeta
            or BoletaSessionStatus.ClosedDrawdown
            or BoletaSessionStatus.ClosedManual)
        {
            session.Status = BoletaSessionStatus.Active;
            session.MetaReached = false;
            session.DrawdownBreached = false;
            session.PositionsClosedByRule = false;
            session.ResultPercentOfBalance = null;
        }

        session.UpdatedAt = DateTime.UtcNow;
        var trailNote = session.TrailingStopPercent > 0
            ? $" · trail {session.TrailingStopPercent:N2}%"
            : "";
        var runNote = session.TradingEnabled ? " · Start" : " · Stop";
        session.LastMessage =
            $"Sessão salva · {strategy} · meta {session.DailyProfitTarget:N2} · DD máx {session.MaxDrawdownPercent:N1}%{trailNote}{runNote}";

        await _db.SaveChangesAsync(ct);

        var mdRec = await ResolveMarketDriversRecommendationAsync(symbol, request.MarketDriversRecommendation, ct);
        var plan = await BuildPlanAsync(new BoletaStrategyContext
        {
            Symbol = symbol,
            LotSize = session.LotSize,
            ConfluenceScore = session.ScenarioConfluenceScore,
            Recommendation = session.ScenarioRecommendation,
            Bias = session.ScenarioBias,
            RiskLevel = session.RiskIndication,
            WyckoffScore = request.WyckoffScore ?? 0,
            SuggestedEntry = request.SuggestedEntry,
            SuggestedStopLoss = request.SuggestedStopLoss,
            SuggestedTakeProfit = request.SuggestedTakeProfit,
            LastPrice = request.LastPrice ?? await _mt5.GetLastPriceAsync(symbol, ct),
            ForcedDirection = session.PreferredDirection,
            MarketDriversRecommendation = mdRec,
            AllowNeutralEntries = session.AllowNeutralEntries,
            PreferDemandWithTrend = true,
            OperationalZones = BoletaStrategyContext.MapZones(request.OperationalZones)
        }, strategy, ct);

        return await MapAsync(session, plan, ct);
    }

    public async Task<IReadOnlyList<BoletaStrategyPlanDto>> PreviewPlanAsync(UpsertBoletaRequest request, CancellationToken ct = default)
    {
        var symbol = NormalizeSymbol(request.Symbol);
        var mdRec = await ResolveMarketDriversRecommendationAsync(symbol, request.MarketDriversRecommendation, ct);
        return await BuildPlanAsync(new BoletaStrategyContext
        {
            Symbol = symbol,
            LotSize = Math.Max(0.01m, request.LotSize),
            ConfluenceScore = request.ConfluenceScore ?? 50,
            Recommendation = request.Recommendation ?? "NEUTRO",
            Bias = request.Bias ?? "Sideways",
            RiskLevel = request.RiskLevel ?? "Moderado",
            WyckoffScore = request.WyckoffScore ?? 0,
            SuggestedEntry = request.SuggestedEntry,
            SuggestedStopLoss = request.SuggestedStopLoss,
            SuggestedTakeProfit = request.SuggestedTakeProfit,
            LastPrice = request.LastPrice ?? await _mt5.GetLastPriceAsync(symbol, ct),
            ForcedDirection = request.Direction,
            MarketDriversRecommendation = mdRec,
            AllowNeutralEntries = request.AllowNeutralEntries,
            PreferDemandWithTrend = true,
            OperationalZones = BoletaStrategyContext.MapZones(request.OperationalZones)
        }, NormalizeStrategy(request.Strategy), ct);
    }

    public async Task<BoletaSessionDto> ExecuteAsync(Guid tenantId, ExecuteBoletaRequest request, CancellationToken ct = default)
    {
        var symbol = NormalizeSymbol(request.Symbol);
        var session = request.SessionId.HasValue
            ? await _db.BoletaSessions.Include(s => s.Orders)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.TenantId == tenantId, ct)
            : await FindTodaySessionAsync(tenantId, symbol, ct);

        if (session is null)
            throw new InvalidOperationException("Salve a sessão de boletagem antes de executar.");

        if (session.Status is not BoletaSessionStatus.Active and not BoletaSessionStatus.Paused)
            throw new InvalidOperationException($"Sessão {session.Status} — não é possível abrir novas ordens.");

        if (session.MetaReached || session.DrawdownBreached)
            throw new InvalidOperationException("Meta ou drawdown já atingidos — feche/reinicie a sessão.");

        // Start/Stop: bloqueia apenas aberturas reais (dry-run e fechamentos continuam ok).
        if (!request.DryRun && !session.TradingEnabled)
        {
            throw new InvalidOperationException(
                "Operação em Stop — ative Start para abrir novas ordens no MT5. Fechamento de posições permanece liberado.");
        }

        var forcedDirection = BoletaStrategyContext.NormalizeDirection(request.Direction)
            ?? session.PreferredDirection;
        var lastPrice = request.LastPrice
            ?? request.EntryPrice
            ?? await _mt5.GetLastPriceAsync(session.Symbol, ct);

        var mdRec = await ResolveMarketDriversRecommendationAsync(
            session.Symbol, request.MarketDriversRecommendation, ct);

        // Trava hard: direção manual/forçada contrária aos Market Drivers.
        var forcedBlock = MarketDriversDirectionGuard.BlockReason(mdRec, forcedDirection);
        if (forcedBlock is not null)
            throw new InvalidOperationException(forcedBlock);

        var plan = await BuildPlanAsync(new BoletaStrategyContext
        {
            Symbol = session.Symbol,
            LotSize = request.Volume ?? session.LotSize,
            ConfluenceScore = request.ConfluenceScore ?? session.ScenarioConfluenceScore,
            Recommendation = request.Recommendation ?? session.ScenarioRecommendation,
            Bias = request.Bias ?? session.ScenarioBias,
            RiskLevel = request.RiskLevel ?? session.RiskIndication,
            WyckoffScore = request.WyckoffScore ?? 0,
            SuggestedEntry = request.SuggestedEntry ?? request.EntryPrice,
            SuggestedStopLoss = request.SuggestedStopLoss ?? request.StopLoss,
            SuggestedTakeProfit = request.SuggestedTakeProfit ?? request.TakeProfit,
            LastPrice = lastPrice,
            ForcedDirection = forcedDirection,
            MarketDriversRecommendation = mdRec,
            AllowNeutralEntries = request.AllowNeutralEntries ?? session.AllowNeutralEntries,
            PreferDemandWithTrend = true,
            OperationalZones = BoletaStrategyContext.MapZones(request.OperationalZones)
        }, session.Strategy, ct);

        var actionable = plan.Where(p => p.Volume > 0 && p.Direction is "Buy" or "Sell").ToList();

        // Direção manual sem plano executável (ex.: sem preço de referência):
        // ainda assim envia uma ordem a mercado com o que o operador informou.
        // ScalpShort exige SL técnico — não faz fallback silencioso sem candle.
        var isScalpShort = session.Strategy.Equals(
            BoletaStrategies.ScalpShort, StringComparison.OrdinalIgnoreCase);
        var zonesProvided = request.OperationalZones is { Count: > 0 };

        // Fallback manual só sem zonas TI (com zonas, Flat por falta de demanda/oferta prevalece).
        if (actionable.Count == 0 && forcedDirection is not null && !isScalpShort && !zonesProvided)
        {
            actionable =
            [
                new BoletaStrategyPlanDto
                {
                    Level = request.TargetLevel ?? 1,
                    Direction = forcedDirection,
                    Volume = request.Volume ?? session.LotSize,
                    Entry = request.EntryPrice ?? lastPrice,
                    StopLoss = request.StopLoss,
                    TakeProfit = request.TakeProfit,
                    Rationale = "Ordem manual a mercado"
                }
            ];
        }

        // Filtra qualquer passo contrário aos Market Drivers (defesa em profundidade).
        var blocked = actionable
            .Where(p => !MarketDriversDirectionGuard.IsAllowed(mdRec, p.Direction))
            .ToList();
        if (blocked.Count > 0)
        {
            var reason = MarketDriversDirectionGuard.BlockReason(mdRec, blocked[0].Direction)
                         ?? "Direção bloqueada por Market Drivers.";
            actionable = actionable
                .Where(p => MarketDriversDirectionGuard.IsAllowed(mdRec, p.Direction))
                .ToList();
            if (actionable.Count == 0)
                throw new InvalidOperationException(reason);
            _logger.LogWarning("Boletagem {Symbol}: {Count} passo(s) bloqueado(s) por Market Drivers ({Md})",
                session.Symbol, blocked.Count, mdRec);
        }

        if (request.TargetLevel is > 0)
            actionable = actionable.Where(p => p.Level == request.TargetLevel).ToList();

        if (actionable.Count == 0)
        {
            session.LastMessage = plan.FirstOrDefault()?.Rationale ?? "Sem setup executÃ¡vel.";
            session.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Boletagem {Symbol}: nada executÃ¡vel â€” {Reason}", session.Symbol, session.LastMessage);
            return await MapAsync(session, plan, ct);
        }

        if (request.DryRun)
        {
            session.LastMessage = $"Dry-run: {actionable.Count} ordem(ns) planejada(s), sem envio ao MT5.";
            await _db.SaveChangesAsync(ct);
            return await MapAsync(session, plan, ct);
        }

        var placed = new List<BoletaOrder>();

        foreach (var step in actionable)
        {
            var order = new BoletaOrder
            {
                SessionId = session.Id,
                TenantId = tenantId,
                Symbol = session.Symbol,
                Direction = step.Direction,
                Volume = step.Volume,
                EntryPrice = step.Entry,
                StopLoss = step.StopLoss,
                TakeProfit = step.TakeProfit,
                TargetLevel = step.Level,
                Strategy = session.Strategy,
                Status = BoletaOrderStatus.Submitted,
                Message = step.Rationale
            };
            _db.BoletaOrders.Add(order);
            session.Orders.Add(order);
            placed.Add(order);

            var result = await _mt5.SendMarketOrderAsync(
                session.Symbol,
                step.Direction,
                step.Volume,
                step.StopLoss,
                step.TakeProfit,
                $"NTBot|{session.Strategy}|L{step.Level}",
                ct);

            if (result.Success)
            {
                order.Status = result.QueuedForEa ? BoletaOrderStatus.Submitted : BoletaOrderStatus.Filled;
                order.Mt5Ticket = result.Ticket;
                order.Mt5OrderId = result.OrderId;
                order.EntryPrice = result.ExecutedPrice ?? order.EntryPrice;
                order.ExecutedAt = DateTime.UtcNow;
                order.PeakFavorablePrice = order.EntryPrice;
                order.TrailingStopPrice = order.StopLoss;
                order.Message = result.Message;
            }
            else
            {
                order.Status = BoletaOrderStatus.Failed;
                order.Message = result.Message;
                _logger.LogWarning("Ordem boletagem falhou {Symbol} {Direction} {Volume}: {Message}",
                    session.Symbol, step.Direction, step.Volume, result.Message);
            }
        }

        var submitted = placed.Count(o => o.Status != BoletaOrderStatus.Failed);
        var failed = placed.Where(o => o.Status == BoletaOrderStatus.Failed).ToList();

        session.Status = BoletaSessionStatus.Active;
        session.LastMessage = failed.Count == 0
            ? $"Executadas {submitted} ordem(ns) via MT5 ({session.Strategy})."
            : $"{submitted} enviada(s), {failed.Count} falha(s): {failed[0].Message}";
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await RefreshPnlAsync(session, ct);
        await ApplyTrailingStopsAsync(session, ct);
        await EvaluateRulesAsync(session, ct);
        await _db.SaveChangesAsync(ct);

        return await MapAsync(session, plan, ct);
    }

    public async Task<BoletaSessionDto> CloseAllAsync(Guid tenantId, string symbol, string reason, CancellationToken ct = default)
    {
        var session = await FindTodaySessionAsync(tenantId, NormalizeSymbol(symbol), ct)
            ?? throw new InvalidOperationException("SessÃ£o nÃ£o encontrada.");

        var close = await _mt5.CloseSymbolPositionsAsync(session.Symbol, ct: ct);
        foreach (var order in session.Orders.Where(o =>
                     o.Status is BoletaOrderStatus.Filled or BoletaOrderStatus.Submitted or BoletaOrderStatus.PartiallyClosed))
        {
            order.Status = BoletaOrderStatus.Closed;
            order.ClosedAt = DateTime.UtcNow;
            order.Message = reason;
        }

        session.PositionsClosedByRule = true;
        session.Status = reason.Contains("drawdown", StringComparison.OrdinalIgnoreCase)
            ? BoletaSessionStatus.ClosedDrawdown
            : reason.Contains("meta", StringComparison.OrdinalIgnoreCase)
                ? BoletaSessionStatus.ClosedMeta
                : BoletaSessionStatus.ClosedManual;

        await RefreshPnlAsync(session, ct);
        session.ResultPercentOfBalance = session.ReferenceBalance > 0
            ? Math.Round(session.SessionPnl / session.ReferenceBalance * 100m, 4)
            : 0m;
        session.LastMessage =
            $"{reason}. Resultado {session.SessionPnl:N2} ({session.ResultPercentOfBalance:N2}% do saldo). MT5: {close.Message}";
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await MapAsync(session, await BuildPlanFromSessionAsync(session, ct: ct), ct);
    }

    public async Task<BoletaSessionDto?> MonitorAsync(Guid tenantId, string symbol, CancellationToken ct = default)
    {
        var session = await FindTodaySessionAsync(tenantId, NormalizeSymbol(symbol), ct);
        if (session is null) return null;

        await RefreshPnlAsync(session, ct);
        await ApplyTrailingStopsAsync(session, ct);
        await EvaluateRulesAsync(session, ct);
        // Soft-fail: monitor polling must not 500 on DbUpdateException / Npgsql timeouts.
        await TrySaveChangesAsync(ct);
        var lastPrice = await _mt5.GetLastPriceAsync(session.Symbol, ct);
        return await MapAsync(session, await BuildPlanFromSessionAsync(session, lastPrice, ct), ct);
    }

    private async Task TrySaveChangesAsync(CancellationToken ct)
    {
        if (!_db.ChangeTracker.HasChanges())
            return;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            await _db.SaveChangesAsync(timeout.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Boleta SaveChanges soft-failed (timeout/DbUpdate); continuing without persist");
            foreach (var entry in _db.ChangeTracker.Entries().ToList())
                entry.State = EntityState.Detached;
        }
    }

    private async Task ApplyTrailingStopsAsync(BoletaSession session, CancellationToken ct)
    {
        var usePercentTrail = session.TrailingStopPercent > 0;
        var useScalpLock = string.Equals(
            session.Strategy, BoletaStrategies.ScalpShort, StringComparison.OrdinalIgnoreCase);

        if ((!usePercentTrail && !useScalpLock) || session.Status is not BoletaSessionStatus.Active)
            return;

        var positions = await _mt5.GetPositionsAsync(session.Symbol, ct);
        if (positions.Count == 0)
            return;

        var openOrders = session.Orders
            .Where(o => o.Status is BoletaOrderStatus.Filled or BoletaOrderStatus.Submitted or BoletaOrderStatus.PartiallyClosed)
            .ToList();

        var advanced = 0;
        foreach (var pos in positions)
        {
            var order = MatchOrder(openOrders, pos);
            if (order is null)
                continue;

            // Preço técnico de mercado; SL atual = max(ordem, posição MT5) no sentido de proteção.
            var currentSl = ResolveCurrentStop(order, pos);
            TrailingStopCalculator.Update? update = null;

            // ScalpShort: lock absoluto $7 → $2 tem prioridade sobre o trail %.
            if (useScalpLock)
            {
                update = TrailingStopCalculator.TryAbsoluteProfitLock(
                    pos.Direction,
                    pos.CurrentPrice,
                    order.EntryPrice ?? pos.EntryPrice,
                    order.PeakFavorablePrice,
                    currentSl);
            }

            if (update is null && usePercentTrail)
            {
                update = TrailingStopCalculator.TryAdvance(
                    pos.Direction,
                    pos.CurrentPrice,
                    session.TrailingStopPercent,
                    order.EntryPrice ?? pos.EntryPrice,
                    order.PeakFavorablePrice,
                    currentSl);
            }
            else if (update is { ShouldApply: false } && usePercentTrail)
            {
                // Peak pode ter avançado no lock sem aplicar; ainda tenta apertar via %.
                var pctUpdate = TrailingStopCalculator.TryAdvance(
                    pos.Direction,
                    pos.CurrentPrice,
                    session.TrailingStopPercent,
                    order.EntryPrice ?? pos.EntryPrice,
                    update.Value.PeakFavorablePrice,
                    currentSl);
                if (pctUpdate is not null)
                    update = pctUpdate;
            }

            if (update is null)
                continue;

            order.PeakFavorablePrice = update.Value.PeakFavorablePrice;

            if (!update.Value.ShouldApply)
            {
                // Peak pode ter avançado sem mover SL (ainda não apertou o suficiente).
                continue;
            }

            if (!long.TryParse(order.Mt5Ticket, out var ticket) || ticket <= 0)
                ticket = pos.Ticket;

            var modify = await _mt5.ModifyPositionStopAsync(
                ticket,
                session.Symbol,
                update.Value.TrailingStopPrice,
                order.TakeProfit,
                ct);

            if (!modify.Success)
            {
                order.Message = $"Trailing falhou: {modify.Message}";
                _logger.LogWarning("Trailing SL falhou {Symbol} ticket={Ticket}: {Message}",
                    session.Symbol, ticket, modify.Message);
                continue;
            }

            order.StopLoss = update.Value.TrailingStopPrice;
            order.TrailingStopPrice = update.Value.TrailingStopPrice;
            order.Message = useScalpLock && session.TrailingStopPercent <= 0
                ? $"BE Scalp +${ScalpShortRiskRules.BreakevenTriggerDistance:0}→+${ScalpShortRiskRules.BreakevenLockDistance:0} · peak {update.Value.PeakFavorablePrice:N2} · SL {update.Value.TrailingStopPrice:N2}"
                : $"Trailing {session.TrailingStopPercent:N2}% · peak {update.Value.PeakFavorablePrice:N2} · SL {update.Value.TrailingStopPrice:N2}";
            advanced++;
        }

        if (advanced > 0)
        {
            session.LastMessage = useScalpLock && session.TrailingStopPercent <= 0
                ? $"Breakeven Scalp avançou em {advanced} posição(ões) (+${ScalpShortRiskRules.BreakevenTriggerDistance:0}→+${ScalpShortRiskRules.BreakevenLockDistance:0})."
                : $"Trailing avançou em {advanced} posição(ões) · {session.TrailingStopPercent:N2}% (somente no sentido favorável).";
            session.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static BoletaOrder? MatchOrder(IReadOnlyList<BoletaOrder> orders, Mt5PositionSnapshot pos)
    {
        if (orders.Count == 0)
            return null;

        var byTicket = orders.FirstOrDefault(o =>
            !string.IsNullOrWhiteSpace(o.Mt5Ticket) &&
            o.Mt5Ticket == pos.Ticket.ToString());
        if (byTicket is not null)
            return byTicket;

        return orders.FirstOrDefault(o =>
            o.Direction.Equals(pos.Direction, StringComparison.OrdinalIgnoreCase) &&
            Math.Abs((o.EntryPrice ?? 0) - pos.EntryPrice) < 0.5m);
    }

    private static decimal? ResolveCurrentStop(BoletaOrder order, Mt5PositionSnapshot pos)
    {
        // Usa o SL mais apertado já conhecido (ordem ou MT5) para nunca afrouxar.
        var candidates = new List<decimal>();
        if (order.TrailingStopPrice is > 0) candidates.Add(order.TrailingStopPrice.Value);
        if (order.StopLoss is > 0) candidates.Add(order.StopLoss.Value);
        if (pos.StopLoss is > 0) candidates.Add(pos.StopLoss.Value);
        if (candidates.Count == 0)
            return null;

        var isBuy = order.Direction.Equals("Buy", StringComparison.OrdinalIgnoreCase);
        return isBuy ? candidates.Max() : candidates.Min();
    }

    private async Task EvaluateRulesAsync(BoletaSession session, CancellationToken ct)
    {
        if (session.Status is not BoletaSessionStatus.Active)
            return;

        var pnl = session.SessionPnl;
        string? closeReason = null;

        if (session.DailyProfitTarget > 0 && pnl >= session.DailyProfitTarget)
        {
            session.MetaReached = true;
            _logger.LogInformation("Meta diÃ¡ria atingida para {Symbol}: {Pnl} >= {Meta}",
                session.Symbol, pnl, session.DailyProfitTarget);

            if (session.AutomationEnabled)
                closeReason = "Meta do dia atingida â€” fechamento automÃ¡tico";
            else
                session.LastMessage =
                    $"Meta atingida ({pnl:N2} â‰¥ {session.DailyProfitTarget:N2}). Ative automaÃ§Ã£o ou feche manualmente.";
        }

        if (closeReason is null &&
            session.MaxDrawdownPercent > 0 &&
            session.CurrentDrawdownPercent >= session.MaxDrawdownPercent)
        {
            session.DrawdownBreached = true;
            _logger.LogWarning("Drawdown mÃ¡ximo atingido para {Symbol}: {Dd}%",
                session.Symbol, session.CurrentDrawdownPercent);

            if (session.AutomationEnabled)
                closeReason = "Drawdown mÃ¡ximo â€” fechamento automÃ¡tico";
            else
                session.LastMessage =
                    $"Drawdown {session.CurrentDrawdownPercent:N2}% â‰¥ limite {session.MaxDrawdownPercent:N2}%. Feche posiÃ§Ãµes.";
        }

        if (closeReason is not null)
        {
            var close = await _mt5.CloseSymbolPositionsAsync(session.Symbol, ct: ct);
            foreach (var order in session.Orders.Where(o =>
                         o.Status is BoletaOrderStatus.Filled or BoletaOrderStatus.Submitted or BoletaOrderStatus.PartiallyClosed))
            {
                order.Status = BoletaOrderStatus.Closed;
                order.ClosedAt = DateTime.UtcNow;
                order.Message = closeReason;
            }

            session.PositionsClosedByRule = true;
            session.Status = closeReason.Contains("Drawdown", StringComparison.OrdinalIgnoreCase)
                ? BoletaSessionStatus.ClosedDrawdown
                : BoletaSessionStatus.ClosedMeta;
            session.ResultPercentOfBalance = session.ReferenceBalance > 0
                ? Math.Round(session.SessionPnl / session.ReferenceBalance * 100m, 4)
                : 0m;
            session.LastMessage =
                $"{closeReason}. Resultado {session.SessionPnl:N2} ({session.ResultPercentOfBalance:N2}% do saldo). MT5: {close.Message}";
        }
        else if (session.AutomationEnabled)
        {
            session.ResultPercentOfBalance = session.SessionPnlPercent;
        }

        session.UpdatedAt = DateTime.UtcNow;
    }

    private async Task RefreshPnlAsync(BoletaSession session, CancellationToken ct)
    {
        var positions = await _mt5.GetPositionsAsync(session.Symbol, ct);
        session.FloatingPnl = positions.Sum(p => p.Profit);

        var equity = session.ReferenceBalance + session.RealizedPnl + session.FloatingPnl;
        if (equity > session.PeakEquity)
            session.PeakEquity = equity;

        if (session.PeakEquity > 0)
        {
            var dd = (session.PeakEquity - equity) / session.PeakEquity * 100m;
            session.CurrentDrawdownPercent = Math.Max(0, Math.Round(dd, 2));
        }

        if (session.AutomationEnabled)
            session.ResultPercentOfBalance = session.SessionPnlPercent;

        session.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<BoletaSession?> FindTodaySessionAsync(Guid tenantId, string symbol, CancellationToken ct)
    {
        var day = DateTime.UtcNow.Date;
        return await _db.BoletaSessions
            .Include(s => s.Orders)
            .Where(s => s.TenantId == tenantId && s.Symbol == symbol && s.SessionDate == day)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<IReadOnlyList<BoletaStrategyPlanDto>> BuildPlanFromSessionAsync(
        BoletaSession session,
        decimal? lastPrice = null,
        CancellationToken ct = default)
    {
        var mdRec = _marketDrivers.GetLastKnownSnapshot(session.Symbol)?.Score?.Recommendation;
        return await BuildPlanAsync(new BoletaStrategyContext
        {
            Symbol = session.Symbol,
            LotSize = session.LotSize,
            ConfluenceScore = session.ScenarioConfluenceScore,
            Recommendation = session.ScenarioRecommendation,
            Bias = session.ScenarioBias,
            RiskLevel = session.RiskIndication,
            LastPrice = lastPrice,
            ForcedDirection = session.PreferredDirection,
            MarketDriversRecommendation = string.IsNullOrWhiteSpace(mdRec) ? null : mdRec.Trim(),
            AllowNeutralEntries = session.AllowNeutralEntries,
            PreferDemandWithTrend = true
        }, session.Strategy, ct);
    }

    private async Task<IReadOnlyList<BoletaStrategyPlanDto>> BuildPlanAsync(
        BoletaStrategyContext ctx,
        string strategy,
        CancellationToken ct)
    {
        if (strategy.Equals(BoletaStrategies.ScalpShort, StringComparison.OrdinalIgnoreCase))
            await EnrichScalpShortTechnicalStopAsync(ctx, ct);

        if (!_strategies.TryGetValue(strategy, out var impl))
            impl = _strategies[BoletaStrategies.Wyckoff];
        return impl.BuildPlan(ctx);
    }

    /// <summary>
    /// Carrega o candle M5 imediatamente anterior ao da entrada (penúltimo OHLC)
    /// e o tick size (MT5 ou fallback conhecido) para o SL técnico do ScalpShort.
    /// </summary>
    private async Task EnrichScalpShortTechnicalStopAsync(BoletaStrategyContext ctx, CancellationToken ct)
    {
        var tf = SymbolTickSize.ScalpShortStopTimeframe;
        ctx.StopLossTimeframe = tf;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var result = await _candles.GetCandlesAsync(ctx.Symbol, count: 5, timeframe: tf, timeout.Token);
            var ordered = result.Candles
                .Where(c => c.High > 0 && c.Low > 0)
                .OrderBy(c => c.OpenTime)
                .ToList();

            // Último = candle da entrada (atual/formando); penúltimo = candle anterior completo.
            if (ordered.Count >= 2)
            {
                var previous = ordered[^2];
                ctx.PreviousCandleHigh = previous.High;
                ctx.PreviousCandleLow = previous.Low;
            }
            else
            {
                _logger.LogWarning(
                    "Boletagem ScalpShort {Symbol}: candles {Tf} insuficientes ({Count}) para SL técnico",
                    ctx.Symbol, tf, ordered.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Boletagem ScalpShort {Symbol}: falha ao obter candle {Tf}", ctx.Symbol, tf);
        }

        decimal? brokerTick = null;
        try
        {
            brokerTick = await _mt5.GetTickSizeAsync(ctx.Symbol, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Boletagem: tick size MT5 indisponível para {Symbol}", ctx.Symbol);
        }

        ctx.TickSize = SymbolTickSize.TryResolve(ctx.Symbol, brokerTick);
    }

    private async Task<string?> ResolveMarketDriversRecommendationAsync(
        string symbol,
        string? fromClient,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(fromClient))
            return fromClient.Trim();

        try
        {
            var last = _marketDrivers.GetLastKnownSnapshot(symbol);
            var rec = last?.Score?.Recommendation;
            if (!string.IsNullOrWhiteSpace(rec))
                return rec.Trim();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var snap = await _marketDrivers.GetSnapshotAsync(symbol, timeout.Token);
            rec = snap?.Score?.Recommendation;
            return string.IsNullOrWhiteSpace(rec) ? null : rec.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Boletagem: falha ao obter Market Drivers para {Symbol}", symbol);
            return null;
        }
    }

    private static string NormalizeSymbol(string? symbol) =>
        (symbol ?? "").Trim().ToUpperInvariant() switch
        {
            "XAU" or "GOLD" => "XAUUSD",
            var s => s
        };

    private static string NormalizeStrategy(string? strategy) =>
        Domain.Entities.BoletaStrategies.All.FirstOrDefault(s =>
            s.Equals(strategy, StringComparison.OrdinalIgnoreCase))
        ?? Domain.Entities.BoletaStrategies.Wyckoff;

    private async Task<BoletaSessionDto> MapAsync(
        BoletaSession s,
        IReadOnlyList<BoletaStrategyPlanDto> plan,
        CancellationToken ct)
    {
        var dto = Map(s, plan);
        var readiness = await _mt5.GetReadinessAsync(ct);
        dto.Mt5Ready = readiness.Ready;
        dto.Mt5Status = readiness.Message;
        return dto;
    }

    private static BoletaSessionDto Map(BoletaSession s, IReadOnlyList<BoletaStrategyPlanDto> plan) => new()
    {
        Id = s.Id,
        Symbol = s.Symbol,
        SessionDate = s.SessionDate,
        Strategy = s.Strategy,
        DailyProfitTarget = s.DailyProfitTarget,
        MaxDrawdownPercent = s.MaxDrawdownPercent,
        TrailingStopPercent = s.TrailingStopPercent,
        LotSize = s.LotSize,
        ReferenceBalance = s.ReferenceBalance,
        RealizedPnl = s.RealizedPnl,
        FloatingPnl = s.FloatingPnl,
        SessionPnl = s.SessionPnl,
        SessionPnlPercent = s.SessionPnlPercent,
        CurrentDrawdownPercent = s.CurrentDrawdownPercent,
        RiskIndication = s.RiskIndication,
        ScenarioBias = s.ScenarioBias,
        ScenarioRecommendation = s.ScenarioRecommendation,
        ScenarioConfluenceScore = s.ScenarioConfluenceScore,
        AutomationEnabled = s.AutomationEnabled,
        TradingEnabled = s.TradingEnabled,
        AllowNeutralEntries = s.AllowNeutralEntries,
        MetaReached = s.MetaReached,
        DrawdownBreached = s.DrawdownBreached,
        PositionsClosedByRule = s.PositionsClosedByRule,
        Status = s.Status,
        LastMessage = s.LastMessage,
        ResultPercentOfBalance = s.ResultPercentOfBalance,
        PlannedTargets = plan.ToList(),
        Orders = s.Orders
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new BoletaOrderDto
            {
                Id = o.Id,
                Direction = o.Direction,
                Volume = o.Volume,
                EntryPrice = o.EntryPrice,
                StopLoss = o.StopLoss,
                TakeProfit = o.TakeProfit,
                PeakFavorablePrice = o.PeakFavorablePrice,
                TrailingStopPrice = o.TrailingStopPrice,
                TargetLevel = o.TargetLevel,
                Strategy = o.Strategy,
                Status = o.Status,
                Mt5Ticket = o.Mt5Ticket,
                RealizedPnl = o.RealizedPnl,
                Message = o.Message,
                CreatedAt = o.CreatedAt,
                ExecutedAt = o.ExecutedAt,
                ClosedAt = o.ClosedAt
            }).ToList()
    };
}
