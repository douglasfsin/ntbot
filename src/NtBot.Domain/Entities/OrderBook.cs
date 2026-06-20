using System.ComponentModel.DataAnnotations;

namespace NtBot.Domain.Entities;

public class OrderBook
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(20)]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string Source { get; set; } = "MT5"; // MT5, NinjaTrader, etc.

    [Required]
    public DateTime Timestamp { get; set; }

    // Levels (both bid and ask). Use OrderBookLevel.IsBid to distinguish sides.
    public List<OrderBookLevel> Levels { get; set; } = new();

    public decimal BestBid => Levels.Any(l => l.IsBid) ? Levels.Where(l => l.IsBid).Max(b => b.Price) : 0;
    public decimal BestAsk => Levels.Any(l => !l.IsBid) ? Levels.Where(l => !l.IsBid).Min(a => a.Price) : 0;
    public int Spread => Levels.Any(l => !l.IsBid) && Levels.Any(l => l.IsBid) ? (int)((BestAsk - BestBid) * 100000) : 0; // For 5-digit brokers
}

public class OrderBookLevel
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderBookId { get; set; }

    // true = bid, false = ask
    public bool IsBid { get; set; } = true;

    [Required]
    public decimal Price { get; set; }

    [Required]
    public decimal Volume { get; set; }

    public int OrdersCount { get; set; }

    // Navigation
    public OrderBook? OrderBook { get; set; }
}