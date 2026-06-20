namespace NtBot.Domain.Entities
{
    /// <summary>
    /// Representa dados de uma opção individual
    /// </summary>
    public class OptionData
    {
        public Guid Id { get; set; }
        public string Symbol { get; set; } = string.Empty; // Ex: "WINFUT"
        public decimal Strike { get; set; }
        public OptionType Type { get; set; } // Call ou Put
        public decimal Gamma { get; set; }
        public decimal Delta { get; set; }
        public int OpenInterest { get; set; }
        public decimal LastPrice { get; set; }
        public decimal ImpliedVolatility { get; set; }
        public DateTime Expiration { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Representa o cálculo agregado de Gamma Exposure
    /// </summary>
    public class GammaExposureData
    {
        public Guid Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        
        // GEX agregado
        public decimal TotalGEX { get; set; }
        public decimal NetGamma { get; set; }
        
        // Níveis críticos
        public decimal? GammaFlipLevel { get; set; } // Nível onde GEX muda de sinal
        public List<GammaWall> GammaWalls { get; set; } = new();
        
        // Análise
        public GEXRegime Regime { get; set; }
        public decimal ExpansionPotential { get; set; } // 0-100
        public decimal MeanReversionPotential { get; set; } // 0-100
        
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Representa uma "parede" de gamma (concentração de OI em um strike)
    /// </summary>
    public class GammaWall
    {
        public decimal Strike { get; set; }
        public decimal GammaConcentration { get; set; }
        public string Type { get; set; } = string.Empty; // "Support" ou "Resistance"
        public decimal Distance { get; set; } // Distância do preço atual (%)
    }

    /// <summary>
    /// Dados de correlação entre dois ativos
    /// </summary>
    public class CorrelationData
    {
        public Guid Id { get; set; }
        public string Symbol1 { get; set; } = string.Empty; // Ex: "NQ"
        public string Symbol2 { get; set; } = string.Empty; // Ex: "WIN"
        
        public decimal PearsonCorrelation { get; set; } // -1 a +1
        public decimal SpearmanCorrelation { get; set; }
        public int LookbackPeriod { get; set; } // Períodos usados
        
        // Direção do símbolo 1 (líder)
        public GlobalBias LeaderBias { get; set; }
        public decimal LeaderMomentum { get; set; }
        public decimal LeaderEMA20 { get; set; }
        public decimal LeaderEMA50 { get; set; }
        
        // Força da tendência
        public decimal TrendStrength { get; set; } // 0-100
        
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Sinal gerado pela estratégia quantitativa
    /// </summary>
    public class QuantSignal
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        
        // Componentes do sinal
        public GlobalBias GlobalBias { get; set; }
        public GEXRegime GEXRegime { get; set; }
        public string WyckoffPhase { get; set; } = string.Empty;
        public SignalDirection Direction { get; set; }
        public StrategyType StrategyType { get; set; } // Breakout ou MeanReversion
        
        // Score e confiança
        public decimal ConfidenceScore { get; set; } // 0-100
        public decimal CorrelationStrength { get; set; } // 0-100
        public decimal GEXAlignment { get; set; } // 0-100
        public decimal WyckoffAlignment { get; set; } // 0-100
        
        // Gestão de risco
        public decimal EntryPrice { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TakeProfit1 { get; set; } // Parcial
        public decimal TakeProfit2 { get; set; } // Final
        public decimal ATRValue { get; set; }
        public decimal RiskRewardRatio { get; set; }
        
        // Detalhes contextuais
        public string Description { get; set; } = string.Empty;
        public List<string> Observations { get; set; } = new();
        
        // Status
        public SignalStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExecutedAt { get; set; }
        
        // Relacionamentos
        public Guid? TradeId { get; set; }
    }

    // Enums
    public enum OptionType
    {
        CALL,
        PUT
    }

    public enum GEXRegime
    {
        POSITIVE_HIGH,      // GEX > 0, muito positivo → compressão, mean reversion
        POSITIVE_LOW,       // GEX > 0, pouco positivo → transição
        NEUTRAL,            // GEX ~ 0
        NEGATIVE_LOW,       // GEX < 0, pouco negativo → início de expansão
        NEGATIVE_HIGH       // GEX < 0, muito negativo → alta volatilidade, breakouts
    }

    public enum GlobalBias
    {
        BULLISH,
        BEARISH,
        NEUTRAL
    }

    public enum StrategyType
    {
        BREAKOUT,           // Continuação de tendência
        MEAN_REVERSION,     // Reversão à média
        HYBRID              // Misto
    }

    // Note: SignalStatus and SignalDirection are already defined in TradingSignal.cs
}
