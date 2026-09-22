using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Marketdata.Database.Models;

namespace Orbital.Core.Services;

public class SMCEngine : BackgroundService
{
    private readonly SMCQuestDbClient _dbClient;
    private readonly ZoneInterestManager _zoneManager;
    private readonly ILogger<SMCEngine> _logger;
    private readonly string[] _tickersToMonitor = { "WDOFUT", "WINFUT" };

    // Configuracoes de varredura
    private readonly TimeSpan _scanInterval = TimeSpan.FromSeconds(5);
    private readonly int _windowSeconds = 5;
    private readonly int _minTicksThreshold = 15;
    private readonly int _minVolumeThreshold = 1000;

    public SMCEngine(SMCQuestDbClient dbClient, ZoneInterestManager zoneManager, ILogger<SMCEngine> logger)
    {
        _dbClient = dbClient ?? throw new ArgumentNullException(nameof(dbClient));
        _zoneManager = zoneManager ?? throw new ArgumentNullException(nameof(zoneManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SMC Engine iniciado. Scan a cada {Interval}s.", _scanInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var ticker in _tickersToMonitor)
                {
                    await RunAnalyticsScanAsync(ticker, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha na execucao do laco principal do SMCEngine.");
            }

            // Aguarda o proximo ciclo
            await Task.Delay(_scanInterval, stoppingToken);
        }

        _logger.LogInformation("SMC Engine finalizado.");
    }

    private async Task RunAnalyticsScanAsync(string ticker, CancellationToken cancellationToken)
    {
        // 1. Consulta o QuestDB procurando absorcoes recentes
        var candidates = await _dbClient.DetectOrderBlocksAsync(ticker, _windowSeconds, _minTicksThreshold, _minVolumeThreshold, cancellationToken);

        if (candidates.Count > 0)
        {
            _logger.LogInformation("SMC Engine detectou {Count} candidatos a Order Block para {Ticker}.", candidates.Count, ticker);

            foreach (var candidate in candidates)
            {
                // Determina o Side da absorcao
                // Se o volume de compra for muito maior que o de venda numa faixa apertada, o institucional esta passivo na venda
                // Logo, absorcao da agressao de compra -> "Order Block de Venda" (bearish OB).
                // Para simplificar: quem bateu mais agredindo define quem foi absorvido.
                string obSide = candidate.VolCompra > candidate.VolVenda ? "Venda" : "Compra";

                // 2. Cria o registro para o Banco
                var poiRecord = new PointOfInterestRecord
                {
                    PoiId = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    Ticker = ticker,
                    Type = "OrderBlock",
                    Side = obSide,
                    Description = $"OB Dinâmico M1",
                    PriceStart = candidate.Price,
                    PriceEnd = null, // Pode ter offset se necessario
                    IsActive = true
                };

                // 3. Salva no DB
                await _dbClient.SaveSMCPointAsync(poiRecord, cancellationToken);

                // 4. Atualiza a memoria
                // Passamos apenas esse como update incrementativo, mas o ideal seria o ZoneInterestManager
                // suportar adicao direta de single POI. 
                // Para nao reescrever a memoria inteira, inserimos localmente via AddZone ou simular recarregamento completo:
                _zoneManager.AddSingleZoneFromRecord(poiRecord);
            }
        }
    }
}
