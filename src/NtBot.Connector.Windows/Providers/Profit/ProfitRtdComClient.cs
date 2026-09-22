using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
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

public sealed class ProfitRtdQuote
{
    public decimal? Last { get; set; }
    public decimal? Bid { get; set; }
    public decimal? Ask { get; set; }
    public long? Volume { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// Cliente COM RTD do ProfitChart — deve rodar em thread STA.
/// </summary>
public sealed class ProfitRtdComClient : IDisposable
{
    // ULT = último; FEC = fechamento (útil fora do pregão); QC/QV = bid/ask no Profit.
    private static readonly string[] DefaultTopics = ["ULT", "FEC", "QC", "QV", "VOL"];

    private readonly ILogger _logger;
    private readonly string _configPath;
    private readonly ConcurrentDictionary<string, decimal> _lastPrices = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ProfitRtdQuote> _quotes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, (string Logical, string Ticker, string Topic)> _topics = new();
    private readonly object _sync = new();
    private readonly HashSet<string> _connectedKeys = new(StringComparer.OrdinalIgnoreCase);

    private IRtdServer? _server;
    private ProfitRtdUpdateSink? _sink;
    private int _nextTopicId = 1;
    private volatile bool _refreshPending;
    private bool _started;
    private int _dataCount;
    private int _pollCount;
    private int _emptyRefreshCount;
    private DateTime _lastDiagUtc = DateTime.MinValue;
    private DateTime _lastHeartbeatUtc = DateTime.MinValue;

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

    public IReadOnlyDictionary<string, ProfitRtdQuote> Quotes => _quotes;

    public bool TryStart(out string? error)
    {
        error = null;
        lock (_sync)
        {
            if (_started)
                return true;

            _sink = new ProfitRtdUpdateSink(() => _refreshPending = true);

            foreach (var (label, server) in CreateServerCandidates())
            {
                try
                {
                    var result = server.ServerStart(_sink);
                    if (result <= 0)
                    {
                        _logger.LogDebug("RTD {Label} ServerStart retornou {Result}", label, result);
                        continue;
                    }

                    _server = server;
                    _logger.LogInformation("RTD conectado via {Label} (ServerStart={Result})", label, result);
                    ConnectConfiguredTopics();
                    _started = true;
                    IsConnected = true;

                    // Puxa snapshot imediato (ConnectData já aplicou valores iniciais quando disponíveis).
                    ForceRefresh();

                    _logger.LogInformation(
                        "Profit RTD COM conectado — {Topics} tópicos, quotes={Quotes}, dataCount={Data}",
                        _topics.Count,
                        _quotes.Count,
                        _dataCount);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "RTD {Label} indisponível", label);
                }
            }

            error = "Profit RTD indisponível — abra o ProfitChart e aguarde login/replay";
            return false;
        }
    }

    public void Start()
    {
        if (!TryStart(out var error))
            throw new InvalidOperationException(error);
    }

    public void Poll()
    {
        if (!_started || _server == null)
            return;

        try
        {
            _pollCount++;

            // Excel RTD client chama Heartbeat periodicamente.
            if (DateTime.UtcNow - _lastHeartbeatUtc > TimeSpan.FromSeconds(5))
            {
                try { _ = _server.Heartbeat(); }
                catch { /* ignore */ }
                _lastHeartbeatUtc = DateTime.UtcNow;
            }

            // Sempre tenta RefreshData — UpdateNotify só indica urgência.
            _ = _refreshPending;
            _refreshPending = false;
            ForceRefresh();

            if (DateTime.UtcNow - _lastDiagUtc > TimeSpan.FromSeconds(15))
            {
                _lastDiagUtc = DateTime.UtcNow;
                var sample = string.Join(", ",
                    _lastPrices.Take(5).Select(kv => $"{kv.Key}={kv.Value:0.##}"));
                _logger.LogInformation(
                    "RTD diag: polls={Polls} emptyRefresh={Empty} dataCount={Data} quotes={Quotes} pending={Pending} lastData={Last} sample=[{Sample}]",
                    _pollCount,
                    _emptyRefreshCount,
                    _dataCount,
                    _quotes.Count(q => q.Value.Last is > 0),
                    _refreshPending,
                    LastDataUtc?.ToLocalTime().ToString("HH:mm:ss") ?? "—",
                    string.IsNullOrEmpty(sample) ? "nenhum" : sample);
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _logger.LogWarning(ex, "Erro ao Poll/RefreshData RTD");
        }
    }

    private void ForceRefresh()
    {
        if (_server == null)
            return;

        var topicCount = 0;
        var raw = _server.RefreshData(ref topicCount);
        if (raw is not Array data || topicCount <= 0)
        {
            _emptyRefreshCount++;
            return;
        }

        ParseRefreshData(data, topicCount);
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

    private IEnumerable<(string Label, IRtdServer Server)> CreateServerCandidates()
    {
        IRtdServer? progIdServer = null;
        try
        {
            var progIdType = Type.GetTypeFromProgID("RTDTrading.RtdServer");
            if (progIdType != null)
            {
                var instance = Activator.CreateInstance(progIdType);
                if (instance is IRtdServer fromProgId)
                    progIdServer = fromProgId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ProgID RTDTrading.RtdServer indisponível");
        }

        if (progIdServer != null)
            yield return ("RTDTrading.RtdServer", progIdServer);

        yield return ("Interop.RtdServerClass", new RtdServerClass());
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

            // Um ticker físico por símbolo lógico (evita duplicar WINFUT em WIN e WINQ26).
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

        var key = $"{ticker}|{topic}";
        if (!_connectedKeys.Add(key))
            return;

        var topicId = _nextTopicId++;
        Array parameters = new object[] { ticker, topic };
        var getNewValues = true;

        try
        {
            var initial = _server.ConnectData(topicId, ref parameters, ref getNewValues);
            _topics[topicId] = (logical, ticker, topic);

            _logger.LogDebug(
                "RTD connect {Ticker}.{Topic} (id={Id}, logical={Logical}, initial={Initial})",
                ticker, topic, topicId, logical, initial);

            if (initial != null && initial is not DBNull)
                ApplyTopicValue(topicId, initial);
        }
        catch (Exception ex)
        {
            _connectedKeys.Remove(key);
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
        for (var i = 0; i < topicCount; i++)
        {
            if (!TryReadRefreshPair(data, i, topicCount, out var topicId, out var valueObj))
                continue;

            ApplyTopicValue(topicId, valueObj);
        }
    }

    /// <summary>
    /// Aceita matriz 2×N ([0,i]=id,[1,i]=val) ou N×2 ([i,0]=id,[i,1]=val),
    /// além de array 1D intercalado.
    /// </summary>
    private static bool TryReadRefreshPair(Array data, int index, int topicCount, out int topicId, out object value)
    {
        topicId = 0;
        value = null!;

        try
        {
            object? idObj = null;
            object? valObj = null;

            if (data.Rank == 2)
            {
                var len0 = data.GetLength(0);
                var len1 = data.GetLength(1);

                // 2 x N (padrão Excel)
                if (len0 >= 2 && len1 >= topicCount && index < len1)
                {
                    idObj = data.GetValue(0, index);
                    valObj = data.GetValue(1, index);
                }
                // N x 2
                else if (len1 >= 2 && len0 >= topicCount && index < len0)
                {
                    idObj = data.GetValue(index, 0);
                    valObj = data.GetValue(index, 1);
                }
            }
            else if (data.Rank == 1 && data.Length >= (index * 2) + 2)
            {
                idObj = data.GetValue(index * 2);
                valObj = data.GetValue(index * 2 + 1);
            }

            if (idObj == null || valObj == null || valObj is DBNull)
                return false;

            topicId = Convert.ToInt32(idObj);
            value = valObj;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyTopicValue(int topicId, object valueObj)
    {
        if (!_topics.TryGetValue(topicId, out var meta))
            return;

        var topic = meta.Topic.ToUpperInvariant();
        if (topic is not ("ULT" or "FEC" or "QC" or "QV" or "VOL" or "OCP" or "OVD"))
            return;

        if (!TryToDecimal(valueObj, out var price) || price <= 0)
            return;

        var quoteKey = string.IsNullOrWhiteSpace(meta.Logical) ? meta.Ticker : meta.Logical;
        var quote = _quotes.GetOrAdd(quoteKey, _ => new ProfitRtdQuote());
        switch (topic)
        {
            case "ULT":
                quote.Last = price;
                _lastPrices[quoteKey] = price;
                break;
            case "FEC":
                // Fora do pregão: usa fechamento se ainda não houver ULT.
                if (quote.Last is null or <= 0)
                {
                    quote.Last = price;
                    _lastPrices[quoteKey] = price;
                }
                break;
            case "QC" or "OCP":
                quote.Bid = price;
                break;
            case "QV" or "OVD":
                quote.Ask = price;
                break;
            case "VOL":
                // Volume financeiro do Profit pode estourar long; só aceita valores razoáveis.
                if (price <= int.MaxValue)
                    quote.Volume = (long)price;
                break;
        }

        quote.UpdatedUtc = DateTime.UtcNow;
        _dataCount++;
        LastDataUtc = DateTime.UtcNow;
        LastError = null;
        IsConnected = true;

        if (_dataCount <= 10 || (_dataCount % 500 == 0 && topic is "ULT" or "FEC"))
        {
            _logger.LogInformation(
                "RTD data #{Count}: {Logical}/{Ticker}.{Topic} = {Value}",
                _dataCount, quoteKey, meta.Ticker, topic, price);
        }
    }

    private static bool TryToDecimal(object value, out decimal result)
    {
        result = 0;
        switch (value)
        {
            case decimal m:
                result = m;
                return m > 0;
            case double d when !double.IsNaN(d) && !double.IsInfinity(d):
                result = (decimal)d;
                return d > 0;
            case float f when !float.IsNaN(f) && !float.IsInfinity(f):
                result = (decimal)f;
                return f > 0;
            case int i:
                result = i;
                return i > 0;
            case long l:
                result = l;
                return l > 0;
            case string s:
                return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                           System.Globalization.CultureInfo.InvariantCulture, out result) && result > 0
                       || decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                           System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), out result) && result > 0;
            default:
                try
                {
                    result = Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
                    return result > 0;
                }
                catch
                {
                    try
                    {
                        result = Convert.ToDecimal(value, System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
                        return result > 0;
                    }
                    catch
                    {
                        return false;
                    }
                }
        }
    }
}
