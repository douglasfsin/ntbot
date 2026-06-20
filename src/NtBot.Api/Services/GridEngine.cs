using NtBot.Domain.Entities;
using NtBot.Api.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NtBot.Api.Services;

public class GridEngine
{
    private readonly ITenantService _tenantService;
    private readonly ITradingService _tradingService;
    private readonly IRiskManager _riskManager;

    public GridEngine(ITenantService tenantService, ITradingService tradingService, IRiskManager riskManager)
    {
        _tenantService = tenantService;
        _tradingService = tradingService;
        _riskManager = riskManager;
    }

    public async Task<GridOrder> CreateGridOrderAsync(Guid tenantId, CreateGridOrderRequest request)
    {
        var tenant = await _tenantService.GetTenantAsync(tenantId);
        if (tenant == null || !tenant.IsActive)
            throw new InvalidOperationException("Invalid or inactive tenant");

        var riskCheck = await _riskManager.ValidateGridOrderAsync(tenantId, request);
        if (!riskCheck.IsValid)
            throw new InvalidOperationException($"Risk validation failed: {riskCheck.Reason}");

        var gridOrder = new GridOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Symbol = request.Symbol,
            BasePrice = request.BasePrice,
            StepSize = request.StepSize,
            MaxLevels = request.MaxLevels,
            LotSize = request.LotSize,
            UseMartingale = request.UseMartingale,
            MartingaleMultiplier = request.UseMartingale ? request.MartingaleMultiplier : 1.0m,
            ProfitTarget = request.ProfitTarget.GetValueOrDefault(0),
            StopLossAmount = request.StopLossAmount.GetValueOrDefault(0),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Levels = new List<GridLevel>()
        };

        await GenerateGridLevelsAsync(gridOrder, request.Direction);
        await SaveGridOrderAsync(gridOrder);

        if (gridOrder.IsActive)
        {
            _ = Task.Run(() => MonitorGridOrderAsync(gridOrder.Id));
        }

        return gridOrder;
    }

    public async Task UpdateGridOrderAsync(Guid gridOrderId, UpdateGridOrderRequest request)
    {
        var gridOrder = await GetGridOrderAsync(gridOrderId);
        if (gridOrder == null)
            throw new InvalidOperationException("Grid order not found");

        if (request.IsActive.HasValue)
            gridOrder.IsActive = request.IsActive.Value;

        if (request.MaxLevels.HasValue)
            gridOrder.MaxLevels = request.MaxLevels.Value;

        if (request.LotSize.HasValue)
            gridOrder.LotSize = request.LotSize.Value;

        await UpdateGridOrderInDbAsync(gridOrder);
    }

    public async Task DeleteGridOrderAsync(Guid gridOrderId)
    {
        var gridOrder = await GetGridOrderAsync(gridOrderId);
        if (gridOrder == null)
            return;

        await CloseAllGridPositionsAsync(gridOrder);
        await DeleteGridOrderFromDbAsync(gridOrderId);
    }

    public async Task<IEnumerable<GridOrder>> GetActiveGridOrdersAsync(Guid tenantId)
    {
        return await GetActiveGridOrdersFromDbAsync(tenantId);
    }

    public async Task<GridOrder?> GetGridOrderAsync(Guid gridOrderId)
    {
        return await GetGridOrderFromDbAsync(gridOrderId);
    }

    private async Task GenerateGridLevelsAsync(GridOrder gridOrder, GridDirection direction)
    {
        var levels = new List<GridLevel>();

        for (int i = 1; i <= gridOrder.MaxLevels; i++)
        {
            if (direction == GridDirection.Both || direction == GridDirection.Buy)
            {
                var buyPrice = gridOrder.BasePrice - (i * gridOrder.StepSize);
                var buyLotSize = gridOrder.UseMartingale
                    ? gridOrder.LotSize * Convert.ToDecimal(Math.Pow((double)gridOrder.MartingaleMultiplier, i - 1))
                    : gridOrder.LotSize;

                levels.Add(new GridLevel
                {
                    Id = Guid.NewGuid(),
                    GridOrderId = gridOrder.Id,
                    Level = -i,
                    Price = buyPrice,
                    Volume = buyLotSize,
                    Direction = TradeDirection.LONG,
                    IsFilled = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (direction == GridDirection.Both || direction == GridDirection.Sell)
            {
                var sellPrice = gridOrder.BasePrice + (i * gridOrder.StepSize);
                var sellLotSize = gridOrder.UseMartingale
                    ? gridOrder.LotSize * Convert.ToDecimal(Math.Pow((double)gridOrder.MartingaleMultiplier, i - 1))
                    : gridOrder.LotSize;

                levels.Add(new GridLevel
                {
                    Id = Guid.NewGuid(),
                    GridOrderId = gridOrder.Id,
                    Level = i,
                    Price = sellPrice,
                    Volume = sellLotSize,
                    Direction = TradeDirection.SHORT,
                    IsFilled = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        gridOrder.Levels = levels;
    }

    private async Task MonitorGridOrderAsync(Guid gridOrderId)
    {
        while (true)
        {
            try
            {
                var gridOrder = await GetGridOrderAsync(gridOrderId);
                if (gridOrder == null || !gridOrder.IsActive)
                    break;

                var currentPrice = await GetCurrentPriceAsync(gridOrder.Symbol);

                var triggeredLevels = gridOrder.Levels
                    .Where(l => !l.IsFilled && ShouldTriggerLevel(l, currentPrice))
                    .ToList();

                foreach (var level in triggeredLevels)
                {
                    await ExecuteGridLevelAsync(level);
                }

                await CheckGridExitConditionsAsync(gridOrder, currentPrice);
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error monitoring grid order {gridOrderId}: {ex.Message}");
                await Task.Delay(5000);
            }
        }
    }

    private bool ShouldTriggerLevel(GridLevel level, decimal currentPrice)
    {
        return level.Direction == TradeDirection.LONG
            ? currentPrice <= level.Price
            : currentPrice >= level.Price;
    }

    private async Task ExecuteGridLevelAsync(GridLevel level)
    {
        try
        {
            var symbol = level.GridOrder?.Symbol ?? string.Empty;
            var tenantId = level.GridOrder?.TenantId ?? Guid.Empty;
            var execution = new TradeExecution
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Symbol = symbol,
                Type = level.Direction == TradeDirection.LONG ? ExecutionType.MarketBuy : ExecutionType.MarketSell,
                Volume = level.Volume,
                Price = level.Price,
                ExecutionTime = DateTime.UtcNow,
                OrderId = level.OrderId
            };

            var tradeResult = await _tradingService.ExecuteOrderAsync(new OrderRequest
            {
                Symbol = symbol,
                Direction = level.Direction,
                Quantity = level.Volume,
                Price = level.Price,
                Type = OrderType.Market,
                TenantId = tenantId
            });

            if (tradeResult.Success)
            {
                level.IsFilled = true;
                level.FilledAt = DateTime.UtcNow;
                level.TradeExecutionId = execution.Id;
                await UpdateGridLevelAsync(level);
                await SaveTradeExecutionAsync(execution);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing grid level {level.Id}: {ex.Message}");
        }
    }

    private async Task CheckGridExitConditionsAsync(GridOrder gridOrder, decimal currentPrice)
    {
        var totalProfit = gridOrder.Levels
            .Where(l => l.IsFilled)
            .Sum(l => CalculateLevelProfit(l, currentPrice));

        if (gridOrder.ProfitTarget > 0 && totalProfit >= gridOrder.ProfitTarget)
        {
            await CloseGridOrderAsync(gridOrder.Id, "Profit target reached");
            return;
        }

        if (gridOrder.StopLossAmount > 0 && totalProfit <= -gridOrder.StopLossAmount)
        {
            await CloseGridOrderAsync(gridOrder.Id, "Stop loss triggered");
            return;
        }

        var filledLevels = gridOrder.Levels.Count(l => l.IsFilled);
        if (filledLevels >= gridOrder.MaxLevels * 2)
        {
            await CloseGridOrderAsync(gridOrder.Id, "Max levels reached");
        }
    }

    private decimal CalculateLevelProfit(GridLevel level, decimal currentPrice)
    {
        if (!level.IsFilled)
            return 0;

        return level.Direction == TradeDirection.LONG
            ? (currentPrice - level.Price) * level.Volume
            : (level.Price - currentPrice) * level.Volume;
    }

    private async Task CloseGridOrderAsync(Guid gridOrderId, string reason)
    {
        var gridOrder = await GetGridOrderAsync(gridOrderId);
        if (gridOrder == null)
            return;

        gridOrder.IsActive = false;
        gridOrder.IsClosed = true;
        gridOrder.ClosedAt = DateTime.UtcNow;
        gridOrder.CloseReason = reason;

        await CloseAllGridPositionsAsync(gridOrder);
        await UpdateGridOrderInDbAsync(gridOrder);
    }

    private async Task CloseAllGridPositionsAsync(GridOrder gridOrder)
    {
        var openLevels = gridOrder.Levels.Where(l => l.IsFilled && !l.IsClosed).ToList();

        foreach (var level in openLevels)
        {
            try
            {
                await _tradingService.ClosePositionAsync(gridOrder.Symbol, level.Volume);
                level.IsClosed = true;
                level.ClosedAt = DateTime.UtcNow;
                await UpdateGridLevelAsync(level);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing grid level {level.Id}: {ex.Message}");
            }
        }
    }

    private async Task SaveGridOrderAsync(GridOrder gridOrder) { await Task.CompletedTask; }
    private async Task UpdateGridOrderInDbAsync(GridOrder gridOrder) { await Task.CompletedTask; }
    private async Task DeleteGridOrderFromDbAsync(Guid gridOrderId) { await Task.CompletedTask; }
    private async Task<IEnumerable<GridOrder>> GetActiveGridOrdersFromDbAsync(Guid tenantId) { return await Task.FromResult<IEnumerable<GridOrder>>(new List<GridOrder>()); }
    private async Task<GridOrder?> GetGridOrderFromDbAsync(Guid gridOrderId) { return await Task.FromResult<GridOrder?>(null); }
    private async Task UpdateGridLevelAsync(GridLevel level) { await Task.CompletedTask; }
    private async Task SaveTradeExecutionAsync(TradeExecution execution) { await Task.CompletedTask; }
    private async Task<decimal> GetCurrentPriceAsync(string symbol) { return await Task.FromResult(0m); }
}

public class CreateGridOrderRequest
{
    public string Symbol { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal StepSize { get; set; }
    public int MaxLevels { get; set; }
    public decimal LotSize { get; set; }
    public bool UseMartingale { get; set; }
    public decimal MartingaleMultiplier { get; set; } = 2.0m;
    public GridDirection Direction { get; set; } = GridDirection.Both;
    public decimal? ProfitTarget { get; set; }
    public decimal? StopLossAmount { get; set; }
}

public class UpdateGridOrderRequest
{
    public bool? IsActive { get; set; }
    public int? MaxLevels { get; set; }
    public decimal? LotSize { get; set; }
}

public enum GridDirection
{
    Buy,
    Sell,
    Both
}
