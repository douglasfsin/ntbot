using NtBot.Api.Services.Interfaces;
using NtBot.Domain.Entities;

namespace NtBot.Api.Services;

public class TradingService : ITradingService
{
    private readonly IRiskManager _riskManager;
    private readonly ILogger<TradingService> _logger;

    public TradingService(IRiskManager riskManager, ILogger<TradingService> logger)
    {
        _riskManager = riskManager;
        _logger = logger;
    }

    public async Task<AccountInfo> GetAccountInfoAsync(Guid tenantId)
    {
        return await Task.FromResult(new AccountInfo
        {
            Balance = 10000,
            Equity = 9950,
            Margin = 500,
            FreeMargin = 9500,
            DailyProfit = 100,
            DailyLoss = 150
        });
    }

    public async Task<IEnumerable<PositionInfo>> GetAllPositionsAsync(Guid tenantId)
    {
        return await Task.FromResult<IEnumerable<PositionInfo>>(new List<PositionInfo>
        {
            new()
            {
                Symbol = "EURUSD",
                Direction = TradeDirection.LONG,
                Volume = 0.1m,
                EntryPrice = 1.0850m,
                CurrentPrice = 1.0875m,
                ProfitLoss = 25,
                ProfitLossPercent = 2.31m
            }
        });
    }

    public async Task<OrderResult> ExecuteOrderAsync(OrderRequest request)
    {
        if (request.TenantId == Guid.Empty)
        {
            return new OrderResult
            {
                Success = false,
                Message = "TenantId é obrigatório para execução de ordens"
            };
        }

        var validation = await _riskManager.ValidateOrderAsync(request.TenantId, request);
        if (!validation.IsValid)
        {
            _logger.LogWarning(
                "Ordem rejeitada para {Symbol}: {Reason}",
                request.Symbol,
                validation.Reason);

            return new OrderResult
            {
                Success = false,
                Message = validation.Reason ?? "Ordem rejeitada pelo risk manager"
            };
        }

        return await Task.FromResult(new OrderResult
        {
            Success = true,
            OrderId = Guid.NewGuid().ToString(),
            Message = "Order placed successfully",
            ExecutedPrice = request.Price,
            ExecutedVolume = request.Quantity
        });
    }

    public async Task<OrderResult> ClosePositionAsync(string symbol, decimal volume)
    {
        return await Task.FromResult(new OrderResult
        {
            Success = true,
            OrderId = Guid.NewGuid().ToString(),
            Message = "Position closed successfully"
        });
    }

    public async Task<PositionInfo> GetPositionAsync(string symbol)
    {
        return await Task.FromResult(new PositionInfo
        {
            Symbol = symbol,
            Direction = TradeDirection.LONG,
            Volume = 0.1m,
            EntryPrice = 1.0850m,
            CurrentPrice = 1.0875m,
            ProfitLoss = 25,
            ProfitLossPercent = 2.31m
        });
    }
}
