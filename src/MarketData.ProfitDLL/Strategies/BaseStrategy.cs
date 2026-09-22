namespace ProfitDLLClient.Strategies;

/// <summary>
/// Classe base para implementação de estratégias
/// </summary>
public abstract class BaseStrategy : IStrategy
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public bool IsActive { get; set; }

    protected PositionManager PositionManager { get; }
    protected Dictionary<string, double> LastPrices { get; } = new();

    protected BaseStrategy(PositionManager positionManager)
    {
        PositionManager = positionManager ?? throw new ArgumentNullException(nameof(positionManager));
    }

    public virtual void Initialize()
    {
        IsActive = true;
        Log($"Estratégia {Name} inicializada");
    }

    public virtual void OnTrade(string ticker, string exchange, TConnectorTrade trade)
    {
        if (!IsActive) return;

        var key = $"{ticker}:{exchange}";
        LastPrices[key] = trade.Price;

        // Atualiza preços das posições
        PositionManager.UpdatePrice(ticker, exchange, trade.Price);
    }

    public virtual void OnOfferBookUpdate(string ticker, string exchange, int side, double price, int quantity)
    {
        if (!IsActive) return;
    }

    public virtual void OnPositionUpdate(TConnectorAccountIdentifier accountId, TConnectorAssetIdentifier assetId)
    {
        if (!IsActive) return;
    }

    public virtual void OnOrderUpdate(TConnectorOrder order)
    {
        if (!IsActive) return;
    }

    public virtual void Stop()
    {
        IsActive = false;
        Log($"Estratégia {Name} parada");
    }

    protected void Log(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{Name}] {message}");
    }

    protected double GetLastPrice(string ticker, string exchange)
    {
        var key = $"{ticker}:{exchange}";
        return LastPrices.TryGetValue(key, out var price) ? price : 0;
    }
}
