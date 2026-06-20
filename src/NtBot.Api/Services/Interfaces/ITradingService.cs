using NtBot.Domain.Entities;
using System.Threading.Tasks;

namespace NtBot.Api.Services.Interfaces;

public interface ITradingService
{
    Task<OrderResult> ExecuteOrderAsync(OrderRequest request);
    Task<OrderResult> ClosePositionAsync(string symbol, decimal volume);
    Task<PositionInfo> GetPositionAsync(string symbol);
    Task<IEnumerable<PositionInfo>> GetAllPositionsAsync(Guid tenantId);
    Task<AccountInfo> GetAccountInfoAsync(Guid tenantId);
}

public class OrderRequest
{
    public string Symbol { get; set; }
    public TradeDirection Direction { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
    public OrderType Type { get; set; }
    public Guid TenantId { get; set; }
}

public class OrderResult
{
    public bool Success { get; set; }
    public string OrderId { get; set; }
    public string Message { get; set; }
    public decimal? ExecutedPrice { get; set; }
    public decimal? ExecutedVolume { get; set; }
}

public class PositionInfo
{
    public string Symbol { get; set; }
    public TradeDirection Direction { get; set; }
    public decimal Volume { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal ProfitLoss { get; set; }
    public decimal ProfitLossPercent { get; set; }
}

public enum OrderType
{
    Market,
    Limit,
    Stop,
    StopLimit
}