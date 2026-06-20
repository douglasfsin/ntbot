using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Core;
using NtBot.Shared.Normalized;

namespace NtBot.Connector.Windows.Providers.NinjaTrader;

public class NinjaTraderProvider : IBrokerPlugin
{
    private readonly HttpClient _http;
    private readonly ILogger<NinjaTraderProvider> _logger;
    private bool _connected;

    public NinjaTraderProvider(HttpClient http, IOptions<ConnectorOptions> options, ILogger<NinjaTraderProvider> logger)
    {
        _http = http;
        _logger = logger;
        _http.BaseAddress = new Uri(options.Value.NinjaTraderBaseUrl.TrimEnd('/') + "/");
    }

    public BrokerSource Source => BrokerSource.NinjaTrader;
    public string Name => "NinjaTrader";
    public bool IsConnected => _connected;

    public event Action<NormalizedMarketTick>? OnTick;
    public event Action<NormalizedBrokerStatus>? OnStatusChanged;

    public async Task ConnectAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync("health", ct);
            _connected = response.IsSuccessStatusCode;
            OnStatusChanged?.Invoke(Status(_connected ? "connected" : "disconnected"));
        }
        catch (Exception ex)
        {
            _connected = false;
            _logger.LogDebug(ex, "NinjaTrader indisponível");
            OnStatusChanged?.Invoke(Status("disconnected", ex.Message));
        }
    }

    public Task DisconnectAsync(CancellationToken ct)
    {
        _connected = false;
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<NormalizedPosition>> GetPositionsAsync(CancellationToken ct)
    {
        if (!_connected) return [];

        try
        {
            var positions = await _http.GetFromJsonAsync<List<NinjaPositionDto>>("positions", ct);
            return positions?.Select(p => new NormalizedPosition
            {
                Symbol = p.Symbol,
                Source = BrokerSource.NinjaTrader,
                Side = p.Quantity >= 0 ? NormalizedSide.Buy : NormalizedSide.Sell,
                Quantity = Math.Abs(p.Quantity),
                AveragePrice = p.AveragePrice,
                UnrealizedPnL = p.UnrealizedPnL,
                RealizedPnL = 0,
                TimestampUtc = DateTime.UtcNow
            }).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    public Task<IReadOnlyList<NormalizedOrder>> GetOpenOrdersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedOrder>>([]);

    public Task<IReadOnlyList<NormalizedOrder>> GetOrdersAsync(CancellationToken ct) =>
        GetOpenOrdersAsync(ct);

    public Task<IReadOnlyList<NormalizedExecution>> GetRecentExecutionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NormalizedExecution>>([]);

    public Task<NormalizedAccount?> GetAccountAsync(CancellationToken ct) =>
        Task.FromResult<NormalizedAccount?>(null);

    private static NormalizedBrokerStatus Status(string status, string? message = null) => new()
    {
        Source = BrokerSource.NinjaTrader,
        IsConnected = status == "connected",
        Status = status,
        Message = message,
        TimestampUtc = DateTime.UtcNow
    };

    private sealed class NinjaPositionDto
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal UnrealizedPnL { get; set; }
    }
}
