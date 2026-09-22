using System;
using Orbital.Core.Models;

namespace Orbital.Core.Services;

public class TapeSpeedAnalyzer
{
    public TapeSpeedAnalysis Analyze(string ticker, TapeSpeedRawData raw)
    {
        var analysis = new TapeSpeedAnalysis
        {
            Ticker = ticker,
            CalculatedAt = DateTime.UtcNow,
            AvgTradesPerSecond60s = raw.Negocios60s / 60.0,
            AvgContractsPerTrade60s = raw.MediaLotes60s,
            AvgContractsPerTrade5s = raw.MediaLotes5s
        };

        // 1. Apetite (Compra vs Venda) baseado nos ultimos 15s (estabilidade de fluxo)
        double totalVol15s = raw.Compra15s + raw.Venda15s;
        if (totalVol15s > 0)
        {
            double ratioCompra = raw.Compra15s / totalVol15s;
            if (ratioCompra >= 0.55)
            {
                analysis.CurrentAppetite = Appetite.Buy;
            }
            else if (ratioCompra <= 0.45)
            {
                analysis.CurrentAppetite = Appetite.Sell;
            }
            else
            {
                analysis.CurrentAppetite = Appetite.Neutral;
            }
        }
        else
        {
            analysis.CurrentAppetite = Appetite.Neutral;
        }

        // 2. Agitacao (Calmo vs Agitado)
        // Se a frequencia de negocios por segundo nos ultimos 5s for 1.5x maior que a dos ultimos 60s
        double freq5s = raw.Negocios5s / 5.0;
        double freq60s = analysis.AvgTradesPerSecond60s;

        if (freq60s > 0 && freq5s > freq60s * 1.5 && raw.Negocios5s >= 5)
        {
            analysis.State = MarketState.Agitated;
        }
        else
        {
            analysis.State = MarketState.Calm;
        }

        // 3. Negociando acima ou abaixo da media de contratos
        analysis.IsAboveAverage = analysis.AvgContractsPerTrade5s > analysis.AvgContractsPerTrade60s;

        return analysis;
    }
}
