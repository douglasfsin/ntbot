namespace NtBot.Domain.Entities
{
    /// <summary>
    /// Representa um sinal de trading gerado pelo sistema
    /// </summary>
    public class TradingSignal
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        
        // Direção do sinal
        public SignalDirection Direction { get; set; }
        
        // Confiança do sinal (0-100)
        public decimal ConfidenceScore { get; set; }
        
        // Componentes do sinal
        public string WyckoffPhase { get; set; } = string.Empty; // "Accumulation", "Distribution", etc.
        public string WyckoffEvent { get; set; } = string.Empty; // "Spring", "Upthrust", "BC", etc.
        public string MacroBias { get; set; } = string.Empty; // "Bullish", "Bearish", "Neutral"
        public decimal NewsImpact { get; set; } // -1 to +1
        public bool EconomicEventActive { get; set; }
        
        // Preços
        public decimal EntryPrice { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TakeProfit { get; set; }
        public decimal RiskRewardRatio { get; set; }
        
        // Tamanho da posição
        public int Quantity { get; set; }
        public decimal RiskAmount { get; set; } // Em $
        
        // Status
        public SignalStatus Status { get; set; }
        public string? RejectionReason { get; set; }
        
        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExecutedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        
        // Relacionamentos
        public Guid? TradeId { get; set; }
        public Trade? Trade { get; set; }
    }
    
    public enum SignalDirection
    {
        LONG,
        SHORT,
        NEUTRAL
    }
    
    public enum SignalStatus
    {
        PENDING,       // Aguardando execução
        EXECUTED,      // Executado
        CANCELLED,     // Cancelado
        REJECTED,      // Rejeitado por filtros
        EXPIRED        // Expirado (timeout)
    }
}
