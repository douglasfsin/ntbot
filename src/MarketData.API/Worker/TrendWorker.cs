using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Orbital.Core.Services;
using MarketData.API.SignalR;
using Npgsql;

namespace MarketData.API.Worker;

public class TrendWorker : BackgroundService
{
    private readonly ILogger<TrendWorker> _logger;
    private readonly IHubContext<MarketHub> _hub;
    private readonly SMCQuestDbClient _dbClient;
    private readonly TrendAnalyzer _analyzer;
    private readonly string[] _tickersToMonitor = { "WDOFUT", "WINFUT" };

    public TrendWorker(
        ILogger<TrendWorker> logger,
        IHubContext<MarketHub> hub,
        SMCQuestDbClient dbClient)
    {
        _logger = logger;
        _hub = hub;
        _dbClient = dbClient;
        _analyzer = new TrendAnalyzer();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TrendWorker iniciado. Atualizando Macro Tendencia a cada 1s.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var ticker in _tickersToMonitor)
                {
                    // 1. Obtem os dados de mercado agregados (VWAP, Agressões) do QuestDB
                    var trendData = await _dbClient.GetMacroTrendDataAsync(ticker, stoppingToken);

                    if (trendData != null)
                    {
                        // 2. Busca o Ajuste Anterior do BD (Tabela points_of_interest)
                        double ajusteAnterior = await GetAjusteAnteriorAsync(ticker, stoppingToken);
                        if (ajusteAnterior == 0 && trendData.PrecoAtual > 0)
                        {
                            // Se não encontrou o ajuste, usa o preço atual temporariamente para não distorcer tudo
                            ajusteAnterior = trendData.PrecoAtual; 
                        }

                        // 3. Aplica heurística do TrendAnalyzer (-4 a +4)
                        var result = _analyzer.EvaluateMacroTrend(trendData, ajusteAnterior);

                        // 4. Envia via SignalR
                        await _hub.Clients.Group(ticker).SendAsync("macrotrend", result, cancellationToken: stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no laco do TrendWorker.");
            }

            await Task.Delay(3000, stoppingToken);
        }
    }

    private async Task<double> GetAjusteAnteriorAsync(string ticker, CancellationToken token)
    {
        try
        {
            return await _dbClient.GetAjusteAnteriorAsync(ticker, token);
        }
        catch
        {
            return 0;
        }
    }
}
