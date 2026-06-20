using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NtBot.Connector.Windows.Configuration;
using RTDTrading;

namespace NtBot.Connector.Windows.Providers.Profit;

public sealed class ProfitRtdConfigEntry
{
    public string TICK { get; set; } = string.Empty;
    public List<string>? TICKERS { get; set; }
    public int BASE { get; set; } = 1;
    public int N_CONTRATO { get; set; } = 1;
    public string? Description { get; set; }
    public string? AssetType { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Cliente COM RTD do ProfitChart — deve rodar em thread STA.
/// </summary>
public sealed class ProfitRtdComClient : IDisposable
{
    private static readonly string[] DefaultTopics = ["ULT", "QC", "QV", "VOL"];

    private readonly ILogger _logger;
    private readonly string _configPath;
    private readonly ConcurrentDictionary<string, decimal> _lastPrices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, (string Logical, string Ticker, string Topic)> _topics = new();
    private readonly object _sync = new();

    private IRtdServer? _server;
    private ProfitRtdUpdateSink? _sink;
    private int _nextTopicId = 1;
    private volatile bool _refreshPending;
    private bool _started;
    private int _dataCount;

    public ProfitRtdComClient(string configPath, ILogger logger)
    {
        _configPath = configPath;
        _logger = logger;
    }

    public bool IsConnected { get; private set; }
    public int DataCount => _dataCount;
    public DateTime? LastDataUtc { get; private set; }
    public string? LastError { get; private set; }

    public IReadOnlyDictionary<string, decimal> LastPrices => _lastPrices;

    public void Start()
    {
        lock (_sync)
        {
            if (_started)
                return;

            _server = CreateServer();
            _sink = new ProfitRtdUpdateSink(() => _refreshPending = true);

            var result = _server.ServerStart(_sink);
            if (result <= 0)
                throw new InvalidOperationException($"Profit RTD ServerStart retornou {result}");

            ConnectConfiguredTopics();
            _started = true;
            IsConnected = true;
            _logger.LogInformation("Profit RTD COM conectado — {Topics} tópicos", _topics.Count);
        }
    }

    public void Poll()
    {
        if (!_started || _server == null)
            return;

        try
        {
            var topicCount = 0;
            var raw = _server.RefreshData(ref topicCount);
            if (raw is not Array data || topicCount <= 0)
                return;

            ParseRefreshData(data, topicCount);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _logger.LogWarning(ex, "Erro ao RefreshData RTD");
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_server == null)
                return;

            try
            {
                foreach (var topicId in _topics.Keys.ToList())
                {
                    try { _server.DisconnectData(topicId); }
                    catch { /* ignore */ }
                }

                _server.ServerTerminate();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro ao encerrar RTD COM");
            }
            finally
            {
                _server = null;
                _sink = null;
                _started = false;
                IsConnected = false;
            }
        }
    }

    private IRtdServer CreateServer()
    {
        try
        {
            var progIdType = Type.GetTypeFromProgID("RTDTrading.RtdServer");
            if (progIdType != null)
            {
                var instance = Activator.CreateInstance(progIdType);
                if (instance is IRtdServer fromProgId)
                {
                    _logger.LogInformation("RTD via ProgID RTDTrading.RtdServer");
                    return fromProgId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ProgID RTDTrading.RtdServer indisponível, usando interop");
        }

        return new RtdServerClass();
    }

    private void ConnectConfiguredTopics()
    {
        var entries = LoadConfig();
        foreach (var (logical, entry) in entries)
        {
            if (!entry.IsActive)
                continue;

            var tickers = (entry.TICKERS ?? [])
                .Prepend(entry.TICK)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tickers.Count == 0 && !string.IsNullOrWhiteSpace(entry.TICK))
                tickers.Add(entry.TICK);

            foreach (var ticker in tickers)
            {
                foreach (var topic in DefaultTopics)
                    ConnectTopic(logical, ticker, topic);
            }
        }
    }

    private void ConnectTopic(string logical, string ticker, string topic)
    {
        if (_server == null)
            return;

        var topicId = _nextTopicId++;
        Array parameters = new object[] { ticker, topic };
        var getNewValues = true;

        try
        {
            _server.ConnectData(topicId, ref parameters, ref getNewValues);
            _topics[topicId] = (logical, ticker, topic);
            _logger.LogInformation("RTD connect {Ticker}.{Topic} (id={Id}, logical={Logical})", ticker, topic, topicId, logical);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha RTD connect {Ticker}.{Topic}", ticker, topic);
        }
    }

    private Dictionary<string, ProfitRtdConfigEntry> LoadConfig()
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogWarning("rtd_config.json não encontrado: {Path}", _configPath);
            return new Dictionary<string, ProfitRtdConfigEntry>();
        }

        var json = File.ReadAllText(_configPath);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("tickers", out var legacy) && legacy.ValueKind == JsonValueKind.Array)
        {
            _logger.LogWarning("rtd_config.json legado (array tickers) — use formato WIN/WDO com TICK");
            return new Dictionary<string, ProfitRtdConfigEntry>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, ProfitRtdConfigEntry>>(json)
               ?? new Dictionary<string, ProfitRtdConfigEntry>();
    }

    private void ParseRefreshData(Array data, int topicCount)
    {
        // RefreshData retorna matriz 2 x N: [0,i]=topicId, [1,i]=valor
        for (var i = 0; i < topicCount; i++)
        {
            var topicIdObj = data.GetValue(0, i);
            var valueObj = data.GetValue(1, i);
            if (topicIdObj == null || valueObj == null)
                continue;

            var topicId = Convert.ToInt32(topicIdObj);
            if (!_topics.TryGetValue(topicId, out var meta))
                continue;

            if (meta.Topic != "ULT" && meta.Topic != "QC" && meta.Topic != "QV")
                continue;

            if (!TryToDecimal(valueObj, out var price) || price <= 0)
                continue;

            _lastPrices[meta.Logical] = price;
            _dataCount++;
            LastDataUtc = DateTime.UtcNow;
            LastError = null;
            IsConnected = true;
        }
    }

    private static bool TryToDecimal(object value, out decimal result)
    {
        result = 0;
        try
        {
            result = Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
