using NtBot.Domain.Entities;

namespace NtBot.Api.Services.NinjaTrader
{
    /// <summary>
    /// Interface para comunicação com NinjaTrader
    /// </summary>
    public interface INinjaTraderService
    {
        // Conexão
        Task<bool> ConnectAsync(string apiKey, string accountId);
        Task DisconnectAsync();
        bool IsConnected { get; }
        
        // Market Data
        Task<Candle?> GetLatestCandleAsync(string symbol, string timeframe);
        Task<List<Candle>> GetHistoricalCandlesAsync(string symbol, string timeframe, DateTime from, DateTime to);
        
        // Order Execution
        Task<string> PlaceMarketOrderAsync(string symbol, TradeDirection direction, int quantity);
        Task<string> PlaceLimitOrderAsync(string symbol, TradeDirection direction, int quantity, decimal price);
        Task<string> PlaceStopOrderAsync(string symbol, TradeDirection direction, int quantity, decimal stopPrice);
        Task<bool> CancelOrderAsync(string orderNumber);
        Task<bool> ModifyOrderAsync(string orderNumber, decimal? newStopLoss, decimal? newTakeProfit);
        
        // Position Management
        Task<List<Position>> GetOpenPositionsAsync(string? symbol = null);
        Task<bool> ClosePositionAsync(string orderNumber);
        Task<bool> CloseAllPositionsAsync(string symbol);
        
        // Account Info
        Task<decimal> GetAccountBalanceAsync();
        Task<decimal> GetBuyingPowerAsync();
        
        // Real-time data subscription
        Task SubscribeToMarketDataAsync(string symbol, Action<Candle> onCandleReceived);
        Task UnsubscribeFromMarketDataAsync(string symbol);
        
        // Events
        event EventHandler<string>? ConnectionLost;
        event EventHandler<string>? OrderFilled;
        event EventHandler<string>? OrderCancelled;
        event EventHandler<Trade>? PositionOpened;
        event EventHandler<Trade>? PositionClosed;
    }
}
