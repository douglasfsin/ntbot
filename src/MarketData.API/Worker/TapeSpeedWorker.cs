using MarketData.API.SignalR;
using Microsoft.AspNetCore.SignalR;
using Orbital.Core.Services;

namespace MarketData.API.Worker;

public class TapeSpeedWorker : BackgroundService
{
    private readonly TapeSpeedEngine _tapeSpeedEngine;
    private readonly IHubContext<MarketHub> _hubContext;
    private readonly ILogger<TapeSpeedWorker> _logger;

    public TapeSpeedWorker(TapeSpeedEngine tapeSpeedEngine, IHubContext<MarketHub> hubContext, ILogger<TapeSpeedWorker> logger)
    {
        _tapeSpeedEngine = tapeSpeedEngine;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TapeSpeedWorker iniciado. Inscrevendo-se no TapeSpeedEngine.");
        
        // Assina o evento emitido pelo motor no Orbital.Core
        _tapeSpeedEngine.OnAnalysisUpdated += OnAnalysisUpdated;

        return Task.CompletedTask;
    }

    private void OnAnalysisUpdated(Orbital.Core.Models.TapeSpeedAnalysis analysis)
    {
        try
        {
            // Extrai dinamicamente o ticker real da análise do motor
            string ticker = string.IsNullOrEmpty(analysis.Ticker) ? "WDOFUT" : analysis.Ticker;
            
            // Dispara para os clientes SignalR conectados ao Hub (grupo do Ticker principal ou para todos)
            _hubContext.Clients.Group(ticker).SendAsync("tapespeed", ticker, analysis);
            
            // Mantém compatibilidade com o grupo legado DOLFUT/INDFUT se o ativo for o Dólar/Índice
            if (ticker.Equals("WDOFUT", StringComparison.OrdinalIgnoreCase))
            {
                _hubContext.Clients.Group("DOLFUT").SendAsync("tapespeed", "DOLFUT", analysis);
            }
            else if (ticker.Equals("WINFUT", StringComparison.OrdinalIgnoreCase))
            {
                _hubContext.Clients.Group("INDFUT").SendAsync("tapespeed", "INDFUT", analysis);
            }
            
            _logger.LogDebug("[TapeSpeedWorker] Emitindo apetite para {T}", ticker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar e enviar OnAnalysisUpdated do Tape Speed");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TapeSpeedWorker parado.");
        _tapeSpeedEngine.OnAnalysisUpdated -= OnAnalysisUpdated;
        return base.StopAsync(cancellationToken);
    }
}
