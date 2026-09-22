using MarketData.API.Configuration;
using MarketData.API.Data.Model;
using System.Collections.Concurrent;

namespace MarketData.API.Data
{
    public static class MarketCache
    {
        private static readonly ConcurrentDictionary<string, PriceTick> _data = new();

        public static void UpdatePrice(string ticker, double price)
        {
            if (!IsValidNumber(price)) return;

            _data.AddOrUpdate(ticker,
                _ => new PriceTick
                {
                    Ticker = ticker,
                    LastPrice = price,
                    Timestamp = DateTime.UtcNow
                },
                (_, existing) =>
                {
                    existing.LastPrice = price;
                    existing.Timestamp = DateTime.UtcNow;
                    return existing;
                });
        }

        public static void UpdateBook(string ticker, bool isBid, double price, double qty, double qty5)
        {
            var newTick = new PriceTick { Ticker = ticker, Timestamp = DateTime.UtcNow };
            if (isBid)
            {
                if (IsPositiveNumber(price)) newTick.Bid = price;
                if (IsPositiveNumber(qty)) newTick.BidQty = qty;
                if (IsPositiveNumber(qty5)) newTick.BidQty5 = qty5;
            }
            else
            {
                if (IsPositiveNumber(price)) newTick.Ask = price;
                if (IsPositiveNumber(qty)) newTick.AskQty = qty;
                if (IsPositiveNumber(qty5)) newTick.AskQty5 = qty5;
            }

            _data.AddOrUpdate(ticker,
             newTick,
                 (_, existing) =>
                 {
                     if (isBid)
                     {
                         if (IsPositiveNumber(price)) existing.Bid = price;
                         if (IsPositiveNumber(qty)) existing.BidQty = qty;
                         if (IsPositiveNumber(qty5)) existing.BidQty5 = qty5;
                     }
                     else
                     {
                         if (IsPositiveNumber(price)) existing.Ask = price;
                         if (IsPositiveNumber(qty)) existing.AskQty = qty;
                         if (IsPositiveNumber(qty5)) existing.AskQty5 = qty5;
                     }

                     existing.Timestamp = DateTime.UtcNow;
                     return existing;
                 });
        }

        public static void UpdateBidDepth(string ticker, double qtyN)
        {
            _data.AddOrUpdate(ticker,
                _ => new PriceTick { Ticker = ticker, BidQtyN = qtyN, Timestamp = DateTime.UtcNow },
                (_, existing) =>
                {
                    existing.BidQtyN = qtyN;
                    existing.Timestamp = DateTime.UtcNow;
                    return existing;
                });
            //System.Diagnostics.Debug.WriteLine($"UpdateBidDepth: {qtyN}");
        }

        public static void UpdateAskDepth(string ticker, double qtyN)
        {
            _data.AddOrUpdate(ticker,
                _ => new PriceTick { Ticker = ticker, AskQtyN = qtyN, Timestamp = DateTime.UtcNow },
                (_, existing) =>
                {
                    existing.AskQtyN = qtyN;
                    existing.Timestamp = DateTime.UtcNow;
                    return existing;
                });

            //System.Diagnostics.Debug.WriteLine($"UpdateAskDepth: {qtyN}");
        }

        public static void UpdateBidFullDepth(string ticker, double qtyFull)
        {
            _data.AddOrUpdate(ticker,
                _ => new PriceTick { Ticker = ticker, BidFullQty = qtyFull, Timestamp = DateTime.UtcNow },
                (_, existing) =>
                {
                    existing.BidFullQty = qtyFull;
                    existing.Timestamp = DateTime.UtcNow;
                    return existing;
                });
        }

        public static void UpdateAskFullDepth(string ticker, double qtyFull)
        {
            _data.AddOrUpdate(ticker,
                _ => new PriceTick { Ticker = ticker, AskFullQty = qtyFull, Timestamp = DateTime.UtcNow },
                (_, existing) =>
                {
                    existing.AskFullQty = qtyFull;
                    existing.Timestamp = DateTime.UtcNow;
                    return existing;
                });
        }

        public static PriceTick? Get(string ticker)
        {
            return _data.TryGetValue(ticker, out var val) ? val : null;
        }

        /// <summary>
        /// Atualiza os dados de leilão (preço teórico) de um ativo.
        /// Quando IsAuction=true, o snapshot publicado via SignalR incluirá os campos de leilão.
        /// Passar price=0 e qty=0 encerra o estado de leilão.
        /// </summary>
        public static void UpdateAuction(string ticker, double price, long qty, int state = 4)
        {
            if (!IsValidNumber(price)) price = 0;

            _data.AddOrUpdate(ticker,
                _ => new PriceTick
                {
                    Ticker = ticker,
                    IsAuction = price > 0,
                    TheoreticalPrice = price,
                    TheoreticalQty = qty,
                    AuctionState = state,
                    Timestamp = DateTime.UtcNow
                },
                (_, existing) =>
                {
                    existing.IsAuction = price > 0;
                    existing.TheoreticalPrice = price;
                    existing.TheoreticalQty = qty;
                    existing.AuctionState = state;
                    existing.Timestamp = DateTime.UtcNow;
                    return existing;
                });
        }

        private static bool IsValidNumber(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsPositiveNumber(double value)
        {
            return value > 0 && IsValidNumber(value);
        }

    }
}
