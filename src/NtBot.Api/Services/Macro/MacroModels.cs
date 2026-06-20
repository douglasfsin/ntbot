namespace NtBot.Api.Services.Macro
{
    /// <summary>
    /// Resultado da análise de contexto macro
    /// </summary>
    public class MacroContextResult
    {
        public DateTime AnalysisTime { get; set; }
        public MacroBias Bias { get; set; }
        public RiskMode RiskMode { get; set; }
        public decimal ConfidenceScore { get; set; }
        
        // Correlações
        public Dictionary<string, decimal> Correlations { get; set; } = new();
        
        // Regime de volatilidade
        public VolatilityRegime VolatilityRegime { get; set; }
        public decimal VIXLevel { get; set; }
        
        // Market internals
        public decimal? AdvanceDeclineRatio { get; set; }
        public bool IsRiskOn { get; set; }
        
        public List<string> Observations { get; set; } = new();
    }
    
    public enum MacroBias
    {
        BULLISH,
        BEARISH,
        NEUTRAL
    }
    
    public enum RiskMode
    {
        NORMAL,         // Operar normalmente
        REDUCED,        // Reduzir tamanho de posição
        BLOCKED         // Não operar
    }
    
    public enum VolatilityRegime
    {
        LOW,
        NORMAL,
        HIGH,
        EXTREME
    }
}
