using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace NtBot.Api.Services.Profit
{
    public class ProfitService : IRtdService
    {
        private readonly ProfitSignalRSettings _settings;
        private HubConnection? _connection;
        private readonly Dictionary<string, object> _lastValues = new();

        public event Action<string, string, object>? OnNewTick;
        public event Action<string>? OnTickerNotFound;

        private readonly Dictionary<string, RtdTickerConfig> _logicalMap = new();
        private readonly Dictionary<string, RtdTickerConfig> _byTicker = new();

        public ProfitService(IOptions<ProfitSignalRSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task InitializeAsync(string configPath = "rtd_config.json")
        {
            LoadConfig(configPath);

            if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
            {
                Console.WriteLine("[SignalR] ProfitChart BaseUrl is not configured, skipping RTD initialization.");
                return;
            }

            _connection = new HubConnectionBuilder()
                .WithUrl(_settings.BaseUrl)
                .WithAutomaticReconnect()
                .Build();

            RegisterHandlers();

            await _connection.StartAsync();

            Console.WriteLine($"[SignalR] Conectado em {_settings.BaseUrl}");

            // Subscreve tickers
            foreach (var ticker in _byTicker.Keys)
            {
                await _connection.InvokeAsync("Subscribe", ticker);
                Console.WriteLine($"[SignalR] Subscribed: {ticker}");
            }
        }

        private void RegisterHandlers()
        {
            // ?? PREÇO
            _connection!.On<string, double>("price", (ticker, price) =>
            {
                _lastValues[ticker] = price;
                OnNewTick?.Invoke(ticker, "ULT", price);
            });

            // ?? BOOK
            _connection.On<object>("book", (data) =>
            {
                var json = JsonSerializer.Serialize(data);
                var book = JsonSerializer.Deserialize<BookDto>(json);

                if (book == null) return;

                foreach (var bid in book.Bids)
                {
                    _lastValues[book.Ticker] = bid.Price;
                    OnNewTick?.Invoke(book.Ticker, "QC", bid.Price);
                }

                foreach (var ask in book.Asks)
                {
                    _lastValues[book.Ticker] = ask.Price;
                    OnNewTick?.Invoke(book.Ticker, "QV", ask.Price);
                }
            });

            // ? NOT FOUND
            _connection.On<string>("notfound", (ticker) =>
            {
                OnTickerNotFound?.Invoke(ticker);
            });
        }

        private void LoadConfig(string path)
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<Dictionary<string, RtdTickerConfig>>(json)!;

            foreach (var kv in config)
            {
                _logicalMap[kv.Key] = kv.Value;

                if (!string.IsNullOrEmpty(kv.Value.TICK))
                    _byTicker[kv.Value.TICK] = kv.Value;
            }
        }

        // =========================
        // COMPATIBILIDADE (RTD)
        // =========================

        public string? GetAliasByTicker(string ticker)
        {
            return _logicalMap.FirstOrDefault(x => x.Value.TICK == ticker).Key;
        }

        public string? GetTicker(string logical) =>
            _logicalMap.TryGetValue(logical, out var cfg) ? cfg.TICK : null;

        public RtdTickerConfig? GetConfig(string logical) =>
            _logicalMap.TryGetValue(logical, out var cfg) ? cfg : null;

        public int GetBase(string ticker) =>
            _byTicker.FirstOrDefault(x => x.Key.Equals(ticker)).Value?.BASE ?? 1;

        public int GetContratoLimite(string ticker) =>
            _byTicker.FirstOrDefault(x => x.Key.Equals(ticker)).Value?.N_CONTRATO ?? 0;

        public RtdStatistics GetStatistics()
        {
            return new RtdStatistics
            {
                ServiceStarted = DateTime.UtcNow,
                TotalDataReceived = _lastValues.Count,
                LastDataReceived = DateTime.UtcNow,
                TotalTopicsConnected = _byTicker.Count,
                TopicsWithData = _lastValues.Count,
                DataRatePerSecond = 0,
                IsConnected = _connection?.State == HubConnectionState.Connected,
                SecondsSinceLastData = 0
            };
        }

        public Dictionary<string, TickerStatus> GetAllTickersStatus()
        {
            return _byTicker.ToDictionary(
                kvp => kvp.Key,
                kvp => new TickerStatus
                {
                    Ticker = kvp.Key,
                    LogicalName = GetAliasByTicker(kvp.Key),
                    IsReceivingData = _lastValues.ContainsKey(kvp.Key),
                    TotalTopics = 1,
                    TopicsWithData = _lastValues.ContainsKey(kvp.Key) ? 1 : 0,
                    LastUpdate = DateTime.UtcNow,
                    LastPrice = _lastValues.TryGetValue(kvp.Key, out var value) && value is double d ? d : null,
                    Volume = null
                });
        }

        public object? GetLastValue(string ticker, string topic)
        {
            if (_lastValues.TryGetValue(ticker, out var value))
            {
                return value;
            }

            return null;
        }

        public Dictionary<string, object>? GetTickerSnapshot(string ticker)
        {
            if (!_lastValues.TryGetValue(ticker, out var value))
                return null;

            return new Dictionary<string, object>
            {
                { "ULT", value }
            };
        }
    }

    public class ProfitSignalRSettings
    {
        public string BaseUrl { get; set; } = "";
        public int ReconnectIntervalSeconds { get; set; }
    }

    public class BookDto
    {
        public string Ticker { get; set; } = "";
        public List<BookLevel> Bids { get; set; } = new();
        public List<BookLevel> Asks { get; set; } = new();
    }

    public class BookLevel
    {
        public double Price { get; set; }
        public int Quantity { get; set; }
    }
}
