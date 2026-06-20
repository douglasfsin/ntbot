namespace NtBot.Api.Services.Profit;

/// <summary>
/// Interface para serviço de integração com ProfitChart via RTD (Real-Time Data)
/// Atua como integrador, recebendo dados do ProfitChart e distribuindo para clientes
/// </summary>
public interface IRtdService
{
    /// <summary>
    /// Evento disparado quando um novo tick é recebido do ProfitChart
    /// </summary>
    event Action<string, string, object>? OnNewTick;

    /// <summary>
    /// Inicializa o servidor RTD e conecta aos tickers configurados
    /// </summary>
    /// <param name="configPath">Caminho para o arquivo de configuração JSON</param>
    Task InitializeAsync(string configPath = "rtd_config.json");

    /// <summary>
    /// Obtém o alias lógico de um ticker
    /// </summary>
    /// <param name="ticker">Nome do ticker (ex: WDOK26)</param>
    /// <returns>Nome lógico do ticker ou null se não encontrado</returns>
    string? GetAliasByTicker(string ticker);

    /// <summary>
    /// Obtém o ticker real de um nome lógico
    /// </summary>
    /// <param name="logical">Nome lógico configurado</param>
    /// <returns>Ticker real ou null se não encontrado</returns>
    string? GetTicker(string logical);

    /// <summary>
    /// Obtém a configuração completa de um ticker
    /// </summary>
    /// <param name="logical">Nome lógico do ticker</param>
    /// <returns>Configuração do ticker ou null se não encontrado</returns>
    RtdTickerConfig? GetConfig(string logical);

    /// <summary>
    /// Obtém o valor base (multiplicador de pontos) de um ticker
    /// </summary>
    /// <param name="ticker">Nome do ticker</param>
    /// <returns>Valor base ou 1 se não encontrado</returns>
    int GetBase(string ticker);

    /// <summary>
    /// Obtém o número máximo de contratos configurado para um ticker
    /// </summary>
    /// <param name="ticker">Nome do ticker</param>
    /// <returns>Número de contratos ou 0 se não configurado</returns>
    int GetContratoLimite(string ticker);

    /// <summary>
    /// Obtém estatísticas de comunicação com o ProfitChart
    /// </summary>
    /// <returns>Objeto com métricas de conexão e dados recebidos</returns>
    RtdStatistics GetStatistics();

    /// <summary>
    /// Obtém todos os tickers configurados e seus status
    /// </summary>
    /// <returns>Dicionário com ticker e status de conexão</returns>
    Dictionary<string, TickerStatus> GetAllTickersStatus();

    /// <summary>
    /// Obtém o último valor recebido de um tópico específico
    /// </summary>
    /// <param name="ticker">Nome do ticker</param>
    /// <param name="topic">Tópico (ULT, VOL, PRT, etc.)</param>
    /// <returns>Último valor ou null se não disponível</returns>
    object? GetLastValue(string ticker, string topic);

    /// <summary>
    /// Obtém todos os valores atuais de um ticker
    /// </summary>
    /// <param name="ticker">Nome do ticker</param>
    /// <returns>Dicionário com todos os tópicos e seus valores</returns>
    Dictionary<string, object>? GetTickerSnapshot(string ticker);
}

/// <summary>
/// Configuração de um ticker no sistema RTD
/// </summary>
public class RtdTickerConfig
{
    /// <summary>
    /// Ticker único (ex: WDOK26)
    /// </summary>
    public string TICK { get; set; } = string.Empty;

    /// <summary>
    /// Lista de tickers alternativos
    /// </summary>
    public List<string>? TICKERS { get; set; }

    /// <summary>
    /// Multiplicador de pontos (ex: 1 para índice, 5 para dólar)
    /// </summary>
    public int BASE { get; set; } = 1;

    /// <summary>
    /// Número máximo de contratos permitido
    /// </summary>
    public int N_CONTRATO { get; set; } = 1;

    /// <summary>
    /// Descrição do ativo
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Tipo do ativo (FUTURE, OPTION, FOREX, STOCK)
    /// </summary>
    public string? AssetType { get; set; }

    /// <summary>
    /// Indica se o ticker está ativo para trading
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Estatísticas de comunicação RTD
/// </summary>
public class RtdStatistics
{
    /// <summary>
    /// Total de dados recebidos desde a inicialização
    /// </summary>
    public int TotalDataReceived { get; set; }

    /// <summary>
    /// Data/hora da última recepção de dados
    /// </summary>
    public DateTime LastDataReceived { get; set; }

    /// <summary>
    /// Data/hora de inicialização do serviço
    /// </summary>
    public DateTime ServiceStarted { get; set; }

    /// <summary>
    /// Número total de tópicos conectados
    /// </summary>
    public int TotalTopicsConnected { get; set; }

    /// <summary>
    /// Número de tópicos que receberam dados
    /// </summary>
    public int TopicsWithData { get; set; }

    /// <summary>
    /// Taxa de dados por segundo (média)
    /// </summary>
    public double DataRatePerSecond { get; set; }

    /// <summary>
    /// Indica se a comunicação está ativa (recebeu dados nos últimos 30s)
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Tempo desde a última recepção em segundos
    /// </summary>
    public double SecondsSinceLastData { get; set; }
}

/// <summary>
/// Status de um ticker específico
/// </summary>
public class TickerStatus
{
    /// <summary>
    /// Nome do ticker
    /// </summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>
    /// Nome lógico/alias
    /// </summary>
    public string? LogicalName { get; set; }

    /// <summary>
    /// Indica se está recebendo dados
    /// </summary>
    public bool IsReceivingData { get; set; }

    /// <summary>
    /// Total de tópicos configurados
    /// </summary>
    public int TotalTopics { get; set; }

    /// <summary>
    /// Tópicos que receberam dados
    /// </summary>
    public int TopicsWithData { get; set; }

    /// <summary>
    /// Última atualização recebida
    /// </summary>
    public DateTime? LastUpdate { get; set; }

    /// <summary>
    /// Último preço (ULT)
    /// </summary>
    public double? LastPrice { get; set; }

    /// <summary>
    /// Volume total (VOL)
    /// </summary>
    public double? Volume { get; set; }
}
