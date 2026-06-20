namespace NtBot.Domain.Entities
{
    /// <summary>
    /// Representa uma operação (trade) executada
    /// </summary>
    public class Trade
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid? SignalId { get; set; }
        public TradingSignal? Signal { get; set; }
        
        public string Symbol { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty; // ID da ordem no NinjaTrader
        
        // Direção
        public TradeDirection Direction { get; set; }
        
        // Preços
        public decimal EntryPrice { get; set; }
        public decimal? ExitPrice { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TakeProfit { get; set; }
        public decimal? CurrentStopLoss { get; set; } // Para trailing stop
        
        // Quantidade
        public int Quantity { get; set; }
        
        // Resultado
        public decimal? PnL { get; set; } // Profit and Loss
        public decimal? PnLPercent { get; set; }
        public decimal? Commission { get; set; }
        public decimal? NetPnL { get; set; } // PnL - Commission
        
        // Timestamps
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public int? Duration { get; set; } // Em segundos
        
        // Status
        public TradeStatus Status { get; set; }
        public string? ExitReason { get; set; } // "Take Profit", "Stop Loss", "Manual", etc.
        
        // Métricas adicionais
        public decimal? MAE { get; set; } // Maximum Adverse Excursion
        public decimal? MFE { get; set; } // Maximum Favorable Excursion
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
    
    public enum TradeDirection
    {
        LONG,
        SHORT
    }
    
    public enum TradeStatus
    {
        OPEN,           // Posição aberta
        CLOSED,         // Posição fechada
        PENDING,        // Ordem enviada mas não preenchida
        CANCELLED,      // Ordem cancelada
        REJECTED        // Ordem rejeitada pelo broker
    }
}
