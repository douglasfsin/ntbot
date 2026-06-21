using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using NtBot.Connector.Windows.Dtos;
using NtBot.Connector.Windows.Providers.MT5;
using NtBot.Connector.Windows.SignalR;
using NtBot.Shared.MarketData;

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

        if (_quantOptions.StartupDelayMs > 0)
            await Task.Delay(_quantOptions.StartupDelayMs, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_pythonHost.IsRunning)
                {
                    _logger.LogWarning("MT5 Python offline — OHLCV sync adiado");
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

        var pairs = BuildSymbolPairs();
        var syncedStorage = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var syncedAny = false;
        var timeframes = ResolveTimeframes();

        foreach (var pair in pairs)
        {
            if (string.IsNullOrWhiteSpace(pair.StorageSymbol))
                continue;

            if (!syncedStorage.Add(pair.StorageSymbol))
                continue;

            foreach (var timeframe in timeframes)
            {
                var fetched = await TryFetchCandlesAsync(client, baseUrl, pair, timeframe, ct);
                if (fetched is null || fetched.Count == 0)
                {
                    _logger.LogWarning(
                        "OHLCV indisponível para {Storage}/{Tf} — tentou MT5: {Candidates}",
                        pair.StorageSymbol,
                        timeframe,
                        string.Join(", ", pair.Mt5Candidates));
                    continue;
                }

                await _apiClient.SendCandlesAsync(new CandleIngestBatch
                {
                    Timeframe = timeframe,
                    Candles = fetched
                }, ct);

                syncedAny = true;
                _logger.LogInformation(
                    "OHLCV sync {Storage}/{Tf} via {Mt5}: {Count} candles → API",
                    pair.StorageSymbol,
                    timeframe,
                    pair.ResolvedMt5Symbol,
                    fetched.Count);
            }
        }

        if (!syncedAny)
        {
            _logger.LogWarning(
                "Nenhum candle OHLCV sincronizado. Verifique símbolos em {Path} e se o MT5 está conectado.",
                _connectorOptions.Mt5ConfigPath);
        }
    }

    private IReadOnlyList<ResolvedSymbolPair> BuildSymbolPairs()
    {
        var mt5Config = Mt5ConfigLoader.Load(
            AppContext.BaseDirectory,
            _connectorOptions.Mt5ConfigPath,
            _connectorOptions);

        if (mt5Config.Symbols.Count == 0)
        {
            _logger.LogWarning(
                "Nenhum símbolo em {Path} — OHLCV sync ignorado",
                _connectorOptions.Mt5ConfigPath);
            return [];
        }

        return mt5Config.Symbols
            .Select(symbol => new ResolvedSymbolPair(symbol, [symbol]))
            .ToList();
    }

    private IReadOnlyList<string> ResolveTimeframes()
    {
        var list = (_quantOptions.Timeframes?.Count > 0
                ? _quantOptions.Timeframes
                : [_quantOptions.Timeframe])
            .Where(tf => !string.IsNullOrWhiteSpace(tf))
            .Select(tf => ChartTimeframe.Normalize(tf))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return list.Count > 0 ? list : ["M5"];
    }

    private async Task<List<CandleIngestItem>?> TryFetchCandlesAsync(
        HttpClient client,
        string baseUrl,
        ResolvedSymbolPair pair,
        string timeframe,
        CancellationToken ct)
    {
        foreach (var mt5Symbol in pair.Mt5Candidates)
        {
            var url =
                $"{baseUrl}/api/ohlcv/{Uri.EscapeDataString(mt5Symbol)}?timeframe={Uri.EscapeDataString(timeframe)}&count={_quantOptions.CandleCount}";

            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("OHLCV {Storage}/{Mt5} HTTP {Status}", pair.StorageSymbol, mt5Symbol, (int)response.StatusCode);
                continue;
            }

            var payload = await response.Content.ReadFromJsonAsync<Mt5OhlcvResponse>(cancellationToken: ct);
            if (payload?.Candles is null || payload.Candles.Count == 0)
                continue;

            var items = payload.Candles
                .Select(row => MapRow(pair.StorageSymbol, row))
                .Where(item => item is not null)
                .Cast<CandleIngestItem>()
                .ToList();

            if (items.Count == 0)
                continue;

            pair.ResolvedMt5Symbol = mt5Symbol;
            return items;
        }

        return null;
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
            Symbol = CandleSymbolAliases.Canonical(storageSymbol),
            OpenTime = openTime,
            CloseTime = openTime,
            Open = row.Open,
            High = row.High,
            Low = row.Low,
            Close = row.Close,
            Volume = row.RealVolume > 0 ? row.RealVolume : row.TickVolume
        };
    }

    private sealed class ResolvedSymbolPair(string storageSymbol, List<string> mt5Candidates)
    {
        public string StorageSymbol { get; } = storageSymbol;
        public List<string> Mt5Candidates { get; } = mt5Candidates;
        public string? ResolvedMt5Symbol { get; set; }
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
