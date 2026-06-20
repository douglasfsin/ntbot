// ============================================================================
// Exemplos de uso do Integrador ProfitChart
// ============================================================================

using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using NtBot.Api.Services.Profit;

namespace NtBot.Api.Examples;

/// <summary>
/// Exemplo 1: Consumir API REST para consultas pontuais
/// </summary>
public class RestApiExample
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:5053/api/profitchart";

    public RestApiExample()
    {
        _httpClient = new HttpClient 
        { 
            BaseAddress = new Uri(BaseUrl) 
        };
    }

    /// <summary>
    /// Obtém o último preço de um ticker
    /// </summary>
    public async Task<double?> GetLastPrice(string ticker)
    {
        var response = await _httpClient.GetAsync($"tickers/{ticker}/ULT");
        
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<TickerTopicResponse>();
            return data?.Value as double?;
        }

        return null;
    }

    /// <summary>
    /// Obtém snapshot completo de um ticker
    /// </summary>
    public async Task<Dictionary<string, object>?> GetSnapshot(string ticker)
    {
        var response = await _httpClient.GetAsync($"tickers/{ticker}");
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        }

        return null;
    }

    /// <summary>
    /// Verifica saúde do serviço
    /// </summary>
    public async Task<bool> IsServiceHealthy()
    {
        var response = await _httpClient.GetAsync("health");
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Obtém múltiplos preços de uma vez
    /// </summary>
    public async Task<Dictionary<string, double>> GetMultiplePrices(params string[] tickers)
    {
        var tickersParam = string.Join(",", tickers);
        var response = await _httpClient.GetAsync($"prices?tickers={tickersParam}");
        
        var result = new Dictionary<string, double>();
        
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<Dictionary<string, PriceData>>();
            
            if (data != null)
            {
                foreach (var kvp in data)
                {
                    if (kvp.Value.Price.HasValue)
                    {
                        result[kvp.Key] = kvp.Value.Price.Value;
                    }
                }
            }
        }

        return result;
    }
}

/// <summary>
/// Exemplo 2: Streaming em tempo real via SignalR
/// </summary>
public class SignalRExample : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly Dictionary<string, Action<double>> _priceCallbacks = new();

    public SignalRExample(string hubUrl = "http://localhost:5053/hubs/profitchart")
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new[] { 
                TimeSpan.Zero, 
                TimeSpan.FromSeconds(1), 
                TimeSpan.FromSeconds(5), 
                TimeSpan.FromSeconds(10) 
            })
            .Build();

        ConfigureHandlers();
    }

    private void ConfigureHandlers()
    {
        // Receber atualizações de ticks
        _connection.On<TickUpdateDto>("TickUpdate", (data) =>
        {
            Console.WriteLine($"[{data.Timestamp:HH:mm:ss}] {data.Ticker}.{data.Topic} = {data.Value}");

            // Se for preço e temos callback registrado
            if (data.Topic == "ULT" && data.Value is double price)
            {
                if (_priceCallbacks.TryGetValue(data.Ticker, out var callback))
                {
                    callback(price);
                }
            }
        });

        // Status de conexão
        _connection.On<ConnectionStatusDto>("ConnectionStatus", (data) =>
        {
            Console.WriteLine($"✓ Conectado ao hub - ID: {data.ConnectionId}");
            Console.WriteLine($"  Servidor: {data.ServerTime}");
            Console.WriteLine($"  Dados recebidos: {data.Statistics?.TotalDataReceived ?? 0}");
        });

        // Confirmação de inscrição
        _connection.On<SubscriptionDto>("SubscriptionConfirmed", (data) =>
        {
            Console.WriteLine($"✓ Inscrito em: {data.Ticker}");
        });

        // Eventos de reconexão
        _connection.Reconnecting += error =>
        {
            Console.WriteLine($"⚠️ Reconectando... {error?.Message}");
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            Console.WriteLine($"✓ Reconectado - ID: {connectionId}");
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            Console.WriteLine($"✗ Conexão fechada - {error?.Message}");
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// Conecta ao hub
    /// </summary>
    public async Task ConnectAsync()
    {
        await _connection.StartAsync();
        Console.WriteLine("✓ Conectado ao ProfitChart Hub");
    }

    /// <summary>
    /// Inscreve em um ticker específico
    /// </summary>
    public async Task SubscribeAsync(string ticker, Action<double>? onPriceUpdate = null)
    {
        if (onPriceUpdate != null)
        {
            _priceCallbacks[ticker] = onPriceUpdate;
        }

        await _connection.InvokeAsync("SubscribeTicker", ticker);
    }

    /// <summary>
    /// Inscreve em todos os tickers
    /// </summary>
    public async Task SubscribeAllAsync()
    {
        await _connection.InvokeAsync("SubscribeAll");
    }

    /// <summary>
    /// Cancela inscrição
    /// </summary>
    public async Task UnsubscribeAsync(string ticker)
    {
        _priceCallbacks.Remove(ticker);
        await _connection.InvokeAsync("UnsubscribeTicker", ticker);
    }

    /// <summary>
    /// Obtém estatísticas
    /// </summary>
    public async Task GetStatisticsAsync()
    {
        await _connection.InvokeAsync("GetStatistics");
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}

/// <summary>
/// Exemplo 3: Bot de Trading simples
/// </summary>
public class SimpleTradingBot
{
    private readonly SignalRExample _signalR;
    private double _lastPrice;
    private const double BUY_THRESHOLD = 127000;
    private const double SELL_THRESHOLD = 128000;

    public SimpleTradingBot()
    {
        _signalR = new SignalRExample();
    }

    public async Task StartAsync(string ticker)
    {
        await _signalR.ConnectAsync();

        await _signalR.SubscribeAsync(ticker, price =>
        {
            _lastPrice = price;
            AnalyzeAndTrade(ticker, price);
        });

        Console.WriteLine($"🤖 Bot iniciado para {ticker}");
        Console.WriteLine($"   Compra: < {BUY_THRESHOLD}");
        Console.WriteLine($"   Venda: > {SELL_THRESHOLD}");
    }

    private void AnalyzeAndTrade(string ticker, double price)
    {
        if (price < BUY_THRESHOLD)
        {
            Console.WriteLine($"🟢 SINAL DE COMPRA - {ticker} @ {price}");
            // Aqui você chamaria a API de execução de ordens
        }
        else if (price > SELL_THRESHOLD)
        {
            Console.WriteLine($"🔴 SINAL DE VENDA - {ticker} @ {price}");
            // Aqui você chamaria a API de execução de ordens
        }
    }
}

/// <summary>
/// Exemplo 4: Monitor multi-ticker
/// </summary>
public class MultiTickerMonitor
{
    private readonly SignalRExample _signalR;
    private readonly Dictionary<string, TickerData> _tickers = new();

    public MultiTickerMonitor()
    {
        _signalR = new SignalRExample();
    }

    public async Task StartAsync(params string[] tickers)
    {
        await _signalR.ConnectAsync();

        foreach (var ticker in tickers)
        {
            _tickers[ticker] = new TickerData { Ticker = ticker };
            
            await _signalR.SubscribeAsync(ticker, price =>
            {
                _tickers[ticker].LastPrice = price;
                _tickers[ticker].UpdateCount++;
                _tickers[ticker].LastUpdate = DateTime.Now;
            });
        }

        // Exibir estatísticas a cada 5 segundos
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(5000);
                DisplayStatistics();
            }
        });
    }

    private void DisplayStatistics()
    {
        Console.Clear();
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("     MONITOR DE MÚLTIPLOS TICKERS      ");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine($"{"Ticker",-15} {"Preço",-15} {"Updates",-10} {"Última",-20}");
        Console.WriteLine("───────────────────────────────────────");

        foreach (var kvp in _tickers)
        {
            var data = kvp.Value;
            Console.WriteLine($"{data.Ticker,-15} {data.LastPrice,-15:N2} {data.UpdateCount,-10} {data.LastUpdate:HH:mm:ss}");
        }

        Console.WriteLine("═══════════════════════════════════════");
    }

    private class TickerData
    {
        public string Ticker { get; set; } = "";
        public double LastPrice { get; set; }
        public int UpdateCount { get; set; }
        public DateTime LastUpdate { get; set; }
    }
}

// ============================================================================
// DTOs
// ============================================================================

public record TickerTopicResponse(string Ticker, string Topic, object Value, DateTime Timestamp);
public record PriceData(double? Price, DateTime Timestamp, string? Error = null);
public record TickUpdateDto(string Ticker, string Topic, object Value, DateTime Timestamp);
public record ConnectionStatusDto(bool Connected, string ConnectionId, DateTime ServerTime, RtdStatistics? Statistics);
public record SubscriptionDto(string Ticker, DateTime Timestamp, int? Count = null, List<string>? Tickers = null);

// ============================================================================
// Programa de Exemplo
// ============================================================================

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Escolha um exemplo:");
        Console.WriteLine("1. REST API - Consulta simples");
        Console.WriteLine("2. SignalR - Streaming em tempo real");
        Console.WriteLine("3. Trading Bot - Bot simples");
        Console.WriteLine("4. Multi-Ticker - Monitor múltiplos ativos");
        Console.Write("\nOpção: ");

        var option = Console.ReadLine();

        switch (option)
        {
            case "1":
                await RunRestApiExample();
                break;
            case "2":
                await RunSignalRExample();
                break;
            case "3":
                await RunTradingBotExample();
                break;
            case "4":
                await RunMultiTickerExample();
                break;
            default:
                Console.WriteLine("Opção inválida");
                break;
        }
    }

    private static async Task RunRestApiExample()
    {
        var api = new RestApiExample();

        Console.WriteLine("Verificando saúde do serviço...");
        var healthy = await api.IsServiceHealthy();
        Console.WriteLine($"Status: {(healthy ? "✓ Saudável" : "✗ Com problemas")}");

        Console.WriteLine("\nObtendo preço do WINJ25...");
        var price = await api.GetLastPrice("WINJ25");
        Console.WriteLine($"WINJ25: {price:N2}");

        Console.WriteLine("\nObtendo múltiplos preços...");
        var prices = await api.GetMultiplePrices("WINJ25", "WDOK25", "PETR4");
        foreach (var kvp in prices)
        {
            Console.WriteLine($"{kvp.Key}: {kvp.Value:N2}");
        }
    }

    private static async Task RunSignalRExample()
    {
        await using var signalR = new SignalRExample();
        await signalR.ConnectAsync();
        await signalR.SubscribeAsync("WINJ25");

        Console.WriteLine("Pressione qualquer tecla para sair...");
        Console.ReadKey();
    }

    private static async Task RunTradingBotExample()
    {
        var bot = new SimpleTradingBot();
        await bot.StartAsync("WINJ25");

        Console.WriteLine("Pressione qualquer tecla para sair...");
        Console.ReadKey();
    }

    private static async Task RunMultiTickerExample()
    {
        var monitor = new MultiTickerMonitor();
        await monitor.StartAsync("WINJ25", "WDOK25", "PETR4", "VALE3");

        Console.WriteLine("Pressione qualquer tecla para sair...");
        Console.ReadKey();
    }
}
