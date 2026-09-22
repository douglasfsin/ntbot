using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NtBot.Api.Hubs;
using NtBot.MarketData.Configuration;
using NtBot.MarketData.Parsing;
using NtBot.Shared.MarketData;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace NtBot.Api.Services.Profit;

/// <summary>
/// Bridge SignalR para MarketData.API (ProfitDLL) — WINFUT, DOLFUT, WDOFUT.
/// Opcional: charts XAUUSD/MT5 e DB não dependem deste hub.
/// </summary>
public sealed class ProfitService : IRtdService
{
    private readonly MarketDataApiOptions _options;
    private readonly IHubContext<ProfitChartHub> _hubContext;
    private readonly ILogger<ProfitService> _logger;

    private HubConnection? _connection;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object>> _snapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TickerRuntime> _tickers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RtdTickerConfig> _logicalMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _statsLock = new();
    private RtdStatistics _stats = new() { ServiceStarted = DateTime.UtcNow };
    private int _reconnectAttempt;
    private DateTime _nextReconnectUtc = DateTime.MinValue;
    private DateTime _unreachableUntilUtc = DateTime.MinValue;
    private bool _offlineAnnounced;

    public ProfitService(
        IOptions<MarketDataApiOptions> options,
        IHubContext<ProfitChartHub> hubContext,
        ILogger<ProfitService> logger)
    {
        _options = options.Value;
        _hubContext = hubContext;
        _logger = logger;
    }

    public event Action<string, string, object>? OnNewTick;

    public async Task InitializeAsync(string configPath = "rtd_config.json")
    {
        LoadConfig(configPath);

        foreach (var ticker in _options.Tickers.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            var key = ticker.Trim().ToUpperInvariant();
            _tickers.TryAdd(key, new TickerRuntime(key, CandleSymbolAliases.Canonical(key)));
        }

        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogInformation("ProfitService: MarketDataApi desabilitado ou sem BaseUrl");
            return;
        }

        if (DateTime.UtcNow < _unreachableUntilUtc)
            return;

        var hubUrl = $"{_options.BaseUrl.TrimEnd('/')}/marketHub";
        WarnIfHttpAgainstHttpsPort(hubUrl);

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, o => o.HttpMessageHandlerFactory = _ => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<string, string, object>("tick", OnTickReceived);
        _connection.On<string, double>("price", OnPriceReceived);
        _connection.On<object>("book", OnBookReceived);

        _connection.Reconnected += _ =>
        {
            MarkOnline();
            return ResubscribeAsync();
        };
        _connection.Closed += async ex =>
        {
            if (ex is not null)
                LogOffline(ex, "ProfitService desconectado do marketHub");
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.ReconnectSeconds)));
        };

        try
        {
            await _connection.StartAsync();
            await ResubscribeAsync();
            MarkOnline();
            _logger.LogInformation("ProfitService conectado em {HubUrl} — tickers={Count}", hubUrl, _tickers.Count);
        }
        catch (Exception ex)
        {
            OnConnectFailure(ex);
            LogOffline(ex, "ProfitService não conectou ao marketHub (MarketData.API offline?)");
        }
    }

    public async Task EnsureHealthyAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BaseUrl))
            return;

        if (DateTime.UtcNow < _unreachableUntilUtc)
            return;

        if (_connection is null)
        {
            if (DateTime.UtcNow < _nextReconnectUtc)
                return;

            await InitializeAsync();
            return;
        }

        if (_connection.State == HubConnectionState.Connected)
        {
            MarkOnline();
            await ResubscribeStaleAsync(cancellationToken);
            return;
        }

        if (_connection.State != HubConnectionState.Disconnected)
            return;

        if (DateTime.UtcNow < _nextReconnectUtc)
            return;

        try
        {
            await _connection.StartAsync(cancellationToken);
            await ResubscribeAsync();
            MarkOnline();
            _logger.LogInformation("ProfitService reconectado ao marketHub");
        }
        catch (Exception ex)
        {
            OnConnectFailure(ex);
            LogOffline(
                ex,
                "ProfitService marketHub indisponível — backoff {Delay}s (B3 ticks offline; charts MT5/DB seguem)");
        }
    }

    private async Task ResubscribeStaleAsync(CancellationToken cancellationToken)
    {
        var stale = _tickers.Values
            .Where(t => !t.LastUpdate.HasValue || (DateTime.UtcNow - t.LastUpdate.Value).TotalSeconds > 60)
            .Select(t => t.Ticker)
            .ToList();

        if (stale.Count == 0 || _connection?.State != HubConnectionState.Connected)
            return;

        foreach (var ticker in stale)
        {
            await _connection.InvokeAsync("Subscribe", ticker, 0, cancellationToken);
            _logger.LogDebug("Re-assinado ticker stale: {Ticker}", ticker);
        }
    }

    private void MarkOnline()
    {
        _reconnectAttempt = 0;
        _nextReconnectUtc = DateTime.MinValue;
        _unreachableUntilUtc = DateTime.MinValue;
        _offlineAnnounced = false;
    }

    private void OnConnectFailure(Exception ex)
    {
        ScheduleBackoff();

        if (!IsExpectedOffline(ex))
            return;

        var skipMinutes = Math.Max(0, _options.UnreachableSkipMinutes);
        if (skipMinutes <= 0)
            return;

        // Cap exponential backoff alone can still retry often while MarketData is stopped for VS rebuilds.
        var skipUntil = DateTime.UtcNow.AddMinutes(skipMinutes);
        if (skipUntil > _nextReconnectUtc)
            _nextReconnectUtc = skipUntil;
        _unreachableUntilUtc = skipUntil;
    }

    private void ScheduleBackoff()
    {
        var baseSeconds = Math.Max(5, _options.ReconnectSeconds);
        var delaySeconds = Math.Min(300, baseSeconds * (1 << Math.Min(_reconnectAttempt, 5)));
        _reconnectAttempt++;
        _nextReconnectUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
    }

    private void LogOffline(Exception ex, string messageTemplate)
    {
        var delay = Math.Max(0, (int)(_nextReconnectUtc - DateTime.UtcNow).TotalSeconds);
        var expected = IsExpectedOffline(ex);

        if (!_offlineAnnounced)
        {
            _offlineAnnounced = true;
            if (messageTemplate.Contains("{Delay}", StringComparison.Ordinal))
                _logger.LogWarning(ex, messageTemplate, delay);
            else
                _logger.LogWarning(ex, "{Message} (próxima tentativa em {Delay}s)", messageTemplate, delay);
            return;
        }

        // Expected when MarketData.API is stopped/crashing negotiate — Debug only after first Warning.
        if (expected)
        {
            if (messageTemplate.Contains("{Delay}", StringComparison.Ordinal))
                _logger.LogDebug(ex, messageTemplate, delay);
            else
                _logger.LogDebug(ex, "{Message} (próxima tentativa em {Delay}s)", messageTemplate, delay);
            return;
        }

        _logger.LogWarning(ex, "{Message} (próxima tentativa em {Delay}s)", messageTemplate, delay);
    }

    private static bool IsExpectedOffline(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is HttpRequestException http)
            {
                var msg = http.Message;
                if (msg.Contains("ResponseEnded", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("ended prematurely", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("connection refused", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Connection reset", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            if (e is SocketException sock
                && sock.SocketErrorCode is SocketError.ConnectionRefused
                    or SocketError.TimedOut
                    or SocketError.HostUnreachable
                    or SocketError.NetworkUnreachable
                    or SocketError.ConnectionReset)
                return true;

            if (e is IOException io
                && (io.Message.Contains("transport connection", StringComparison.OrdinalIgnoreCase)
                    || io.Message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase)
                    || io.Message.Contains("unexpectedly", StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private void WarnIfHttpAgainstHttpsPort(string hubUrl)
    {
        if (!Uri.TryCreate(hubUrl, UriKind.Absolute, out var uri))
            return;

        if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && uri.Port is 5242)
        {
            _logger.LogWarning(
                "MarketDataApi:BaseUrl usa http:// na porta 5242, mas MarketData.API escuta HTTPS. " +
                "Use https://localhost:5242 (HTTP causa ResponseEnded / connection closed).");
        }
    }

    private async Task ResubscribeAsync()
    {
        if (_connection?.State != HubConnectionState.Connected)
            return;

        foreach (var ticker in _tickers.Keys)
        {
            await _connection.InvokeAsync("Subscribe", ticker, 0);
            _logger.LogDebug("Assinado marketHub — {Ticker}", ticker);
        }
    }

    private void OnTickReceived(string ticker, string topic, object value)
    {
        if (!ProfitDllTopicParser.TryParseTick(ticker, topic, value, out var tick))
            return;

        ApplyTick(tick.Ticker, tick.Topic, tick.Value, tick.TimestampUtc);
    }

    private void OnPriceReceived(string ticker, double price) =>
        ApplyTick(ticker, "ULT", price, DateTime.UtcNow);

    private void OnBookReceived(object data)
    {
        var json = JsonSerializer.Serialize(data);
        var book = JsonSerializer.Deserialize<BookDto>(json);
        if (book is null || string.IsNullOrWhiteSpace(book.Ticker))
            return;

        var topics = _snapshots.GetOrAdd(book.Ticker, _ => new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        topics["BOOK"] = data;
        BumpStats();
    }

    private void ApplyTick(string ticker, string topic, double value, DateTime timestampUtc)
    {
        EnsureTicker(ticker);
        var topics = _snapshots.GetOrAdd(ticker, _ => new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        topics[topic] = value;

        if (ProfitDllTopicParser.IsPriceTopic(topic))
            topics["ULT"] = value;

        if (_tickers.TryGetValue(ticker, out var runtime))
        {
            runtime.LastUpdate = timestampUtc;
            if (ProfitDllTopicParser.IsPriceTopic(topic))
                runtime.LastPrice = value;
        }

        BumpStats();
        OnNewTick?.Invoke(ticker, topic, value);
        _ = ProfitChartHub.BroadcastTickData(_hubContext, ticker, topic, value);
    }

    private void LoadConfig(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<Dictionary<string, RtdTickerConfig>>(json);
            if (config is null)
                return;

            foreach (var kv in config)
            {
                _logicalMap[kv.Key] = kv.Value;
                if (!string.IsNullOrWhiteSpace(kv.Value.TICK))
                    _tickers.TryAdd(kv.Value.TICK.ToUpperInvariant(), new TickerRuntime(kv.Value.TICK, kv.Key));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar {ConfigPath}", path);
        }
    }

    private void EnsureTicker(string ticker)
    {
        _tickers.TryAdd(ticker, new TickerRuntime(ticker, CandleSymbolAliases.Canonical(ticker)));
    }

    public string? GetAliasByTicker(string ticker) =>
        _tickers.TryGetValue(ticker, out var runtime) ? runtime.LogicalName : _logicalMap.FirstOrDefault(x => x.Value.TICK == ticker).Key;

    public string? GetTicker(string logical)
    {
        if (_logicalMap.TryGetValue(logical, out var cfg) && !string.IsNullOrWhiteSpace(cfg.TICK))
            return cfg.TICK;

        return _tickers.Values.FirstOrDefault(t =>
            t.LogicalName.Equals(logical, StringComparison.OrdinalIgnoreCase)
            || t.Ticker.Equals(logical, StringComparison.OrdinalIgnoreCase))?.Ticker;
    }

    public RtdTickerConfig? GetConfig(string logical) =>
        _logicalMap.TryGetValue(logical, out var cfg) ? cfg : null;

    public int GetBase(string ticker) =>
        ticker.StartsWith("WIN", StringComparison.OrdinalIgnoreCase) ? 1 : 5;

    public int GetContratoLimite(string ticker) =>
        _logicalMap.Values.FirstOrDefault(c => c.TICK == ticker)?.N_CONTRATO ?? 1;

    public RtdStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            var copy = new RtdStatistics
            {
                TotalDataReceived = _stats.TotalDataReceived,
                LastDataReceived = _stats.LastDataReceived,
                ServiceStarted = _stats.ServiceStarted,
                TotalTopicsConnected = Math.Max(_stats.TotalTopicsConnected, _tickers.Count),
                TopicsWithData = _stats.TopicsWithData,
                DataRatePerSecond = _stats.DataRatePerSecond,
                IsConnected = _connection?.State == HubConnectionState.Connected
            };

            if (copy.LastDataReceived != default)
                copy.SecondsSinceLastData = (DateTime.UtcNow - copy.LastDataReceived).TotalSeconds;

            copy.IsConnected = copy.IsConnected && copy.SecondsSinceLastData < 30;
            return copy;
        }
    }

    public Dictionary<string, TickerStatus> GetAllTickersStatus()
    {
        var result = new Dictionary<string, TickerStatus>(StringComparer.OrdinalIgnoreCase);

        foreach (var runtime in _tickers.Values)
        {
            _snapshots.TryGetValue(runtime.Ticker, out var topics);
            var topicsWithData = topics?.Count ?? 0;
            var receiving = runtime.LastUpdate.HasValue
                && (DateTime.UtcNow - runtime.LastUpdate.Value).TotalSeconds < 30;

            result[runtime.Ticker] = new TickerStatus
            {
                Ticker = runtime.Ticker,
                LogicalName = runtime.LogicalName,
                IsReceivingData = receiving,
                TotalTopics = Math.Max(topicsWithData, 1),
                TopicsWithData = topicsWithData,
                LastUpdate = runtime.LastUpdate,
                LastPrice = runtime.LastPrice,
                Volume = TryGetDouble(topics, "VOL")
            };
        }

        return result;
    }

    public object? GetLastValue(string ticker, string topic)
    {
        if (!_snapshots.TryGetValue(ticker, out var topics))
            return null;

        return topics.TryGetValue(topic, out var value) ? value : null;
    }

    public Dictionary<string, object>? GetTickerSnapshot(string ticker) =>
        _snapshots.TryGetValue(ticker, out var topics)
            ? topics.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            : null;

    private void BumpStats()
    {
        lock (_statsLock)
        {
            _stats.TotalDataReceived++;
            _stats.LastDataReceived = DateTime.UtcNow;
            _stats.TopicsWithData = _snapshots.Values.Sum(d => d.Count);

            var elapsed = (DateTime.UtcNow - _stats.ServiceStarted).TotalSeconds;
            if (elapsed > 0)
                _stats.DataRatePerSecond = _stats.TotalDataReceived / elapsed;
        }
    }

    private static double? TryGetDouble(ConcurrentDictionary<string, object>? topics, string key)
    {
        if (topics is null || !topics.TryGetValue(key, out var value))
            return null;

        return value switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            long l => l,
            _ => null
        };
    }

    private sealed class TickerRuntime(string ticker, string logicalName)
    {
        public string Ticker { get; } = ticker;
        public string LogicalName { get; } = logicalName;
        public DateTime? LastUpdate { get; set; }
        public double? LastPrice { get; set; }
    }

    private sealed class BookDto
    {
        public string Ticker { get; set; } = string.Empty;
        public List<BookLevel> Bids { get; set; } = [];
        public List<BookLevel> Asks { get; set; } = [];
    }

    private sealed class BookLevel
    {
        public double Price { get; set; }
        public int Quantity { get; set; }
    }
}
