namespace NtBot.Domain.Entities
{
    /// <summary>
    /// Representa um candle (OHLCV) com dados adicionais de order flow
    /// </summary>
    public class Candle
    {
        public Guid Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Timeframe { get; set; } = string.Empty; // "1m", "5m", "15m", "1h", "1d"
        public DateTime OpenTime { get; set; }
        public DateTime CloseTime { get; set; }
        
        // OHLCV
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
        
        // Order Flow Data (opcional, se disponível)
        public long? Delta { get; set; } // Buy Volume - Sell Volume
        public long? BuyVolume { get; set; }
        public long? SellVolume { get; set; }
        public decimal? VWAP { get; set; }
        public decimal? POC { get; set; } // Point of Control (volume profile)
        
        // Indicadores técnicos pré-calculados
        public decimal? ATR { get; set; }
        public decimal? RSI { get; set; }
        public decimal? EMA20 { get; set; }
        public decimal? EMA50 { get; set; }
        public decimal? EMA200 { get; set; }
        
        // Compatibilidade com código anterior
        public DateTime Time 
        { 
            get => OpenTime; 
            set => OpenTime = value; 
        }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
