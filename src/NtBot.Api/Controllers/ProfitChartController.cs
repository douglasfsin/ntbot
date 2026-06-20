using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NtBot.Api.Services.Connector;
using NtBot.Api.Services.Profit;
using NtBot.Connector.Services;
using System.Security.Claims;

namespace NtBot.Api.Controllers;

/// <summary>
/// Controlador para integração com ProfitChart via RTD
/// Fornece endpoints REST para consulta de dados em tempo real
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProfitChartController : ControllerBase
{
    private readonly IRtdService _rtdService;
    private readonly IConnectorLiveState _liveState;
    private readonly ILogger<ProfitChartController> _logger;

    public ProfitChartController(
        IRtdService rtdService,
        IConnectorLiveState liveState,
        ILogger<ProfitChartController> logger)
    {
        _rtdService = rtdService;
        _liveState = liveState;
        _logger = logger;
    }

    private ConnectorLiveSnapshot? GetTenantLiveSnapshot()
    {
        if (User.Identity?.IsAuthenticated != true)
            return null;

        var claim = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(claim, out var tenantId))
            return null;

        return _liveState.GetSnapshot(tenantId);
    }

    /// <summary>
    /// Obtém estatísticas do serviço RTD
    /// </summary>
    /// <returns>Estatísticas de comunicação com ProfitChart</returns>
    /// <response code="200">Estatísticas retornadas com sucesso</response>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(RtdStatistics), StatusCodes.Status200OK)]
    public ActionResult<RtdStatistics> GetStatistics()
    {
        _logger.LogDebug("GET statistics chamado");
        var live = GetTenantLiveSnapshot();
        if (live is { TotalTicksReceived: > 0 })
            return Ok(ConnectorLiveMapper.ToStatistics(live));

        var stats = _rtdService.GetStatistics();
        return Ok(stats);
    }

    /// <summary>
    /// Obtém status de todos os tickers configurados
    /// </summary>
    /// <returns>Dicionário com ticker e seu status</returns>
    /// <response code="200">Status retornado com sucesso</response>
    [HttpGet("tickers")]
    [ProducesResponseType(typeof(Dictionary<string, TickerStatus>), StatusCodes.Status200OK)]
    public ActionResult<Dictionary<string, TickerStatus>> GetAllTickers()
    {
        _logger.LogDebug("GET tickers chamado");
        var live = GetTenantLiveSnapshot();
        if (live is { Ticks.Count: > 0 })
            return Ok(ConnectorLiveMapper.ToTickers(live));

        var tickers = _rtdService.GetAllTickersStatus();
        return Ok(tickers);
    }

    /// <summary>
    /// Obtém snapshot completo de um ticker específico
    /// </summary>
    /// <param name="ticker">Nome do ticker (ex: WDOK26)</param>
    /// <returns>Todos os tópicos e valores atuais do ticker</returns>
    /// <response code="200">Snapshot retornado com sucesso</response>
    /// <response code="404">Ticker não encontrado ou sem dados</response>
    [HttpGet("tickers/{ticker}")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Dictionary<string, object>> GetTickerSnapshot(string ticker)
    {
        _logger.LogDebug("GET ticker snapshot: {Ticker}", ticker);
        
        var snapshot = _rtdService.GetTickerSnapshot(ticker);
        
        if (snapshot == null)
        {
            _logger.LogWarning("Ticker '{Ticker}' não encontrado ou sem dados", ticker);
            return NotFound(new { message = $"Ticker '{ticker}' não encontrado ou sem dados" });
        }

        return Ok(snapshot);
    }

    /// <summary>
    /// Obtém valor específico de um tópico
    /// </summary>
    /// <param name="ticker">Nome do ticker (ex: WDOK26)</param>
    /// <param name="topic">Nome do tópico (ULT, VOL, PRT, etc.)</param>
    /// <returns>Valor atual do tópico</returns>
    /// <response code="200">Valor retornado com sucesso</response>
    /// <response code="404">Ticker/tópico não encontrado</response>
    [HttpGet("tickers/{ticker}/{topic}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<object> GetTickerTopic(string ticker, string topic)
    {
        _logger.LogDebug("GET ticker topic: {Ticker}.{Topic}", ticker, topic);
        
        var value = _rtdService.GetLastValue(ticker, topic);
        
        if (value == null)
        {
            _logger.LogWarning("Ticker '{Ticker}' tópico '{Topic}' não encontrado", ticker, topic);
            return NotFound(new { message = $"Ticker '{ticker}' tópico '{topic}' não encontrado" });
        }

        return Ok(new 
        { 
            ticker, 
            topic, 
            value,
            timestamp = DateTime.Now
        });
    }

    /// <summary>
    /// Obtém configuração de um ticker
    /// </summary>
    /// <param name="logical">Nome lógico do ticker</param>
    /// <returns>Configuração completa do ticker</returns>
    /// <response code="200">Configuração retornada com sucesso</response>
    /// <response code="404">Ticker não encontrado</response>
    [HttpGet("config/{logical}")]
    [ProducesResponseType(typeof(RtdTickerConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<RtdTickerConfig> GetConfig(string logical)
    {
        _logger.LogDebug("GET config: {Logical}", logical);
        
        var config = _rtdService.GetConfig(logical);
        
        if (config == null)
        {
            _logger.LogWarning("Configuração '{Logical}' não encontrada", logical);
            return NotFound(new { message = $"Configuração '{logical}' não encontrada" });
        }

        return Ok(config);
    }

    /// <summary>
    /// Health check do serviço RTD
    /// </summary>
    /// <returns>Status de saúde do serviço</returns>
    /// <response code="200">Serviço saudável</response>
    /// <response code="503">Serviço com problemas</response>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult GetHealth()
    {
        var live = GetTenantLiveSnapshot();
        if (live is { TotalTicksReceived: > 0 })
        {
            var stats = ConnectorLiveMapper.ToStatistics(live);
            var health = new
            {
                status = stats.IsConnected ? "healthy" : "degraded",
                isConnected = stats.IsConnected,
                totalDataReceived = stats.TotalDataReceived,
                secondsSinceLastData = stats.SecondsSinceLastData,
                serviceStarted = stats.ServiceStarted,
                topicsConnected = stats.TotalTopicsConnected,
                topicsWithData = stats.TopicsWithData,
                dataRate = $"{stats.DataRatePerSecond:F2} data/s",
                source = "connector",
                timestamp = DateTime.UtcNow
            };
            return Ok(health);
        }

        var rtdStats = _rtdService.GetStatistics();
        
        var health = new
        {
            status = rtdStats.IsConnected ? "healthy" : "unhealthy",
            isConnected = rtdStats.IsConnected,
            totalDataReceived = rtdStats.TotalDataReceived,
            secondsSinceLastData = rtdStats.SecondsSinceLastData,
            serviceStarted = rtdStats.ServiceStarted,
            topicsConnected = rtdStats.TotalTopicsConnected,
            topicsWithData = rtdStats.TopicsWithData,
            dataRate = $"{rtdStats.DataRatePerSecond:F2} data/s",
            source = "rtd",
            timestamp = DateTime.Now
        };

        if (!rtdStats.IsConnected && rtdStats.TotalDataReceived == 0)
        {
            _logger.LogWarning("RTD Service unhealthy: Nenhum dado recebido");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, health);
        }

        if (!rtdStats.IsConnected)
        {
            _logger.LogWarning("RTD Service unhealthy: Sem dados há {Seconds}s", rtdStats.SecondsSinceLastData);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, health);
        }

        return Ok(health);
    }

    /// <summary>
    /// Obtém preço atual (ULT) de múltiplos tickers
    /// </summary>
    /// <param name="tickers">Lista de tickers separados por vírgula</param>
    /// <returns>Dicionário com ticker e preço atual</returns>
    /// <response code="200">Preços retornados com sucesso</response>
    [HttpGet("prices")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    public ActionResult<Dictionary<string, object>> GetPrices([FromQuery] string tickers)
    {
        _logger.LogDebug("GET prices: {Tickers}", tickers);
        
        var tickerList = tickers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(t => t.Trim())
                                .ToList();

        var result = new Dictionary<string, object>();

        foreach (var ticker in tickerList)
        {
            var price = _rtdService.GetLastValue(ticker, "ULT");
            if (price != null)
            {
                result[ticker] = new
                {
                    price,
                    timestamp = DateTime.Now
                };
            }
            else
            {
                result[ticker] = new
                {
                    error = "Ticker não encontrado ou sem dados",
                    timestamp = DateTime.Now
                };
            }
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtém book de ofertas (níveis de compra/venda) de um ticker
    /// </summary>
    /// <param name="ticker">Nome do ticker</param>
    /// <param name="levels">Número de níveis (1-20, padrão: 5)</param>
    /// <returns>Book com níveis de compra e venda</returns>
    /// <response code="200">Book retornado com sucesso</response>
    /// <response code="404">Ticker não encontrado</response>
    [HttpGet("book/{ticker}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetBook(string ticker, [FromQuery] int levels = 5)
    {
        _logger.LogDebug("GET book: {Ticker}, levels: {Levels}", ticker, levels);

        var live = GetTenantLiveSnapshot();
        if (live != null && ConnectorLiveMapper.TryGetTick(live, ticker, out var liveTick))
        {
            return Ok(new
            {
                ticker,
                compra = liveTick.Bid.HasValue
                    ? new[] { new { level = 1, quantity = 1, price = liveTick.Bid.Value } }
                    : Array.Empty<object>(),
                venda = liveTick.Ask.HasValue
                    ? new[] { new { level = 1, quantity = 1, price = liveTick.Ask.Value } }
                    : Array.Empty<object>(),
                source = "connector",
                timestamp = liveTick.TimestampUtc
            });
        }

        levels = Math.Clamp(levels, 1, 20);

        var compra = new List<object>();
        var venda = new List<object>();

        for (int i = 1; i <= levels; i++)
        {
            var qc = _rtdService.GetLastValue(ticker, $"QC{i}");
            var pc = _rtdService.GetLastValue(ticker, $"PC{i}");
            
            if (qc != null && pc != null)
            {
                compra.Add(new { level = i, quantity = qc, price = pc });
            }

            var qv = _rtdService.GetLastValue(ticker, $"QV{i}");
            var pv = _rtdService.GetLastValue(ticker, $"PV{i}");
            
            if (qv != null && pv != null)
            {
                venda.Add(new { level = i, quantity = qv, price = pv });
            }
        }

        if (!compra.Any() && !venda.Any())
        {
            return NotFound(new { message = $"Book não disponível para ticker '{ticker}'" });
        }

        return Ok(new
        {
            ticker,
            compra,
            venda,
            timestamp = DateTime.Now
        });
    }
}
