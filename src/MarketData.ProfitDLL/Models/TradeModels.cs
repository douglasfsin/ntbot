namespace ProfitDLLClient.Models;

/// <summary>
/// Representa um trade (negócio) com informações de candle
/// </summary>
public struct CandleTrade
{
    public CandleTrade(double close, double vol, double open, double max, double min, int qtd, string asset, DateTime date)
    {
        Close = close;
        Vol = vol;
        Qtd = qtd;
        Asset = asset;
        Date = date;
        Open = open;
        Max = max;
        Min = min;
    }

    public double Close { get; set; }
    public double Vol { get; set; }
    public double Max { get; set; }
    public double Min { get; set; }
    public double Open { get; set; }
    public int Qtd { get; set; }
    public string Asset { get; set; }
    public DateTime Date { get; set; }
}

/// <summary>
/// Representa um trade simples
/// </summary>
public struct Trade
{
    public Trade(double price, double vol, int qtd, string asset, string date)
    {
        Price = price;
        Qtd = qtd;
        Asset = asset;
        Date = date;
        Vol = vol;
    }

    public double Price { get; }
    public double Vol { get; }
    public int Qtd { get; }
    public string Asset { get; }
    public string Date { get; }
}
