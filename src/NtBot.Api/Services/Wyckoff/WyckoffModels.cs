using NtBot.Domain.Entities;

namespace NtBot.Api.Services.Wyckoff
{
    /// <summary>
    /// Resultado da análise Wyckoff
    /// </summary>
    public class WyckoffAnalysisResult
    {
        public string Symbol { get; set; } = string.Empty;
        public string Timeframe { get; set; } = string.Empty;
        public DateTime AnalysisTime { get; set; }
        
        // Fase detectada
        public WyckoffPhase Phase { get; set; }
        public decimal PhaseConfidence { get; set; } // 0-100
        
        // Evento detectado
        public WyckoffEvent? Event { get; set; }
        public decimal EventConfidence { get; set; } // 0-100
        
        // Range information
        public decimal? RangeHigh { get; set; }
        public decimal? RangeLow { get; set; }
        public int? RangeCandles { get; set; }
        
        // Volume analysis
        public bool VolumeConfirmation { get; set; }
        public decimal? CurrentVolume { get; set; }
        public decimal? AverageVolume { get; set; }
        public decimal? VolumeDeltaAverage { get; set; }
        
        // Structure
        public List<StructureLevel> KeyLevels { get; set; } = new();
        
        // Bias
        public MarketBias Bias { get; set; }
        
        // Observações
        public List<string> Observations { get; set; } = new();
    }
    
    public enum WyckoffPhase
    {
        UNKNOWN,
        ACCUMULATION,           // Fase A-E de acumulação
        DISTRIBUTION,           // Fase A-E de distribuição
        MARKUP,                 // Tendência de alta
        MARKDOWN,               // Tendência de baixa
        RANGING                 // Lateralização sem fase clara
    }
    
    public enum WyckoffEvent
    {
        // Acumulação
        PS,                     // Preliminary Support
        SC,                     // Selling Climax
        AR,                     // Automatic Rally
        ST,                     // Secondary Test
        SPRING,                 // Spring (falso rompimento baixo)
        SOS,                    // Sign of Strength
        LPS,                    // Last Point of Support
        
        // Distribuição
        PSY,                    // Preliminary Supply
        BC,                     // Buying Climax
        AR_DIST,                // Automatic Reaction
        ST_DIST,                // Secondary Test
        UPTHRUST,               // Upthrust (falso rompimento alto)
        SOW,                    // Sign of Weakness
        LPSY                    // Last Point of Supply
    }
    
    public enum MarketBias
    {
        BULLISH,
        BEARISH,
        NEUTRAL
    }
    
    public class StructureLevel
    {
        public string Type { get; set; } = string.Empty; // "SUPPORT", "RESISTANCE", "POC"
        public decimal Price { get; set; }
        public int Touches { get; set; }
        public DateTime FirstTouch { get; set; }
        public DateTime LastTouch { get; set; }
        public decimal Strength { get; set; } // 0-100
    }
}
