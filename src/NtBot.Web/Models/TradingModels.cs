namespace NtBot.Web.Models;

public class QuantDashboardModel
{
    public string Symbol { get; set; } = "";
    public string LeaderSymbol { get; set; } = "";
    public decimal CurrentPrice { get; set; }
    public DateTime Timestamp { get; set; }
    public CorrelationModel? Correlation { get; set; }
    public GexModel? Gex { get; set; }
    public QuantSignalModel? Signal { get; set; }
    public string CandleSource { get; set; } = "";
    public string LeaderCandleSource { get; set; } = "";
    public bool DataAvailable { get; set; } = true;
    public string? Message { get; set; }
}

public class CorrelationModel
{
    public string Symbol1 { get; set; } = "";
    public string Symbol2 { get; set; } = "";
    public double PearsonCorrelation { get; set; }
    public string LeaderBias { get; set; } = "NEUTRAL";
    public double LeaderMomentum { get; set; }
    public double TrendStrength { get; set; }
}

public class GexModel
{
    public string Symbol { get; set; } = "";
    public decimal CurrentPrice { get; set; }
    public double TotalGEX { get; set; }
    public double NetGamma { get; set; }
    public string Regime { get; set; } = "NEUTRAL";
    public double ExpansionPotential { get; set; }
    public double MeanReversionPotential { get; set; }
    public List<GammaWallModel> GammaWalls { get; set; } = [];
}

public class GammaWallModel
{
    public decimal Strike { get; set; }
    public double GammaConcentration { get; set; }
    public string Type { get; set; } = "";
    public double Distance { get; set; }
}

public class QuantSignalModel
{
    public string Symbol { get; set; } = "";
    public string GlobalBias { get; set; } = "NEUTRAL";
    public string GexRegime { get; set; } = "NEUTRAL";
    public string WyckoffPhase { get; set; } = "";
    public string Direction { get; set; } = "FLAT";
    public string StrategyType { get; set; } = "";
    public double ConfidenceScore { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit1 { get; set; }
    public double RiskRewardRatio { get; set; }
    public string Description { get; set; } = "";
    public List<string> Observations { get; set; } = [];
}

public class RtdStatisticsModel
{
    public long TotalDataReceived { get; set; }
    public bool IsConnected { get; set; }
    public int TotalTopicsConnected { get; set; }
    public int TopicsWithData { get; set; }
    public double DataRatePerSecond { get; set; }
    public double SecondsSinceLastData { get; set; }
}

public class TickerStatusModel
{
    public string Ticker { get; set; } = "";
    public string? LogicalName { get; set; }
    public bool IsReceivingData { get; set; }
    public int TotalTopics { get; set; }
    public int TopicsWithData { get; set; }
    public decimal? LastPrice { get; set; }
    public long? Volume { get; set; }
    public DateTime? LastUpdate { get; set; }
}

public class ProfitChartHealthModel
{
    public string Status { get; set; } = "";
    public bool IsConnected { get; set; }
    public int TopicsConnected { get; set; }
    public int TopicsWithData { get; set; }
}

public class BookDataModel
{
    public string Ticker { get; set; } = "";
    public List<BookLevelModel> Compra { get; set; } = [];
    public List<BookLevelModel> Venda { get; set; } = [];
}

public class BookLevelModel
{
    public int Level { get; set; }
    public long Quantity { get; set; }
    public decimal Price { get; set; }
}

public class WyckoffAnalysisModel
{
    public string Symbol { get; set; } = "";
    public string Timeframe { get; set; } = "";
    public string Phase { get; set; } = "";
    public decimal PhaseConfidence { get; set; }
    public string? Event { get; set; }
    public string Bias { get; set; } = "NEUTRAL";
    public List<string> Observations { get; set; } = [];
}

public class MacroContextModel
{
    public string Bias { get; set; } = "NEUTRAL";
    public string RiskMode { get; set; } = "NORMAL";
    public decimal VIXLevel { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string VolatilityRegime { get; set; } = "NORMAL";
    public bool IsRiskOn { get; set; }
    public List<string> Observations { get; set; } = [];
}

public class HealthModel
{
    public string Status { get; set; } = "";
    public string Version { get; set; } = "";
    public Dictionary<string, string>? Services { get; set; }
}
