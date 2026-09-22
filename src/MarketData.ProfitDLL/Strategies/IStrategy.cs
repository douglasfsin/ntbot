namespace ProfitDLLClient.Strategies;

/// <summary>
/// Interface base para implementação de estratégias de trading
/// </summary>
public interface IStrategy
{
    /// <summary>
    /// Nome da estratégia
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Descrição da estratégia
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Indica se a estratégia está ativa
    /// </summary>
    bool IsActive { get; set; }

    /// <summary>
    /// Inicializa a estratégia
    /// </summary>
    void Initialize();

    /// <summary>
    /// Processa um novo trade
    /// </summary>
    void OnTrade(string ticker, string exchange, TConnectorTrade trade);

    /// <summary>
    /// Processa atualização do book de ofertas
    /// </summary>
    void OnOfferBookUpdate(string ticker, string exchange, int side, double price, int quantity);

    /// <summary>
    /// Processa atualização de posição
    /// </summary>
    void OnPositionUpdate(TConnectorAccountIdentifier accountId, TConnectorAssetIdentifier assetId);

    /// <summary>
    /// Processa atualização de ordem
    /// </summary>
    void OnOrderUpdate(TConnectorOrder order);

    /// <summary>
    /// Para a execução da estratégia
    /// </summary>
    void Stop();
}
