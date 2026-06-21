using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Dtos;
using NtBot.Connector.Windows.Providers.MT5;
using NtBot.Connector.Windows.SignalR;

namespace NtBot.Connector.Windows.Workers;

/// <summary>
/// Sincroniza OHLCV do MT5 Python local para a API (tabela Candles).
/// </summary>
public sealed class OhlcvSyncWorker : BackgroundService
{
    private readonly ConnectorOptions _connectorOptions;
    private readonly QuantConnectorOptions _quantOptions;
    private readonly Mt5PythonHost _pythonHost;
    private readonly INtBotApiClient _apiClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OhlcvSyncWorker> _logger;

    public OhlcvSyncWorker(
        IOptions<ConnectorOptions> connectorOptions,
        IOptions<QuantConnectorOptions> quantOptions,
        Mt5PythonHost pythonHost,
        INtBotApiClient apiClient,
        IHttpClientFactory httpClientFactory,
        ILogger<OhlcvSyncWorker> logger)
    {
        _connectorOptions = connectorOptions.Value;
        _quantOptions = quantOptions.Value;
        _pythonHost = pythonHost;
        _apiClient = apiClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_connectorOptions.EnableMt5 || !_quantOptions.EnableOhlcvSync)
        {
            _logger.LogInformation("OHLCV sync desabilitado (EnableMt5={Mt5}, EnableOhlcvSync={Sync})",
                _connectorOptions.EnableMt5, _quantOptions.EnableOhlcvSync);
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_pythonHost.IsRunning)
                {
                    _logger.LogDebug("MT5 Python offline — pulando sync OHLCV");
                }
                else
                {
                    await _apiClient.EnsureSessionAsync(stoppingToken);
                    await SyncOnceAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Falha no sync OHLCV");
            }

            await Task.Delay(_quantOptions.OhlcvSyncIntervalMs, stoppingToken);
        }
    }

    private async Task SyncOnceAsync(CancellationToken ct)
    {
        var baseUrl = _pythonHost.BaseUrl.TrimEnd('/');
        var client = _httpClientFactory.CreateClient(nameof(OhlcvSyncWorker));
        client.Timeout = TimeSpan.FromSeconds(30);

        foreach (var pair in _quantOptions.CandleSymbols)
        {
            if (string.IsNullOrWhiteSpace(pair.Symbol) || string.IsNullOrWhiteSpace(pair.Mt5Symbol))
                continue;

            var url =
                $"{baseUrl}/api/ohlcv/{Uri.EscapeDataString(pair.Mt5Symbol)}?timeframe={Uri.EscapeDataString(_quantOptions.Timeframe)}&count={_quantOptions.CandleCount}";

            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("OHLCV {Symbol}/{Mt5} HTTP {Status}", pair.Symbol, pair.Mt5Symbol, (int)response.StatusCode);
                continue;
            }

            var payload = await response.Content.ReadFromJsonAsync<Mt5OhlcvResponse>(cancellationToken: ct);
            if (payload?.Candles is null || payload.Candles.Count == 0)
                continue;

            var items = payload.Candles
                .Select(row => MapRow(pair.Symbol, row))
                .Where(item => item is not null)
                .Cast<CandleIngestItem>()
                .ToList();

            if (items.Count == 0)
                continue;

            await _apiClient.SendCandlesAsync(new CandleIngestBatch
            {
                Timeframe = _quantOptions.Timeframe,
                Candles = items
            }, ct);

            _logger.LogInformation("OHLCV sync {Symbol}: {Count} candles → API", pair.Symbol, items.Count);
        }
    }

    private static CandleIngestItem? MapRow(string storageSymbol, Mt5OhlcvRow row)
    {
        if (!DateTime.TryParse(row.Time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var openTime))
            return null;

        openTime = openTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(openTime, DateTimeKind.Utc)
            : openTime.ToUniversalTime();

        return new CandleIngestItem
        {
            Symbol = storageSymbol.ToUpperInvariant(),
            OpenTime = openTime,
            CloseTime = openTime,
            Open = row.Open,
            High = row.High,
            Low = row.Low,
            Close = row.Close,
            Volume = row.RealVolume > 0 ? row.RealVolume : row.TickVolume
        };
    }

    private sealed class Mt5OhlcvResponse
    {
        [JsonPropertyName("candles")]
        public List<Mt5OhlcvRow> Candles { get; set; } = [];
    }

    private sealed class Mt5OhlcvRow
    {
        [JsonPropertyName("time")]
        public string Time { get; set; } = string.Empty;

        [JsonPropertyName("open")]
        public decimal Open { get; set; }

        [JsonPropertyName("high")]
        public decimal High { get; set; }

        [JsonPropertyName("low")]
        public decimal Low { get; set; }

        [JsonPropertyName("close")]
        public decimal Close { get; set; }

        [JsonPropertyName("tick_volume")]
        public long TickVolume { get; set; }

        [JsonPropertyName("real_volume")]
        public long RealVolume { get; set; }
    }
}
