namespace ProfitDLLClient.Strategies;

/// <summary>
/// Gerenciador de estratégias
/// </summary>
public class StrategyManager
{
    private readonly List<IStrategy> _strategies = new();
    private readonly PositionManager _positionManager;
    private CancellationTokenSource _cancellationTokenSource;
    private Task _monitoringTask;

    public PositionManager PositionManager => _positionManager;

    public StrategyManager()
    {
        _positionManager = new PositionManager();
    }

    /// <summary>
    /// Registra uma nova estratégia
    /// </summary>
    public void RegisterStrategy(IStrategy strategy)
    {
        if (!_strategies.Contains(strategy))
        {
            _strategies.Add(strategy);
            Console.WriteLine($"[StrategyManager] Estratégia '{strategy.Name}' registrada");
        }
    }

    /// <summary>
    /// Inicia todas as estratégias ativas
    /// </summary>
    public void StartAll()
    {
        foreach (var strategy in _strategies.Where(s => !s.IsActive))
        {
            strategy.Initialize();
        }

        StartMonitoring();
    }

    /// <summary>
    /// Para todas as estratégias
    /// </summary>
    public void StopAll()
    {
        foreach (var strategy in _strategies.Where(s => s.IsActive))
        {
            strategy.Stop();
        }

        StopMonitoring();
    }

    /// <summary>
    /// Inicia uma estratégia específica
    /// </summary>
    public void StartStrategy(string name)
    {
        var strategy = _strategies.FirstOrDefault(s => s.Name == name);
        if (strategy != null && !strategy.IsActive)
        {
            strategy.Initialize();
        }
    }

    /// <summary>
    /// Para uma estratégia específica
    /// </summary>
    public void StopStrategy(string name)
    {
        var strategy = _strategies.FirstOrDefault(s => s.Name == name);
        if (strategy != null && strategy.IsActive)
        {
            strategy.Stop();
        }
    }

    /// <summary>
    /// Processa novo trade para todas as estratégias ativas
    /// </summary>
    public void ProcessTrade(string ticker, string exchange, TConnectorTrade trade)
    {
        foreach (var strategy in _strategies.Where(s => s.IsActive))
        {
            try
            {
                strategy.OnTrade(ticker, exchange, trade);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrategyManager] Erro ao processar trade na estratégia '{strategy.Name}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Processa atualização do book de ofertas
    /// </summary>
    public void ProcessOfferBookUpdate(string ticker, string exchange, int side, double price, int quantity)
    {
        foreach (var strategy in _strategies.Where(s => s.IsActive))
        {
            try
            {
                strategy.OnOfferBookUpdate(ticker, exchange, side, price, quantity);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrategyManager] Erro ao processar offer book na estratégia '{strategy.Name}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Processa atualização de posição
    /// </summary>
    public void ProcessPositionUpdate(TConnectorAccountIdentifier accountId, TConnectorAssetIdentifier assetId)
    {
        foreach (var strategy in _strategies.Where(s => s.IsActive))
        {
            try
            {
                strategy.OnPositionUpdate(accountId, assetId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrategyManager] Erro ao processar posição na estratégia '{strategy.Name}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Processa atualização de ordem
    /// </summary>
    public void ProcessOrderUpdate(TConnectorOrder order)
    {
        foreach (var strategy in _strategies.Where(s => s.IsActive))
        {
            try
            {
                strategy.OnOrderUpdate(order);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrategyManager] Erro ao processar ordem na estratégia '{strategy.Name}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Lista todas as estratégias
    /// </summary>
    public void ListStrategies()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                        ESTRATÉGIAS REGISTRADAS                             ║");
        Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════╣");

        if (_strategies.Count == 0)
        {
            Console.WriteLine("║  Nenhuma estratégia registrada                                             ║");
        }
        else
        {
            foreach (var strategy in _strategies)
            {
                var status = strategy.IsActive ? "ATIVA  " : "PARADA ";
                Console.WriteLine($"║  [{status}] {strategy.Name,-40} ║");
                Console.WriteLine($"║           {strategy.Description,-61} ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════╣");
            }
        }

        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════╝\n");
    }

    /// <summary>
    /// Inicia monitoramento periódico das posições
    /// </summary>
    private void StartMonitoring()
    {
        if (_monitoringTask != null && !_monitoringTask.IsCompleted)
            return;

        _cancellationTokenSource = new CancellationTokenSource();
        _monitoringTask = Task.Run(async () =>
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, _cancellationTokenSource.Token); // Atualiza a cada 5 segundos
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, _cancellationTokenSource.Token);
    }

    /// <summary>
    /// Para o monitoramento
    /// </summary>
    private void StopMonitoring()
    {
        _cancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// Exibe resumo das posições
    /// </summary>
    public void ShowPositions()
    {
        _positionManager.PrintPositionsSummary();
    }
}
