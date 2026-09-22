using System;

namespace Orbital.Core.Models;

public enum MarketState
{
    Calm,
    Agitated
}

public enum Appetite
{
    Buy,
    Sell,
    Neutral
}

public class TapeSpeedRawData
{
    public double Compra5s { get; set; }
    public double Venda5s { get; set; }
    public long Negocios5s { get; set; }
    
    public double Compra15s { get; set; }
    public double Venda15s { get; set; }
    public long Negocios15s { get; set; }
    
    public double Compra60s { get; set; }
    public double Venda60s { get; set; }
    public long Negocios60s { get; set; }
    
    public double MediaLotes5s { get; set; }
    public double MediaLotes60s { get; set; }
}

public class TapeSpeedAnalysis
{
    public string Ticker { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; }
    
    // As 4 Respostas do Motor
    public Appetite CurrentAppetite { get; set; }
    public MarketState State { get; set; }
    
    public double AvgTradesPerSecond60s { get; set; }
    public double AvgContractsPerTrade60s { get; set; }
    public double AvgContractsPerTrade5s { get; set; }
    
    public bool IsAboveAverage { get; set; }
}
