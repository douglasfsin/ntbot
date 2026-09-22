using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orbital.Core.Models;

namespace Orbital.Core.Services;

public class TapeSpeedEngine : BackgroundService
{
    private readonly TapeSpeedDbClient _dbClient;
    private readonly TapeSpeedAnalyzer _analyzer;
    private readonly ILogger<TapeSpeedEngine> _logger;

    private readonly ConcurrentDictionary<string, TapeSpeedAnalysis> _latestAnalyses = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] _tickersToMonitor = { "WDOFUT", "WINFUT" }; // Suporte a múltiplos ativos configuráveis
    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(1);

    public event Action<TapeSpeedAnalysis>? OnAnalysisUpdated;

    public TapeSpeedEngine(
        TapeSpeedDbClient dbClient,
        TapeSpeedAnalyzer analyzer,
        ILogger<TapeSpeedEngine> logger)
    {
        _dbClient = dbClient ?? throw new ArgumentNullException(nameof(dbClient));
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public TapeSpeedAnalysis? GetLatestAnalysis(string ticker)
    {
        return _latestAnalyses.TryGetValue(ticker, out var analysis) ? analysis : null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tape Speed Engine iniciado. Atualizando a cada {Interval}s.", _refreshInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var ticker in _tickersToMonitor)
                {
                    await UpdateTapeSpeedForTickerAsync(ticker, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no laço principal do TapeSpeedEngine.");
            }

            await Task.Delay(_refreshInterval, stoppingToken);
        }

        _logger.LogInformation("Tape Speed Engine finalizado.");
    }

    private async Task UpdateTapeSpeedForTickerAsync(string ticker, CancellationToken cancellationToken)
    {
        // 1. Obtém dados crus do banco
        var rawData = await _dbClient.GetRawTapeSpeedDataAsync(ticker, cancellationToken);

        // 2. Processa com a lógica analítica do motor
        var analysis = _analyzer.Analyze(ticker, rawData);

        // 3. Atualiza cache em memória
        _latestAnalyses[ticker] = analysis;

        // 4. Dispara evento para assinantes
        OnAnalysisUpdated?.Invoke(analysis);

        _logger.LogDebug("Tape Speed atualizado para {Ticker}. Appetite: {Appetite}, State: {State}", 
            ticker, analysis.CurrentAppetite, analysis.State);
    }
}
