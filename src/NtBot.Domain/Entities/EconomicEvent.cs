namespace NtBot.Domain.Entities
{
    /// <summary>
    /// Evento econômico (FOMC, CPI, NFP, etc.)
    /// </summary>
    public class EconomicEvent
    {
        public Guid Id { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        
        // Impacto esperado
        public EventImpact Impact { get; set; }
        
        // Horário do evento
        public DateTime EventTime { get; set; }
        
        // Dados do evento
        public string? Actual { get; set; }
        public string? Forecast { get; set; }
        public string? Previous { get; set; }
        
        // Janela de bloqueio (em minutos)
        public int BlockBeforeMinutes { get; set; } = 30;
        public int BlockAfterMinutes { get; set; } = 15;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
    
    public enum EventImpact
    {
        LOW,
        MEDIUM,
        HIGH
    }
}
