using ProfitDLLClient.Models;

namespace ProfitDLLClient.Core;

/// <summary>
/// Armazena dados globais da aplicação
/// </summary>
public static class DataStore
{
    // Locks para sincronização
    private static readonly object TradeLock = new();
    private static readonly object HistLock = new();

    // Filas de trades
    public static Queue<Trade> Traders { get; } = new();
    public static Queue<Trade> HistTraders { get; } = new();

    // Listas de preços e ofertas
    public static List<TGroupPrice> PriceSell { get; } = new();
    public static List<TGroupPrice> PriceBuy { get; } = new();
    public static List<TConnectorOffer> OfferSell { get; } = new();
    public static List<TConnectorOffer> OfferBuy { get; } = new();

    // Estado
    public static bool IsActive { get; set; }
    public static bool IsMarketConnected { get; set; }

    /// <summary>
    /// Adiciona trade à fila de forma thread-safe
    /// </summary>
    public static void AddTrade(Trade trade)
    {
        lock (TradeLock)
        {
            Traders.Enqueue(trade);
        }
    }

    /// <summary>
    /// Adiciona trade histórico à fila de forma thread-safe
    /// </summary>
    public static void AddHistoricalTrade(Trade trade)
    {
        lock (HistLock)
        {
            HistTraders.Enqueue(trade);
        }
    }

    /// <summary>
    /// Obtém trades de forma thread-safe
    /// </summary>
    public static List<Trade> GetTrades()
    {
        lock (TradeLock)
        {
            return Traders.ToList();
        }
    }

    /// <summary>
    /// Limpa todos os dados
    /// </summary>
    public static void Clear()
    {
        lock (TradeLock)
        {
            Traders.Clear();
        }

        lock (HistLock)
        {
            HistTraders.Clear();
        }

        PriceSell.Clear();
        PriceBuy.Clear();
        OfferSell.Clear();
        OfferBuy.Clear();
    }
}
