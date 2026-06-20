namespace NtBot.Domain.Entities
{
    /// <summary>
    /// Configuração de um tenant (cliente SaaS)
    /// </summary>
    public class Tenant
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        
        // Plano de assinatura
        public SubscriptionPlan Plan { get; set; }
        public DateTime? SubscriptionStart { get; set; }
        public DateTime? SubscriptionEnd { get; set; }
        
        // Status
        public bool IsActive { get; set; } = true;
        public bool IsTrial { get; set; } = false;
        
        // Configurações
        public string? NinjaTraderApiKey { get; set; }
        public string? NinjaTraderAccountId { get; set; }
        public string? StripeCustomerId { get; set; }
        
        // Relacionamentos billing
        public Subscription? Subscription { get; set; }
        public int MaxActivePositions { get; set; } = 1;
        public int MaxDailyTrades { get; set; } = 10;
        public decimal MaxRiskPerTrade { get; set; } = 2.0m; // %
        public decimal MaxDailyLoss { get; set; } = 1000m;
        public decimal MaxDrawdownPercent { get; set; } = 5.0m;
        
        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        
        // Relacionamentos
        public List<User> Users { get; set; } = new();
        public List<AssetConfiguration> AssetConfigurations { get; set; } = new();
        public List<TradingSignal> Signals { get; set; } = new();
        public List<Trade> Trades { get; set; } = new();
    }
    
    public enum SubscriptionPlan
    {
        FREE,           // 1 ativo, backtesting básico
        PRO,            // 3 ativos, backtesting avançado, alertas
        ENTERPRISE      // Ilimitado, API access, suporte prioritário
    }
}
