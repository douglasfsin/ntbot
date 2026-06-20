namespace NtBot.Domain.Entities
{
    /// <summary>
    /// Configuração de um ativo para um tenant específico
    /// </summary>
    public class AssetConfiguration
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        
        public string Symbol { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        
        // Configurações de risco
        public int MaxPositionSize { get; set; } = 1;
        public decimal RiskPerTrade { get; set; } = 1.0m; // %
        public decimal MaxDailyLoss { get; set; } = 5.0m; // %
        
        // Timeframes ativos
        public string Timeframes { get; set; } = "[\"1m\",\"5m\",\"15m\"]"; // JSON array
        
        // Parâmetros da estratégia
        public decimal MinConfidenceScore { get; set; } = 70.0m;
        public decimal MinRiskReward { get; set; } = 2.0m;
        public bool EnableWyckoff { get; set; } = true;
        public bool EnableMacroFilter { get; set; } = true;
        public bool EnableNewsFilter { get; set; } = true;
        public bool EnableEconomicCalendar { get; set; } = true;
        
        // Horários de operação (UTC)
        public TimeSpan? TradingStartTime { get; set; }
        public TimeSpan? TradingEndTime { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
