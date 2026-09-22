using MarketData.API.SignalR;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using System.IO;
using MarketData.API.Worker;

namespace MarketData.API.Worker.Simulator
{
    public class MockMarketDataProvider : IMarketDataProvider
    {
        private readonly IHubContext<MarketHub> _hub;
        private readonly ILogger<MockMarketDataProvider> _logger;

        private Dictionary<string, MockTicker> _data = new();
        private static bool StopMock = false;
        private List<MarketEvent> _simulationData = new();
        private System.Timers.Timer? _simulationTimer;
        private int _simulationIndex = 0;
        public MockMarketDataProvider(
            IHubContext<MarketHub> hub,
            ILogger<MockMarketDataProvider> logger)
        {
            _hub = hub;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            LoadMock();

            _logger.LogInformation("🚀 MOCK iniciado");
            StopMock = false;
            while (!ct.IsCancellationRequested)
            {
                if (StopMock)
                {
                    _logger.LogInformation("🛑 MOCK parado");
                    break;
                }

                foreach (var (ticker, tickerData) in _data)
                {
                    await EmitPrice(ticker, tickerData);
                    await EmitBook(ticker, tickerData);
                }

                await Task.Delay(500, ct); // 🔥 velocidade do mercado fake
            }
        }
        public async Task StopAsync(CancellationToken ct)
        {
            StopMock = true;
            StopSimulation();
        }

        public Task StartSimulationAsync(string date, CancellationToken ct)
        {
            LoadSimulationData(date);
            if (_simulationData.Count == 0)
            {
                _logger.LogWarning("Nenhum dado encontrado para simulação na data {Date}", date);
                return Task.CompletedTask;
            }
            StartSimulation();
            return Task.CompletedTask;
        }

        private void LoadMock()
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                "Worker",
                "Simulator",
                "mock_marketdata.json"
            );

            var json = File.ReadAllText(path);

            var raw = JsonSerializer.Deserialize<Dictionary<string, MockTicker>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;

            _data = raw;
        }

        private void LoadSimulationData(string date)
        {
            var dir = Path.Combine("c:\\liga", date);
            if (!Directory.Exists(dir))
            {
                var datafiles = Directory.GetFiles(dir).ToList();
                foreach (var file in datafiles)
                {
                    if (File.Exists(file))
                    {
                        try
                        {
                            var json = File.ReadAllText(file);
                            _simulationData = JsonSerializer.Deserialize<List<MarketEvent>>(json) ?? new List<MarketEvent>();
                            // Ordenar por timestamp
                            _simulationData = _simulationData.OrderBy(e => e.Timestamp).ToList();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erro ao carregar dados antigos de simulação para {Date}", date);
                            _simulationData.Clear();
                        }
                    }
                    else
                    {
                        _simulationData.Clear();
                    }
                }
                return;
            }

            var files = Directory.GetFiles(dir, "market_data_*.json");
            var allEvents = new List<MarketEvent>();
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var events = JsonSerializer.Deserialize<List<MarketEvent>>(json) ?? new List<MarketEvent>();
                    allEvents.AddRange(events);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao carregar arquivo {File}", file);
                }
            }
            // Ordenar por timestamp
            _simulationData = allEvents.OrderBy(e => e.Timestamp).ToList();
        }

        private void StartSimulation()
        {
            _simulationIndex = 0;
            _simulationTimer = new System.Timers.Timer(2000); // 10 segundos
            _simulationTimer.Elapsed += OnSimulationTick;
            _simulationTimer.AutoReset = true;
            _simulationTimer.Start();
            _logger.LogInformation("Simulação mock iniciada com {Count} eventos", _simulationData.Count);
        }

        private void StopSimulation()
        {
            _simulationTimer?.Stop();
            _simulationTimer?.Dispose();
            _simulationIndex = 0;
            _logger.LogInformation("Simulação mock parada");
        }

        private void OnSimulationTick(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_simulationIndex >= _simulationData.Count)
            {
                StopSimulation();
                return;
            }
            var evt = _simulationData[_simulationIndex];
            // Emitir preço
            _ = _hub.Clients.Group(evt.Ticker).SendAsync("price", evt.Ticker, evt.Price);
            _logger.LogDebug("Simulação emitindo {Ticker} {Price}", evt.Ticker, evt.Price);
            _simulationIndex++;
        }

        private int _priceIndex = 0;
        private int _bookIndex = 0;

        private async Task EmitPrice(string ticker, MockTicker data)
        {
            if (data.Prices.Count == 0) return;

            var price = data.Prices[_priceIndex % data.Prices.Count];
            _priceIndex++;

            await _hub.Clients.Group(ticker)
                .SendAsync("price", ticker, price);

            _logger.LogDebug($"[MOCK] PRICE {ticker} {price}");
        }

        public void RegisterInterest(string ticker, int niveis)
        {
            // O simulador pode apenas logar o interesse
            _logger.LogInformation("[MOCK] Interesse registrado em {T} para {N} níveis", ticker, niveis);
        }

        private async Task EmitBook(string ticker, MockTicker data)
        {
            if (data.Book.Count == 0) return;

            var book = data.Book[_bookIndex % data.Book.Count];
            _bookIndex++;

            var dto = new
            {
                Ticker = ticker,
                BestBid = book.Bids.FirstOrDefault()?.Price ?? 0,
                BestBidQty = book.Bids.FirstOrDefault()?.Qty ?? 0,
                BestAsk = book.Asks.FirstOrDefault()?.Price ?? 0,
                BestAskQty = book.Asks.FirstOrDefault()?.Qty ?? 0,
                BestBidQty5 = book.Bids.Take(5).Sum(x => x.Qty),
                BestAskQty5 = book.Asks.Take(5).Sum(x => x.Qty),
                Timestamp = DateTime.UtcNow
            };

            await _hub.Clients.Group(ticker)
                .SendAsync("book", dto);

            _logger.LogDebug($"[MOCK] BOOK {ticker}");
        }
    }
}
