using ProfitDLLClient;

namespace MarketData.API.Worker;

/// <summary>
/// Evento publicado no Channel pelo MarketDataWorker.
/// Substitui PriceTick como tipo do Channel interno.
/// </summary>
public record MarketEvent(
    string Ticker,
    double Price,
    TConnectorBookSideType Side,
    long Quantity,
    DateTime Timestamp
);