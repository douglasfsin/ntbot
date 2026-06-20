using Microsoft.AspNetCore.SignalR;
using NtBot.Api.Services.Profit;

namespace NtBot.Api.Hubs;

/// <summary>
/// Hub SignalR para streaming de dados em tempo real do ProfitChart
/// Permite que clientes se inscrevam em tickers específicos e recebam atualizações
/// </summary>
public class ProfitChartHub : Hub
{
    private readonly IRtdService _rtdService;
    private readonly ILogger<ProfitChartHub> _logger;

    // Mapeamento de connectionId -> lista de tickers inscritos
    private static readonly Dictionary<string, HashSet<string>> _subscriptions = new();
    private static readonly object _lock = new();

    public ProfitChartHub(IRtdService rtdService, ILogger<ProfitChartHub> logger)
    {
        _rtdService = rtdService;
        _logger = logger;
    }

    /// <summary>
    /// Evento executado quando um cliente se conecta
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("[ProfitChart Hub] Cliente conectado: {ConnectionId}", Context.ConnectionId);
        
        lock (_lock)
        {
            _subscriptions[Context.ConnectionId] = new HashSet<string>();
        }

        // Envia status inicial
        var stats = _rtdService.GetStatistics();
        await Clients.Caller.SendAsync("ConnectionStatus", new
        {
            Connected = true,
            ConnectionId = Context.ConnectionId,
            ServerTime = DateTime.Now,
            Statistics = stats
        });

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Evento executado quando um cliente se desconecta
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("[ProfitChart Hub] Cliente desconectado: {ConnectionId}", Context.ConnectionId);
        
        lock (_lock)
        {
            _subscriptions.Remove(Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Cliente se inscreve para receber dados de um ticker específico
    /// </summary>
    /// <param name="ticker">Nome do ticker (ex: WDOK26)</param>
    public async Task SubscribeTicker(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            await Clients.Caller.SendAsync("Error", "Ticker inválido");
            return;
        }

        lock (_lock)
        {
            if (_subscriptions.TryGetValue(Context.ConnectionId, out var tickers))
            {
                tickers.Add(ticker.ToUpperInvariant());
            }
        }

        _logger.LogInformation("[ProfitChart Hub] Cliente {ConnectionId} inscrito em {Ticker}", 
            Context.ConnectionId, ticker);

        // Envia snapshot atual do ticker
        var snapshot = _rtdService.GetTickerSnapshot(ticker);
        if (snapshot != null)
        {
            await Clients.Caller.SendAsync("TickerSnapshot", new
            {
                Ticker = ticker,
                Data = snapshot,
                Timestamp = DateTime.Now
            });
        }

        await Clients.Caller.SendAsync("SubscriptionConfirmed", new
        {
            Ticker = ticker,
            Timestamp = DateTime.Now
        });
    }

    /// <summary>
    /// Cliente cancela inscrição de um ticker
    /// </summary>
    /// <param name="ticker">Nome do ticker</param>
    public async Task UnsubscribeTicker(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return;
        }

        lock (_lock)
        {
            if (_subscriptions.TryGetValue(Context.ConnectionId, out var tickers))
            {
                tickers.Remove(ticker.ToUpperInvariant());
            }
        }

        _logger.LogInformation("[ProfitChart Hub] Cliente {ConnectionId} cancelou inscrição de {Ticker}", 
            Context.ConnectionId, ticker);

        await Clients.Caller.SendAsync("UnsubscriptionConfirmed", new
        {
            Ticker = ticker,
            Timestamp = DateTime.Now
        });
    }

    /// <summary>
    /// Cliente se inscreve para receber todos os tickers disponíveis
    /// </summary>
    public async Task SubscribeAll()
    {
        var allStatus = _rtdService.GetAllTickersStatus();
        
        lock (_lock)
        {
            if (_subscriptions.TryGetValue(Context.ConnectionId, out var tickers))
            {
                foreach (var ticker in allStatus.Keys)
                {
                    tickers.Add(ticker.ToUpperInvariant());
                }
            }
        }

        _logger.LogInformation("[ProfitChart Hub] Cliente {ConnectionId} inscrito em TODOS os tickers ({Count})", 
            Context.ConnectionId, allStatus.Count);

        await Clients.Caller.SendAsync("SubscriptionConfirmed", new
        {
            Ticker = "ALL",
            Count = allStatus.Count,
            Tickers = allStatus.Keys.ToList(),
            Timestamp = DateTime.Now
        });
    }

    /// <summary>
    /// Obtém estatísticas do servidor RTD
    /// </summary>
    public async Task GetStatistics()
    {
        var stats = _rtdService.GetStatistics();
        await Clients.Caller.SendAsync("Statistics", stats);
    }

    /// <summary>
    /// Obtém status de todos os tickers
    /// </summary>
    public async Task GetAllTickersStatus()
    {
        var status = _rtdService.GetAllTickersStatus();
        await Clients.Caller.SendAsync("AllTickersStatus", status);
    }

    /// <summary>
    /// Obtém snapshot de um ticker específico
    /// </summary>
    /// <param name="ticker">Nome do ticker</param>
    public async Task GetTickerSnapshot(string ticker)
    {
        var snapshot = _rtdService.GetTickerSnapshot(ticker);
        
        if (snapshot != null)
        {
            await Clients.Caller.SendAsync("TickerSnapshot", new
            {
                Ticker = ticker,
                Data = snapshot,
                Timestamp = DateTime.Now
            });
        }
        else
        {
            await Clients.Caller.SendAsync("Error", $"Ticker '{ticker}' não encontrado");
        }
    }

    /// <summary>
    /// Método chamado pelo RTDService para distribuir dados aos clientes inscritos
    /// </summary>
    public static async Task BroadcastTickData(IHubContext<ProfitChartHub> hubContext, string ticker, string topic, object value)
    {
        var tickerUpper = ticker.ToUpperInvariant();
        var subscribedConnections = new List<string>();

        lock (_lock)
        {
            foreach (var sub in _subscriptions)
            {
                if (sub.Value.Contains(tickerUpper))
                {
                    subscribedConnections.Add(sub.Key);
                }
            }
        }

        if (subscribedConnections.Any())
        {
            await hubContext.Clients.Clients(subscribedConnections).SendAsync("TickUpdate", new
            {
                Ticker = ticker,
                Topic = topic,
                Value = value,
                Timestamp = DateTime.Now
            });
        }
    }
}
