namespace Marketdata.Database.Models;

public record HistoricalTradeDto(
    string Ticker, 
    string Side, 
    string BuyAgent, 
    string SellAgent, 
    double Price, 
    long Quantity, 
    long TradeType, 
    long TradeNumber, 
    bool IsAuction, 
    DateTime Timestamp
);
