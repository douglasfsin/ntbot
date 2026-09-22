using ProfitDLLClient.Strategies;
using ProfitDLLClient.Strategies.Examples;

namespace ProfitDLLClient;

/// <summary>
/// Exemplo de integração do sistema de estratégias com o Program.cs
/// </summary>
public static class StrategyIntegration
{
    private static StrategyManager _strategyManager;

    /// <summary>
    /// Inicializa o sistema de estratégias
    /// </summary>
    public static void Initialize()
    {
        _strategyManager = new StrategyManager();

        // Registra estratégias de exemplo
        _strategyManager.RegisterStrategy(new SimpleMovingAverageStrategy(
            _strategyManager.PositionManager,
            "PETR4",
            "BOVESPA",
            20
        ));

        _strategyManager.RegisterStrategy(new VolumeMonitorStrategy(
            _strategyManager.PositionManager,
            1000
        ));

        Console.WriteLine("[StrategyIntegration] Sistema de estratégias inicializado");
    }

    /// <summary>
    /// Inicia as estratégias
    /// </summary>
    public static void Start()
    {
        if (_strategyManager != null)
        {
            _strategyManager.StartAll();
            _strategyManager.ListStrategies();
        }
    }

    /// <summary>
    /// Para as estratégias
    /// </summary>
    public static void Stop()
    {
        if (_strategyManager != null)
        {
            _strategyManager.StopAll();
        }
    }

    /// <summary>
    /// Processa callback de trade
    /// Adicione esta chamada dentro do seu TradeCallback existente
    /// </summary>
    public static void OnTradeCallback(string ticker, string exchange, TConnectorTrade trade)
    {
        if (_strategyManager != null)
        {
            _strategyManager.ProcessTrade(ticker, exchange, trade);
        }
    }

    /// <summary>
    /// Processa callback de offerbook
    /// Adicione esta chamada dentro do seu OfferBookCallback existente
    /// </summary>
    public static void OnOfferBookCallback(string ticker, string exchange, int side, double price, int quantity)
    {
        if (_strategyManager != null)
        {
            _strategyManager.ProcessOfferBookUpdate(ticker, exchange, side, price, quantity);
        }
    }

    /// <summary>
    /// Processa callback de posição
    /// Adicione esta chamada dentro do seu AssetPositionListCallback existente
    /// </summary>
    public static void OnPositionCallback(TConnectorAccountIdentifier accountId, TConnectorAssetIdentifier assetId)
    {
        if (_strategyManager != null)
        {
            _strategyManager.ProcessPositionUpdate(accountId, assetId);
        }
    }

    /// <summary>
    /// Processa callback de ordem
    /// Adicione esta chamada dentro do seu OrderCallback existente
    /// </summary>
    public static void OnOrderCallback(TConnectorOrder order)
    {
        if (_strategyManager != null)
        {
            _strategyManager.ProcessOrderUpdate(order);
        }
    }

    /// <summary>
    /// Exibe as posições abertas
    /// </summary>
    public static void ShowPositions()
    {
        if (_strategyManager != null)
        {
            _strategyManager.ShowPositions();
        }
    }

    /// <summary>
    /// Lista as estratégias registradas
    /// </summary>
    public static void ListStrategies()
    {
        if (_strategyManager != null)
        {
            _strategyManager.ListStrategies();
        }
    }

    /// <summary>
    /// Exemplo de como adicionar uma posição manualmente para teste
    /// </summary>
    public static void AddTestPosition(string asset, string exchange, string accountId, int brokerId,
                                      TConnectorOrderSide side, long quantity, double averagePrice, double currentPrice)
    {
        if (_strategyManager != null)
        {
            _strategyManager.PositionManager.UpdatePosition(
                asset, exchange, accountId, brokerId,
                side, quantity, averagePrice, currentPrice
            );
        }
    }
}

/* ==================================================================================
 * INSTRUÇÕES DE INTEGRAÇÃO NO Program.cs
 * ==================================================================================
 * 
 * 1. No método Main(), adicione após a conexão ser estabelecida:
 * 
 *    StrategyIntegration.Initialize();
 *    StrategyIntegration.Start();
 * 
 * 2. Adicione comando no menu para exibir posições:
 * 
 *    case "pos":
 *    case "posicoes":
 *        StrategyIntegration.ShowPositions();
 *        break;
 * 
 *    case "strategies":
 *    case "estrategias":
 *        StrategyIntegration.ListStrategies();
 *        break;
 * 
 * 3. Modifique os callbacks existentes para integrar com as estratégias:
 * 
 * // No TradeCallbackV2 (ou similar), adicione:
 * public static void TradeCallbackV2(TConnectorAssetIdentifier assetId, /* outros params *\/)
 * {
 *     // ... seu código existente ...
 *     
 *     // Integração com estratégias
 *     StrategyIntegration.OnTradeCallback(
 *         assetId.Ticker, 
 *         assetId.Exchange, 
 *         trade
 *     );
 * }
 * 
 * // No OfferBookCallbackV2, adicione:
 * public static void OfferBookCallbackV2(TAssetID assetId, int nAction, int nPosition, 
 *                                        int Side, int nQtd, /* outros params *\/)
 * {
 *     // ... seu código existente ...
 *     
 *     // Integração com estratégias
 *     if (bHasPrice == 1 && bHasQtd == 1)
 *     {
 *         StrategyIntegration.OnOfferBookCallback(
 *             assetId.Ticker,
 *             assetId.Bolsa,
 *             Side,
 *             sPrice,
 *             nQtd
 *         );
 *     }
 * }
 * 
 * // No AssetPositionListCallback, adicione:
 * public static void AssetPositionListCallback(TConnectorAccountIdentifier AccountID,
 *                                              TConnectorAssetIdentifier assetId, 
 *                                              int EventID)
 * {
 *     // ... seu código existente ...
 *     
 *     // Integração com estratégias
 *     StrategyIntegration.OnPositionCallback(AccountID, assetId);
 *     
 *     // Exemplo: buscar e atualizar posição real
 *     var position = ProfitDLL.GetPosition(
 *         AccountID.AccountID,
 *         AccountID.BrokerID.ToString(),
 *         assetId.Ticker,
 *         assetId.Exchange
 *     );
 *     
 *     if (position != IntPtr.Zero)
 *     {
 *         // Extrair dados da posição e atualizar o PositionManager
 *         // ... processar o ponteiro da posição ...
 *     }
 * }
 * 
 * // No OrderCallback, adicione:
 * public static void OrderCallback(TConnectorOrderIdentifier orderId, 
 *                                  TConnectorAccountIdentifier accountId, 
 *                                  TConnectorAssetIdentifier assetId)
 * {
 *     // ... seu código existente ...
 *     
 *     var order = new TConnectorOrder { /* preencher campos *\/ };
 *     ProfitDLL.GetOrderDetails(ref order);
 *     
 *     // Integração com estratégias
 *     StrategyIntegration.OnOrderCallback(order);
 * }
 * 
 * 4. Exemplo de teste manual (adicione no menu):
 * 
 *    case "testpos":
 *        // Adiciona posições de teste
 *        StrategyIntegration.AddTestPosition(
 *            "PETR4", "BOVESPA", "123456", 1,
 *            TConnectorOrderSide.Buy, 100, 38.50, 39.20
 *        );
 *        StrategyIntegration.AddTestPosition(
 *            "VALE3", "BOVESPA", "123456", 1,
 *            TConnectorOrderSide.Buy, 200, 68.20, 67.80
 *        );
 *        StrategyIntegration.ShowPositions();
 *        break;
 * 
 * 5. No encerramento da aplicação:
 * 
 *    StrategyIntegration.Stop();
 * 
 * ================================================================================== */
