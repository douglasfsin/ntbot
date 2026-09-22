using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using MarketData.API.Configuration;
using MarketData.API.Data;
using MarketData.API.Data.Model;
using MarketData.API.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ProfitDLLClient;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Timers;
using static MarketData.API.Worker.ProfitConstants;

namespace MarketData.API.Worker;

public class MarketDataWorker : BackgroundService, IMarketDataProvider
{
    private readonly ILogger<MarketDataWorker> _logger;
    private readonly ProfitDllSettings _settings;

    // Delegates mantidos como campos para evitar coleta pelo GC
    private TStateCallback? _stateCallback;
    private TNewDailyCallback? _newDailyCallback;
    private TNewTinyBookCallBack? _newTinyBookCallBack;
    private TAccountCallback? _accountCallback;
    private TConnectorBrokerAccountListCallback? _brokerAccountListCallback;
    private TConnectorBrokerSubAccountListCallback? _brokerSubAccountListCallback;
    private TOfferBookCallback? _offerBookCallbackV2;
    private TConnectorPriceDepthCallback? _priceDepthCallback;
    private TConnectorOrderCallback? _orderCallback;
    private TConnectorAccountCallback? _orderHistoryCallback;
    private TAssetListInfoCallbackV2? _assetListInfoCallbackV2;
    private TAdjustHistoryCallbackV2? _adjustHistoryCallbackV2;
    private TConnectorAssetPositionListCallback? _assetPositionListCallback;
    private TTheoreticalPriceCallback? _theoreticalPriceCallback;
    private TChangeStateTickerCallback? _changeStateTickerCallback;

    private volatile bool _isActive = false;
    private volatile bool _isMarketConnected = false;

    public bool IsMarketConnected => _isMarketConnected;

    private readonly TaskCompletionSource _readyTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TConnectorTradeCallback? _tradeCallback;
    private TConnectorTradeCallback? _historyTradeCallback;
    private readonly IHubContext<MarketHub> _hub;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, bool>> _dynamicLevelInterests = new();

    // Diagnóstico de dados enviados
    private readonly ConcurrentDictionary<string, TConnectorAssetIdentifier> _assetIdentifiers = new();
    private readonly ConcurrentDictionary<string, bool> _pendingPublishers = new();
    private readonly ConcurrentDictionary<string, double> _assetTickSizes = new();
    private readonly ConcurrentDictionary<string, TickerBookCache> _localBookCache = new();
    private readonly ConcurrentDictionary<string, TinyBookTopCache> _tinyBookTop = new();
    private int _tradesSent = 0;
    private int _booksSent = 0;
    private int _ticksSent = 0;
    private System.Timers.Timer? _diagnosticTimer;

    // Simulação
    private volatile bool _isSimulationMode = false;
    private List<MarketEvent> _simulationData = new();
    private System.Timers.Timer? _simulationTimer;
    private int _simulationIndex = 0;

    // Salvamento de dados
    private Dictionary<string, List<MarketEvent>> _dailyData = new();
    private readonly object _dataLock = new();
    private System.Timers.Timer? _saveDataTimer;

    private readonly MarketData.API.Services.ProfitHistoricalTradeProvider _historyProvider;

    public MarketDataWorker(
        ILogger<MarketDataWorker> logger,
        IOptions<ProfitDllSettings> settings,
        IHubContext<MarketHub> hub,
        MarketData.API.Services.ProfitHistoricalTradeProvider historyProvider)
    {
        _logger = logger;
        _settings = settings.Value;
        _hub = hub;
        _historyProvider = historyProvider;

        // Garantir que o diretório de dados existe
        if (!Directory.Exists("c:\\liga"))
        {
            Directory.CreateDirectory("c:\\liga");
        }

        // Inicializar timer de salvamento (salva a cada 5 minutos)
        _saveDataTimer = new System.Timers.Timer(5 * 60 * 1000);
        _saveDataTimer.Elapsed += (_, _) => SaveDailyData();
        _saveDataTimer.AutoReset = true;
        _saveDataTimer.Start();
    }

    // =========================================================================
    // Simulação
    // =========================================================================

    public void StartSimulation(string date)
    {
        if (_isSimulationMode) return;
        LoadSimulationData(date);
        if (_simulationData.Count == 0)
        {
            _logger.LogWarning("Nenhum dado encontrado para simulação na data {Date}", date);
            return;
        }
        _isSimulationMode = true;
        _simulationIndex = 0;
        _simulationTimer = new System.Timers.Timer(10000); // 10 segundos
        _simulationTimer.Elapsed += OnSimulationTick;
        _simulationTimer.AutoReset = true;
        _simulationTimer.Start();
        _logger.LogInformation("Simulação iniciada para data {Date} com {Count} eventos", date, _simulationData.Count);
    }

    public void StopSimulation()
    {
        if (!_isSimulationMode) return;
        _simulationTimer?.Stop();
        _simulationTimer?.Dispose();
        _isSimulationMode = false;
        _simulationIndex = 0;
        _logger.LogInformation("Simulação parada");
    }

    public Task StartSimulationAsync(string date, CancellationToken ct)
    {
        StartSimulation(date);
        return Task.CompletedTask;
    }

    private void LoadSimulationData(string date)
    {
        var dir = Path.Combine("c:\\liga", date);
        if (!Directory.Exists(dir))
        {
            // Tentar formato antigo para compatibilidade
            var oldPath = Path.Combine("c:\\liga", $"{date}.json");
            if (File.Exists(oldPath))
            {
                try
                {
                    var json = File.ReadAllText(oldPath);
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

    private void OnSimulationTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_simulationIndex >= _simulationData.Count)
        {
            StopSimulation();
            return;
        }
        var evt = _simulationData[_simulationIndex];
        // Simular envio como se fosse callback
        PublishTick(evt.Ticker, "SIM", evt.Price); // Ajustar topic conforme necessário
        // Para book, etc., mas simplificar enviando preço
        _ = _hub.Clients.Group(evt.Ticker).SendAsync("price", evt.Ticker, evt.Price);
        _simulationIndex++;
    }

    // =========================================================================
    // Salvamento de dados
    // =========================================================================

    private void AddMarketEvent(MarketEvent evt)
    {
        var date = evt.Timestamp.ToString("yyyy-MM-dd");
        lock (_dataLock)
        {
            if (!_dailyData.ContainsKey(date))
                _dailyData[date] = new List<MarketEvent>();
            _dailyData[date].Add(evt);
        }
    }

    private void SaveDailyData()
    {
        lock (_dataLock)
        {
            foreach (var kvp in _dailyData)
            {
                var date = kvp.Key;
                var events = kvp.Value;
                var dir = Path.Combine("c:\\liga", date);
                Directory.CreateDirectory(dir);
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var path = Path.Combine(dir, $"market_data_{timestamp}.json");
                try
                {
                    var json = JsonSerializer.Serialize(events);
                    File.WriteAllText(path, json);
                    _logger.LogInformation("Dados salvos para {Date}: {Count} eventos em {Path}", date, events.Count, path);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao salvar dados para {Date}", date);
                }
            }
            _dailyData.Clear(); // Limpar após salvar
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataWorker iniciado.");

        // Inicia o loop de publicação em background para evitar bloqueio de callbacks
        _ = Task.Run(() => BackgroundPublishingLoop(stoppingToken), stoppingToken);

        StartDiagnosticTimer();
        RegisterCallbacks();

        var result = StartDLL(_settings.ActivationKey, _settings.User, _settings.Password);
        if (result != NL_OK)
        {
            _logger.LogError("Falha ao inicializar a ProfitDLL. Código: {Code}", result);
            return;
        }

        _logger.LogInformation("Aguardando conexão com o mercado (sem timeout — subscrições só ocorrem após DLL pronta)...");
        try
        {
            // Aguarda indefinidamente até que OnStateCallback sinalize mercado conectado+ativo.
            // NÃO usar timeout aqui: chamar SubscribePriceDepth com DLL não inicializada causa
            // AccessViolationException (memória corrompida).
            await _readyTcs.Task.WaitAsync(stoppingToken);
            _logger.LogInformation("Mercado conectado. Iniciando subscrições...");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MarketDataWorker cancelado antes de conectar ao mercado.");
            return;
        }

        foreach (var ticker in _settings.Tickers)
        {
            var assetId = new TConnectorAssetIdentifier
            {
                Version = 0,
                Ticker = ticker.Code,
                Exchange = ticker.Bag
            };

            ProfitDLL.SubscribePriceBook(ticker.Code, ticker.Bag);
            ProfitDLL.SubscribeTicker(ticker.Code, ticker.Bag);
            ProfitDLL.SubscribePriceDepth(assetId);

            _logger.LogInformation("Subscrito: {Ticker} @ {Exchange}", ticker.Code, ticker.Bag);
        }

        stoppingToken.Register(() => ChannelProvider.Channel.Writer.TryComplete());
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    private async Task BackgroundPublishingLoop(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundPublishingLoop iniciado.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_pendingPublishers.Any())
                {
                    var tickers = _pendingPublishers.Keys.ToList();
                    foreach (var ticker in tickers)
                    {
                        if (_pendingPublishers.TryRemove(ticker, out _))
                        {
                            await PublishTickerSnapshot(ticker);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no BackgroundPublishingLoop");
            }

            await Task.Delay(200, stoppingToken);
        }
    }

    public void RegisterInterest(string ticker, int niveis)
    {
        var levels = _dynamicLevelInterests.GetOrAdd(ticker, _ => new ConcurrentDictionary<int, bool>());
        levels.TryAdd(niveis, true);
        _logger.LogInformation("Novo interesse registrado: {T} -> {N} níveis", ticker, niveis);
    }

    private void CalculateDensity(TConnectorAssetIdentifier assetId, byte side, double tickSize,
        out int levels10, out int ticks10, out int levels25, out int ticks25,
        out int levels50, out int ticks50, out int levels100, out int ticks100)
    {
        levels10 = levels25 = levels50 = levels100 = 0;
        ticks10 = ticks25 = ticks50 = ticks100 = 0;

        if (assetId.Ticker == null || !_localBookCache.TryGetValue(assetId.Ticker, out var bookCache)) return;

        List<PriceGroupCache> levels;
        if (side == 0)
        {
            lock (bookCache.Bids)
            {
                levels = new List<PriceGroupCache>(bookCache.Bids);
            }
        }
        else
        {
            lock (bookCache.Asks)
            {
                levels = new List<PriceGroupCache>(bookCache.Asks);
            }
        }

        if (levels.Count == 0) return;

        double totalVolume = 0;
        foreach (var lvl in levels)
        {
            totalVolume += lvl.Quantity;
        }

        if (totalVolume <= 0) return;

        double target10 = totalVolume * 0.10;
        double target25 = totalVolume * 0.25;
        double target50 = totalVolume * 0.50;
        double target100 = totalVolume * 1.00;

        double accumulated = 0;
        double p0 = levels[0].Price;

        bool hit10 = false, hit25 = false, hit50 = false, hit100 = false;

        for (int i = 0; i < levels.Count; i++)
        {
            accumulated += levels[i].Quantity;

            if (!hit10 && accumulated >= target10)
            {
                levels10 = i + 1;
                double dist = Math.Abs(p0 - levels[i].Price);
                ticks10 = tickSize > 0 ? (int)Math.Round(dist / tickSize) : 0;
                hit10 = true;
            }

            if (!hit25 && accumulated >= target25)
            {
                levels25 = i + 1;
                double dist = Math.Abs(p0 - levels[i].Price);
                ticks25 = tickSize > 0 ? (int)Math.Round(dist / tickSize) : 0;
                hit25 = true;
            }

            if (!hit50 && accumulated >= target50)
            {
                levels50 = i + 1;
                double dist = Math.Abs(p0 - levels[i].Price);
                ticks50 = tickSize > 0 ? (int)Math.Round(dist / tickSize) : 0;
                hit50 = true;
            }

            if (!hit100 && accumulated >= target100)
            {
                levels100 = i + 1;
                double dist = Math.Abs(p0 - levels[i].Price);
                ticks100 = tickSize > 0 ? (int)Math.Round(dist / tickSize) : 0;
                hit100 = true;
            }
        }

        // Fallbacks if target not fully met
        int lastIdx = levels.Count - 1;
        double lastDist = Math.Abs(p0 - levels[lastIdx].Price);
        int lastTicks = tickSize > 0 ? (int)Math.Round(lastDist / tickSize) : 0;

        if (!hit10)
        {
            levels10 = levels.Count;
            ticks10 = lastTicks;
        }
        if (!hit25)
        {
            levels25 = levels.Count;
            ticks25 = lastTicks;
        }
        if (!hit50)
        {
            levels50 = levels.Count;
            ticks50 = lastTicks;
        }
        if (!hit100)
        {
            levels100 = levels.Count;
            ticks100 = lastTicks;
        }
    }

    private async Task PublishTickerSnapshot(string ticker)
    { 
        var cached = MarketCache.Get(ticker);
        if (cached == null) return;

        // --- CÁLCULOS PESADOS MOVIDOS PARA O BACKGROUND ---
        double bid5 = 0, ask5 = 0;
        if (_assetIdentifiers.TryGetValue(ticker, out var assetID))
        {
            int halfLevels = Math.Max(5, _settings.NiveisBook / 2);

            // Realiza as somas de profundidade apenas no momento da publicação (Throttle)
            double bidN = SumBidQuantity(assetID, halfLevels);
            double bidFull = SumBidFullQuantity(assetID);
            double askN = SumAskQuantity(assetID, halfLevels);
            double askFull = SumAskFullQuantity(assetID);
            bid5 = SumBidQuantity(assetID, 5);
            ask5 = SumAskQuantity(assetID, 5);

            // Atualiza o cache com os novos valores calculados
            MarketCache.UpdateBidDepth(ticker, bidN);
            MarketCache.UpdateBidFullDepth(ticker, bidFull);
            MarketCache.UpdateAskDepth(ticker, askN);
            MarketCache.UpdateAskFullDepth(ticker, askFull);

            if (bid5 > 0)
                MarketCache.UpdateBook(ticker, true, 0, 0, bid5);
            if (ask5 > 0)
                MarketCache.UpdateBook(ticker, false, 0, 0, ask5);

            if (_settings.EnableDetailedLogging)
            {
                _logger.LogInformation(
                    "[SNAPSHOT] {T} Bid={B} Ask={A} Bid5={B5} Ask5={A5} BidN={BN} AskN={AN}",
                    ticker, cached.Bid, cached.Ask, bid5, ask5, bidN, askN);
            }
        }

        // Preferir topo do cache local de profundidade (lados distintos)
        ResolveBookTop(ticker, cached, out var snapBid, out var snapBidQty, out var snapAsk, out var snapAskQty);

        int bidL10 = 0, bidT10 = 0, bidL25 = 0, bidT25 = 0, bidL50 = 0, bidT50 = 0, bidL100 = 0, bidT100 = 0;
        int askL10 = 0, askT10 = 0, askL25 = 0, askT25 = 0, askL50 = 0, askT50 = 0, askL100 = 0, askT100 = 0;

        if (_assetIdentifiers.TryGetValue(ticker, out var assetId))
        {
            double tickSize = 0.5; // default fallback
            if (_assetTickSizes.TryGetValue(ticker, out double cachedTickSize) && cachedTickSize > 0)
            {
                tickSize = cachedTickSize;
            }
            else
            {
                if (ticker.StartsWith("WDO")) tickSize = 0.5;
                else if (ticker.StartsWith("DOL")) tickSize = 0.5;
                else if (ticker.StartsWith("WIN")) tickSize = 1.0;
                else if (ticker.StartsWith("IND")) tickSize = 5.0;
                else if (ticker.StartsWith("DI1")) tickSize = 0.005;
                else tickSize = 0.01;
            }

            CalculateDensity(assetId, 0, tickSize, out bidL10, out bidT10, out bidL25, out bidT25, out bidL50, out bidT50, out bidL100, out bidT100);
            CalculateDensity(assetId, 1, tickSize, out askL10, out askT10, out askL25, out askT25, out askL50, out askT50, out askL100, out askT100);
        }

        // Snapshot padrão (5 níveis)
        var baseSnapshot = new BookSnapshot
        {
            Ticker = ticker,
            BestBid = SafeNumber(snapBid),
            BestBidQty = SafeInt(snapBidQty),
            BestAsk = SafeNumber(snapAsk),
            BestAskQty = SafeInt(snapAskQty),
            BestBidQty5 = SafeInt(bid5 > 0 ? bid5 : cached.BidQty5),
            BestAskQty5 = SafeInt(ask5 > 0 ? ask5 : cached.AskQty5),

            // LEILÃO
            IsAuction = cached.IsAuction,
            TheoreticalPrice = SafeNumber(cached.TheoreticalPrice),
            TheoreticalQty = cached.TheoreticalQty,

            Niveis = 5,
            BidQtyN = SafeNumber(cached.BidQtyN),
            AskQtyN = SafeNumber(cached.AskQtyN),
            BidFullQty = SafeNumber(cached.BidFullQty),
            AskFullQty = SafeNumber(cached.AskFullQty),

            // DENSIDADE COMPRA (BID)
            BidLevels10 = bidL10,
            BidTicks10 = bidT10,
            BidLevels25 = bidL25,
            BidTicks25 = bidT25,
            BidLevels50 = bidL50,
            BidTicks50 = bidT50,
            BidLevels100 = bidL100,
            BidTicks100 = bidT100,

            // DENSIDADE VENDA (ASK)
            AskLevels10 = askL10,
            AskTicks10 = askT10,
            AskLevels25 = askL25,
            AskTicks25 = askT25,
            AskLevels50 = askL50,
            AskTicks50 = askT50,
            AskLevels100 = askL100,
            AskTicks100 = askT100,

            Timestamp = DateTime.UtcNow
        };

        await _hub.Clients.Group(ticker).SendAsync("book", baseSnapshot);
        await _hub.Clients.Group(ticker).SendAsync("price", ticker, cached.LastPrice);

        // Publicar também para o grupo genérico (WDOFUT, DOLFUT) se aplicável
        string generic = GetGenericTicker(ticker);
        if (generic != ticker)
        {
            await _hub.Clients.Group(generic).SendAsync("book", baseSnapshot);
            await _hub.Clients.Group(generic).SendAsync("price", generic, cached.LastPrice);
        }

        // Snapshots Dinâmicos - Usa os valores do cache para não travar a DLL em thread separada
        if (_dynamicLevelInterests.TryGetValue(ticker, out var interests) &&
            _assetIdentifiers.TryGetValue(ticker, out var assetIdRef))
        {
            foreach (var nivel in interests.Keys)
            {
                if (nivel == 5) continue; // Já enviado no base

                // Evento dedicado de leilão — emite sempre que IsAuction estiver ativo
                if (cached.IsAuction && cached.TheoreticalPrice > 0)
                {
                    var auction = new AuctionDto
                    {
                        Ticker = ticker,
                        IsAuction = true,
                        TheoreticalPrice = SafeNumber(cached.TheoreticalPrice),
                        TheoreticalQty = cached.TheoreticalQty,
                        State = cached.AuctionState,
                        Timestamp = DateTime.UtcNow
                    };
                    await _hub.Clients.Group(ticker).SendAsync("auction", auction);
                    _logger.LogWarning(
                    "[AUCTION SEND] {Ticker} P={Price} Q={Qty} State={State}",
                    ticker,
                    auction.TheoreticalPrice,
                    auction.TheoreticalQty,
                    auction.State);
                }

                double bidQtyN = SumBidQuantity(assetIdRef, nivel);
                double askQtyN = SumAskQuantity(assetIdRef, nivel);

                var dynSnapshot = new BookSnapshot
                {
                    Ticker = ticker,
                    BestBid = SafeNumber(cached.Bid),
                    BestBidQty = SafeInt(cached.BidQty),
                    BestAsk = SafeNumber(cached.Ask),
                    BestAskQty = SafeInt(cached.AskQty),
                    // Como não podemos consultar a DLL fora da thread principal (callback), usamos os totais que o OnPriceDepthCallback calculou
                    BestBidQty5 = SafeInt(bidQtyN),
                    BestAskQty5 = SafeInt(askQtyN),
                    BidQtyN = SafeNumber(bidQtyN),
                    AskQtyN = SafeNumber(askQtyN),
                    Niveis = nivel,

                    // DENSIDADE COMPRA (BID)
                    BidLevels10 = bidL10,
                    BidTicks10 = bidT10,
                    BidLevels25 = bidL25,
                    BidTicks25 = bidT25,
                    BidLevels50 = bidL50,
                    BidTicks50 = bidT50,
                    BidLevels100 = bidL100,
                    BidTicks100 = bidT100,

                    // DENSIDADE VENDA (ASK)
                    AskLevels10 = askL10,
                    AskTicks10 = askT10,
                    AskLevels25 = askL25,
                    AskTicks25 = askT25,
                    AskLevels50 = askL50,
                    AskTicks50 = askT50,
                    AskLevels100 = askL100,
                    AskTicks100 = askT100,

                    Timestamp = DateTime.UtcNow
                };

                await _hub.Clients.Group($"{ticker}_{nivel}").SendAsync("book", dynSnapshot);


                // Também publica no genérico dinâmico
                if (generic != ticker)
                {
                    await _hub.Clients.Group($"{generic}_{nivel}").SendAsync("book", dynSnapshot);
                }
            }
        }

        _booksSent++;
    }

    private static double SafeNumber(double value)
    {
        return double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;
    }

    private static int SafeInt(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0) return 0;
        return value > int.MaxValue ? int.MaxValue : (int)value;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MarketDataWorker encerrando...");

        StopSimulation();

        foreach (var ticker in _settings.Tickers)
        {
            var assetId = new TConnectorAssetIdentifier
            {
                Version = 0,
                Ticker = ticker.Code,
                Exchange = ticker.Bag
            };
            ProfitDLL.UnsubscribePriceDepth(assetId);
            ProfitDLL.UnsubscribePriceBook(ticker.Code, ticker.Bag);
            ProfitDLL.UnsubscribeTicker(ticker.Code, ticker.Bag);
        }

        ChannelProvider.Channel.Writer.Complete();

        _saveDataTimer?.Stop();
        _saveDataTimer?.Dispose();

        await base.StopAsync(cancellationToken);
    }

    private void StartDiagnosticTimer()
    {
        _diagnosticTimer = new System.Timers.Timer(30_000);
        _diagnosticTimer.Elapsed += (_, _) =>
        {
            _logger.LogInformation("[DIAG] Trades enviados={T} | Books enviados={B} | Ticks enviados={K} | MarketConnected={MC} | Active={A}",
                _tradesSent, _booksSent, _ticksSent, _isMarketConnected, _isActive);
        };
        _diagnosticTimer.AutoReset = true;
        _diagnosticTimer.Start();
    }

    private void PublishTick(string ticker, string topic, double value)
    {
        _ = _hub.Clients.Group(ticker).SendAsync("tick", ticker, topic, value);

        string generic = GetGenericTicker(ticker);
        if (generic != ticker)
        {
            _ = _hub.Clients.Group(generic).SendAsync("tick", generic, topic, value);
        }

        _ticksSent++;
    }

    private string GetGenericTicker(string ticker)
    {
        if (string.IsNullOrEmpty(ticker)) return ticker;
        if (ticker.StartsWith("WDO")) return "WDOFUT";
        if (ticker.StartsWith("DOL") && !ticker.Contains("INDEX")) return "DOLFUT";
        if (ticker.StartsWith("WIN")) return "WINFUT";
        if (ticker.StartsWith("IND")) return "INDFUT";
        if (ticker.StartsWith("DI1")) return "DI1FUT";
        return ticker;
    }

    private int StartDLL(string key, string user, string password)
    {
        int retVal;

        if (_settings.UseRouting)
        {
            retVal = ProfitDLL.DLLInitializeLogin(
                key, user, password,
                _stateCallback!,
                null, null,
                _accountCallback!,
                null,
                _newDailyCallback!,
                null, null, null, null,
                _newTinyBookCallBack!);
        }
        else
        {
            retVal = ProfitDLL.DLLInitializeMarketLogin(
                key, user, password,
                _stateCallback!,
                null,
                _newDailyCallback!,
                null, null, null, null,
                _newTinyBookCallBack!);
        }

        if (retVal != NL_OK)
        {
            _logger.LogError("DLLInitialize falhou: {Code}", retVal);
            return retVal;
        }

        ProfitDLL.SetPriceDepthCallback(_priceDepthCallback!);
        ProfitDLL.SetBrokerAccountListChangedCallback(_brokerAccountListCallback!);
        ProfitDLL.SetBrokerSubAccountListChangedCallback(_brokerSubAccountListCallback!);
        ProfitDLL.SetOrderCallback(_orderCallback!);
        ProfitDLL.SetOrderHistoryCallback(_orderHistoryCallback!);
        ProfitDLL.SetAssetListInfoCallbackV2(_assetListInfoCallbackV2!);
        ProfitDLL.SetAdjustHistoryCallbackV2(_adjustHistoryCallbackV2!);
        ProfitDLL.SetAssetPositionListCallback(_assetPositionListCallback!);
        ProfitDLL.SetTradeCallbackV2(_tradeCallback!);
        ProfitDLL.SetTheoreticalPriceCallback(_theoreticalPriceCallback!);
        ProfitDLL.SetChangeStateTickerCallback(_changeStateTickerCallback!);
        ProfitDLL.SetHistoryTradeCallbackV2(_historyTradeCallback!);

        return NL_OK;
    }

    private void RegisterCallbacks()
    {
        _stateCallback = new TStateCallback(OnStateCallback);
        _newDailyCallback = new TNewDailyCallback(OnNewDailyCallback);
        _newTinyBookCallBack = new TNewTinyBookCallBack(OnNewTinyBookCallBack);
        _accountCallback = new TAccountCallback(OnAccountCallback);
        _brokerAccountListCallback = new TConnectorBrokerAccountListCallback(OnBrokerAccountListChanged);
        _brokerSubAccountListCallback = new TConnectorBrokerSubAccountListCallback(OnBrokerSubAccountListChanged);
        _priceDepthCallback = new TConnectorPriceDepthCallback(OnPriceDepthCallback);
        _orderCallback = new TConnectorOrderCallback(OnOrderCallback);
        _orderHistoryCallback = new TConnectorAccountCallback(OnOrderHistoryCallback);
        _assetListInfoCallbackV2 = new TAssetListInfoCallbackV2(OnAssetListInfoCallbackV2);
        _adjustHistoryCallbackV2 = new TAdjustHistoryCallbackV2(OnAdjustHistoryCallbackV2);
        _assetPositionListCallback = new TConnectorAssetPositionListCallback(OnAssetPositionListCallback);
        _tradeCallback = new TConnectorTradeCallback(OnTradeCallback);
        _historyTradeCallback = new TConnectorTradeCallback(_historyProvider.OnHistoryTradeCallback);
        _theoreticalPriceCallback = new TTheoreticalPriceCallback(OnTheoreticalPriceCallback);
        _changeStateTickerCallback = new TChangeStateTickerCallback(OnChangeStateTickerCallback);
    }

    private void OnStateCallback(int nConnStateType, int result)
    {
        switch (nConnStateType)
        {
            case 0:
                _logger.LogInformation("Login: {S}", result switch
                {
                    0 => "Conectado",
                    1 => "Inválido",
                    2 => "Senha inválida",
                    3 => "Senha bloqueada",
                    4 => "Senha expirada",
                    200 => "Erro desconhecido",
                    _ => $"Código {result}"
                });
                break;

            case 1:
                _logger.LogInformation("Broker: {S}", result switch
                {
                    0 => "Desconectado",
                    1 => "Conectando",
                    2 => "Conectado",
                    3 => "HCS Desconectado",
                    4 => "HCS Conectando",
                    5 => "HCS Conectado",
                    _ => $"Código {result}"
                });
                break;

            case 2:
                _isMarketConnected = result == 4;
                _logger.LogInformation("Market: {S}", result switch
                {
                    0 => "Desconectado",
                    1 => "Conectando",
                    2 => "Aguardando",
                    3 => "Não logado",
                    4 => "Conectado",
                    _ => $"Código {result}"
                });
                break;

            case 3:
                _isActive = result == 0;
                _logger.LogInformation("Profit: Ativação {S}", _isActive ? "Válida" : "Inválida");
                break;
        }

        if (_isMarketConnected && _isActive)
        {
            _logger.LogInformation("[DIAG] ✓ Mercado conectado e ativação válida — aguardando dados...");
            _readyTcs.TrySetResult();
        }
    }

    private void OnPriceDepthCallback(
        TConnectorAssetIdentifier assetID,
        byte side,
        int position,
        byte updateType)
    {
        var ticker = assetID.Ticker;
        _assetIdentifiers[ticker] = assetID;
        // Filtro de Segurança: Processar apenas Side 0 (Compra) ou 1 (Venda)
        if (side != 0 && side != 1)
        {
            return;
        }

        var tUpdate = (TConnectorUpdateType)updateType;

        // Processamos se for FullBook, Delete, ou posição dentro do intervalo configurado
        if (position >= _settings.NiveisBook) return;

        // Carrega o lado completo do book a partir da DLL (seguro na thread do callback nativo)
        int bookSide = side; // int explícito — P/Invoke exige int no x64 (não byte)
        int available = ProfitDLL.GetPriceDepthSideCount(assetID, bookSide);
        var levels = new List<PriceGroupCache>(available);
        bool isAuctionDetected = false;
        double theoreticalPrice = 0;
        long theoreticalQtd = 0;

        for (int i = 0; i < available; i++)
        {
            var pg = new TConnectorPriceGroup { Version = 0 };
            if (ProfitDLL.GetPriceGroup(assetID, bookSide, i, ref pg) == NL_OK)
            {
                if (i == 0 && double.IsNegativeInfinity(pg.Price))
                {
                    isAuctionDetected = true;
                    if (ProfitDLL.GetTheoreticalValues(assetID, out theoreticalPrice, out theoreticalQtd) == NL_OK)
                    {
                        levels.Add(new PriceGroupCache { Price = theoreticalPrice, Quantity = theoreticalQtd });
                    }
                    continue;
                }

                if (double.IsInfinity(pg.Price) || double.IsNaN(pg.Price) || pg.Price <= 0)
                {
                    continue;
                }
                levels.Add(new PriceGroupCache { Price = pg.Price, Quantity = pg.Quantity });
            }
            else
            {
                break;
            }
        }

        // Salva no cache local C# thread-safe
        var bookCache = _localBookCache.GetOrAdd(ticker, _ => new TickerBookCache());
        levels = SanitizeDepthLevels(ticker, bookSide, levels, bookCache);

        if (bookSide == 0)
        {
            lock (bookCache.Bids)
            {
                bookCache.Bids = levels;
            }
        }
        else
        {
            lock (bookCache.Asks)
            {
                bookCache.Asks = levels;
            }
        }

        // Lógica de Leilão
        if (isAuctionDetected)
        {
            _logger.LogInformation(
                "[LEILÃO] {T} Preço teórico={P:F2} Qtd={Q}",
                ticker,
                theoreticalPrice,
                theoreticalQtd);

            MarketCache.UpdateAuction(ticker, theoreticalPrice, theoreticalQtd);

            PublishTick(ticker, "PRT", theoreticalPrice);
            PublishTick(ticker, "QTR", (double)theoreticalQtd);
        }
        else
        {
            // 🔥 garante limpeza do estado de leilão
            var cache = MarketCache.Get(ticker);
            if (cache?.IsAuction == true)
            {
                _logger.LogInformation("[LEILÃO ENCERRADO] {T}", ticker);
                MarketCache.UpdateAuction(ticker, 0, 0, 0);
            }
        }

        // Topo do book vem do TinyBook (confiável). PriceDepth alimenta profundidade/somas.
        _pendingPublishers[ticker] = true;
        LogBookDepthAnomalyIfNeeded(ticker, bookCache);
    }

    /// <summary>
    /// Alerta quando profundidade duplica ask=bid mas TinyBook mostra spread real.
    /// </summary>
    private void LogBookDepthAnomalyIfNeeded(string ticker, TickerBookCache bookCache)
    {
        if (!_tinyBookTop.TryGetValue(ticker, out var tiny) || !tiny.HasBid || !tiny.HasAsk)
            return;

        double tick = GetTickSizeForTicker(ticker);
        if (tiny.AskPrice <= tiny.BidPrice + tick * 0.01)
            return;

        double depthBid = 0, depthAsk = 0;
        lock (bookCache.Bids)
        {
            if (bookCache.Bids.Count > 0) depthBid = bookCache.Bids[0].Price;
        }
        lock (bookCache.Asks)
        {
            if (bookCache.Asks.Count > 0) depthAsk = bookCache.Asks[0].Price;
        }

        if (depthBid > 0 && depthAsk > 0 && Math.Abs(depthBid - depthAsk) < tick * 0.01)
        {
            _logger.LogWarning(
                "[BOOK-DEPTH] {T} ask duplicado na profundidade ({P}) — TinyBook Bid={B} Ask={A}",
                ticker, depthAsk, tiny.BidPrice, tiny.AskPrice);
        }
    }

    /// <summary>
    /// Corrige ask da profundidade quando GetPriceGroup(side=1) retorna dados de compra (bug x64/DLL).
    /// </summary>
    private List<PriceGroupCache> SanitizeDepthLevels(
        string ticker,
        int bookSide,
        List<PriceGroupCache> levels,
        TickerBookCache bookCache)
    {
        if (bookSide != 1 || levels.Count == 0)
            return levels;

        if (!_tinyBookTop.TryGetValue(ticker, out var tiny) || !tiny.HasAsk)
            return levels;

        double tick = GetTickSizeForTicker(ticker);
        double topBid = tiny.HasBid ? tiny.BidPrice : 0;
        if (topBid <= 0)
        {
            lock (bookCache.Bids)
            {
                if (bookCache.Bids.Count > 0) topBid = bookCache.Bids[0].Price;
            }
        }

        bool askDuplicated = topBid > 0 && Math.Abs(levels[0].Price - topBid) < tick * 0.01;
        bool tinySpread = tiny.HasBid && tiny.AskPrice > tiny.BidPrice + tick * 0.01;

        if (!askDuplicated || !tinySpread)
            return levels;

        var corrected = new List<PriceGroupCache>
        {
            new() { Price = tiny.AskPrice, Quantity = tiny.AskQty > 0 ? tiny.AskQty : levels[0].Quantity }
        };

        foreach (var lvl in levels.Skip(1))
        {
            if (lvl.Price > tiny.AskPrice + tick * 0.01 && Math.Abs(lvl.Price - topBid) > tick * 0.01)
                corrected.Add(lvl);
        }

        _logger.LogDebug(
            "[BOOK-DEPTH] {T} ask corrigido {Bad} -> {Good}",
            ticker, levels[0].Price, tiny.AskPrice);

        return corrected;
    }

    private double GetTickSizeForTicker(string ticker)
    {
        if (_assetTickSizes.TryGetValue(ticker, out double cached) && cached > 0)
            return cached;

        if (ticker.StartsWith("WDO", StringComparison.OrdinalIgnoreCase)) return 0.5;
        if (ticker.StartsWith("DOL", StringComparison.OrdinalIgnoreCase)) return 0.5;
        if (ticker.StartsWith("WIN", StringComparison.OrdinalIgnoreCase)) return 5.0;
        if (ticker.StartsWith("IND", StringComparison.OrdinalIgnoreCase)) return 5.0;
        if (ticker.StartsWith("DI1", StringComparison.OrdinalIgnoreCase)) return 0.005;
        return 0.01;
    }

    // --- MÉTODOS DE CÁLCULO BASEADOS NO CACHE LOCAL (EVITA LOCK NA DLL) ---

    private void ResolveBookTop(
        string ticker,
        PriceTick cached,
        out double bestBid,
        out double bestBidQty,
        out double bestAsk,
        out double bestAskQty)
    {
        bestBid = cached.Bid;
        bestBidQty = cached.BidQty;
        bestAsk = cached.Ask;
        bestAskQty = cached.AskQty;

        // TinyBook é a fonte confiável para topo (bid/ask distintos)
        if (_tinyBookTop.TryGetValue(ticker, out var tiny))
        {
            if (tiny.HasBid)
            {
                bestBid = tiny.BidPrice;
                bestBidQty = tiny.BidQty;
            }
            if (tiny.HasAsk)
            {
                bestAsk = tiny.AskPrice;
                bestAskQty = tiny.AskQty;
            }
            return;
        }
    }

    private double SumBidQuantity(TConnectorAssetIdentifier assetID, int nLevels)
    {
        var ticker = assetID.Ticker;
        if (ticker == null || !_localBookCache.TryGetValue(ticker, out var bookCache)) return 0;
        lock (bookCache.Bids)
        {
            int count = Math.Min(nLevels, bookCache.Bids.Count);
            double total = 0;
            for (int i = 0; i < count; i++)
            {
                total += bookCache.Bids[i].Quantity;
            }
            return total;
        }
    }

    private double SumAskQuantity(TConnectorAssetIdentifier assetID, int nLevels)
    {
        var ticker = assetID.Ticker;
        if (ticker == null || !_localBookCache.TryGetValue(ticker, out var bookCache)) return 0;
        lock (bookCache.Asks)
        {
            int count = Math.Min(nLevels, bookCache.Asks.Count);
            double total = 0;
            for (int i = 0; i < count; i++)
            {
                total += bookCache.Asks[i].Quantity;
            }
            return total;
        }
    }

    private double SumBidFullQuantity(TConnectorAssetIdentifier assetID)
    {
        var ticker = assetID.Ticker;
        if (ticker == null || !_localBookCache.TryGetValue(ticker, out var bookCache)) return 0;
        lock (bookCache.Bids)
        {
            double total = 0;
            foreach (var lvl in bookCache.Bids)
            {
                total += lvl.Quantity;
            }
            return total;
        }
    }

    private double SumAskFullQuantity(TConnectorAssetIdentifier assetID)
    {
        var ticker = assetID.Ticker;
        if (ticker == null || !_localBookCache.TryGetValue(ticker, out var bookCache)) return 0;
        lock (bookCache.Asks)
        {
            double total = 0;
            foreach (var lvl in bookCache.Asks)
            {
                total += lvl.Quantity;
            }
            return total;
        }
    }

    private void OnNewTinyBookCallBack(TAssetID assetId, double price, int qtd, int side)
    {
        var ticker = assetId.Ticker;
        _logger.LogDebug("{A} TinyBook [{T}] {S} {P} x {Q}",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), ticker, side == 0 ? "Buy" : "Sell", price, qtd);

        var top = _tinyBookTop.GetOrAdd(ticker, _ => new TinyBookTopCache());
        if (side == 0)
        {
            top.BidPrice = price;
            top.BidQty = qtd;
        }
        else
        {
            top.AskPrice = price;
            top.AskQty = qtd;
        }
        top.LastUpdateUtc = DateTime.UtcNow;

        MarketCache.UpdateBook(ticker, side == 0, price, qtd, 0);
        _pendingPublishers[ticker] = true;
    }

    private void OnNewDailyCallback(
        TAssetID assetId,
        [MarshalAs(UnmanagedType.LPWStr)] string date,
        double sOpen, double sHigh, double sLow, double sClose,
        double sVol, double sAjuste, double sMaxLimit, double sMinLimit,
        double sVolBuyer, double sVolSeller,
        int nQtd, int nNegocios, int nContratosOpen,
        int nQtdBuyer, int nQtdSeller, int nNegBuyer, int nNegSeller)
    {
        _logger.LogDebug("{A} Daily [{T}] O={O} H={H} L={L} C={C}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), assetId.Ticker, sOpen, sHigh, sLow, sClose);

        var ticker = assetId.Ticker;

        if (sOpen > 0) PublishTick(ticker, "ABE", sOpen);
        if (sHigh > 0) PublishTick(ticker, "MAX", sHigh);
        if (sLow > 0) PublishTick(ticker, "MIN", sLow);
        if (sAjuste > 0) { PublishTick(ticker, "AJA", sAjuste); PublishTick(ticker, "AJU", sAjuste); }
        if (sClose > 0) { PublishTick(ticker, "FEA", sClose); PublishTick(ticker, "FEC", sClose); }
        if (nQtdBuyer > 0) PublishTick(ticker, "VOC", nQtdBuyer);
        if (nQtdSeller > 0) PublishTick(ticker, "VOV", nQtdSeller);
    }

    private void OnTradeCallback(
        TConnectorAssetIdentifier assetId,
        nint pTrade,
        TConnectorTradeCallbackFlags flags)
    {
        var ticker = assetId.Ticker;
        if (string.IsNullOrEmpty(ticker))
        {
            _logger.LogWarning("[DIAG] OnTradeCallback recebeu Ticker vazio.");
            return;
        }

        var trade = new TConnectorTrade();
        if (ProfitDLL.TranslateTrade(pTrade, ref trade) != NL_OK)
            return;

        _logger.LogDebug("[TRADE] {Ticker} Price={Price} Vol={Vol}", ticker, trade.Price, trade.Volume);

        _assetIdentifiers.TryAdd(ticker, assetId);
        MarketCache.UpdatePrice(ticker, trade.Price);
        _pendingPublishers[ticker] = true;
        _tradesSent++;

        var record = new Marketdata.Database.Models.TradeRecord
        {
            Timestamp = DateTime.UtcNow,
            Ticker = ticker,
            Price = trade.Price,
            Quantity = trade.Quantity,
            BuyAgent = trade.BuyAgent.ToString(),
            SellAgent = trade.SellAgent.ToString(),
            TradeType = trade.TradeType,
            TradeNumber = trade.TradeNumber,
            Side = trade.TradeType.ToString(),
            IsAuction = (trade.TradeType == 4) 
        };

        ChannelProvider.Channel.Writer.TryWrite(record);
    }
    private void OnAccountCallback(
        int nCorretora,
        [MarshalAs(UnmanagedType.LPWStr)] string corretoraNome,
        [MarshalAs(UnmanagedType.LPWStr)] string accountId,
        [MarshalAs(UnmanagedType.LPWStr)] string nomeTitular)
    {
        _logger.LogInformation("Conta: {A} - {N} [{C}]", accountId, nomeTitular, corretoraNome);
    }

    private void OnBrokerAccountListChanged(int nCorretora, int nChanged)
    {
        _logger.LogInformation("BrokerAccountList: Corretora={C} Contas={N}",
            nCorretora, ProfitDLL.GetAccountCountByBroker(nCorretora));
    }

    private void OnBrokerSubAccountListChanged(TConnectorAccountIdentifier accountId)
    {
        _logger.LogInformation("BrokerSubAccount: {A} SubContas={N}",
            accountId.AccountID, ProfitDLL.GetSubAccountCount(ref accountId));
    }

    // Métodos antigos de gerenciamento do OfferBook L3 removidos por estarem desativados e sem consumo
    private void OnOrderCallback(TConnectorOrderIdentifier orderId)
    {
        _logger.LogDebug("{A} Order: Local={L} Cl={C}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), orderId.LocalOrderID, orderId.ClOrderID);
    }

    private void OnOrderHistoryCallback(TConnectorAccountIdentifier accountId)
    {
        _logger.LogInformation("OrderHistory carregado: {A}", accountId.AccountID);
    }

    private void OnAssetListInfoCallbackV2(
        TAssetID assetId,
        [MarshalAs(UnmanagedType.LPWStr)] string strName,
        [MarshalAs(UnmanagedType.LPWStr)] string strDescription,
        int nMinOrderQtd, int nMaxOrderQtd, int nLote,
        int stSecurityType, int ssSecuritySubType,
        double sMinPriceInc, double sContractMultiplier,
        [MarshalAs(UnmanagedType.LPWStr)] string validityDate,
        [MarshalAs(UnmanagedType.LPWStr)] string strISIN,
        [MarshalAs(UnmanagedType.LPWStr)] string strSetor,
        [MarshalAs(UnmanagedType.LPWStr)] string strSubSetor,
        [MarshalAs(UnmanagedType.LPWStr)] string strSegmento)
    {
        if (assetId.Ticker != null && sMinPriceInc > 0)
        {
            _assetTickSizes[assetId.Ticker] = sMinPriceInc;
        }
        _logger.LogDebug("{A} AssetInfo [{T}] {N} ISIN={I}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), assetId.Ticker, strName, strISIN);
    }

    private void OnAdjustHistoryCallbackV2(
        TAssetID assetId,
        double dValue,
        [MarshalAs(UnmanagedType.LPWStr)] string adjustType,
        [MarshalAs(UnmanagedType.LPWStr)] string strObserv,
        [MarshalAs(UnmanagedType.LPWStr)] string dtAjuste,
        [MarshalAs(UnmanagedType.LPWStr)] string dtDeliber,
        [MarshalAs(UnmanagedType.LPWStr)] string dtPagamento,
        int nFlags,
        double dMult)
    {
        _logger.LogDebug("{A} AdjustHistory [{T}] Type={Tp} Value={V}",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), assetId.Ticker, adjustType, dValue);
    }

    private void OnAssetPositionListCallback(
        TConnectorAccountIdentifier accountId,
        TConnectorAssetIdentifier assetId,
        int eventId)
    {
        _logger.LogDebug("AssetPosition [{A}] {T} EventID={E}",
            accountId.AccountID, assetId.Ticker, eventId);
    }

    public double GetVolumeToPrice(string ticker, double targetPrice)
    {
        if (!_assetIdentifiers.TryGetValue(ticker, out var assetID)) return 0;

        // 1. Identificar Direção
        var tick = MarketCache.Get(ticker);
        if (tick == null) return 0;

        double currentPrice = tick.LastPrice > 0 ? tick.LastPrice : (tick.Bid > 0 ? tick.Bid : 0);
        if (currentPrice <= 0) return 0;

        byte side; // 0 = Compra, 1 = Venda
        if (targetPrice > currentPrice) side = 1; // Queremos subir -> consumir a VENDA
        else if (targetPrice < currentPrice) side = 0; // Queremos descer -> consumir a COMPRA
        else return 0;

        int bookSide = side;

        // 2. Obter contagem de níveis
        int count = ProfitDLL.GetPriceDepthSideCount(assetID, bookSide);
        if (count <= 0) return 0;

        double totalQuantity = 0;

        // 3. Iterar e Filtrar
        for (int i = 0; i < count; i++)
        {
            var pg = new TConnectorPriceGroup { Version = 0 };
            if (ProfitDLL.GetPriceGroup(assetID, bookSide, i, ref pg) == NL_OK)
            {
                if (bookSide == 1) // Venda (Preços crescentes)
                {
                    if (pg.Price <= targetPrice)
                        totalQuantity += pg.Quantity;
                    else
                        break;
                }
                else // Compra (Preços decrescentes)
                {
                    if (pg.Price >= targetPrice)
                        totalQuantity += pg.Quantity;
                    else
                        break;
                }
            }
            else break;
        }

        return totalQuantity;
    }

    private void OnTheoreticalPriceCallback(TAssetID assetId, double dTheoreticalPrice, Int64 nTheoreticalQtd)
    {
        var ticker = assetId.Ticker;
        if (string.IsNullOrEmpty(ticker)) return;

        _logger.LogWarning(
        "[LEILAO CALLBACK] {Ticker} Price={Price} Qty={Qty}",
        ticker,
        dTheoreticalPrice,
        nTheoreticalQtd);

        // Atualiza cache de leilão
        MarketCache.UpdateAuction(ticker, dTheoreticalPrice, nTheoreticalQtd);

        // Publica tópicos de tick (compatível com o ProfitService do BussolaDaLiga)
        PublishTick(ticker, "PRT", dTheoreticalPrice);
        PublishTick(ticker, "QTR", (double)nTheoreticalQtd);

        // Dispara publicação do snapshot via SignalR (inclui campos de leilão)
        _pendingPublishers[ticker] = true;
    }

    private void OnChangeStateTickerCallback(TAssetID assetId, string strDate, int nState)
    {
        var ticker = assetId.Ticker;

        if (string.IsNullOrEmpty(ticker)) return;

        _logger.LogWarning(
        "[STATE] {Ticker} -> {StateName} ({State})",
        ticker,
        GetTickerState(nState),
        nState);



        _logger.LogInformation("[DIAG EST] {T} Ticker State Changed: {S} at {D}", ticker, nState, strDate);
        // Publica o estado para o Dashboard (4 = Leilão)
        PublishTick(ticker, "EST", nState);

        // Se o estado for leilão (4), tentamos atualizar o preço teórico imediatamente
        if (nState == 4)
        {
            if (_assetIdentifiers.TryGetValue(ticker, out var assetID))
            {
                if (ProfitDLL.GetTheoreticalValues(assetID, out double price, out long qty) == NL_OK)
                {
                    if (price > 0)
                    {
                        PublishTick(ticker, "PRT", price);
                    }
                }
            }
        }
        else
        {
            // Se saiu do leilão, limpamos os dados teóricos no cache
            _logger.LogInformation("[LEILAO ENCERRADO] {Ticker}", ticker);
            MarketCache.UpdateAuction(ticker, 0, 0, 0);
        }
    }

    public long TestSellBook(string ticker, double targetPrice, int niveis)
    {
        if (!_assetIdentifiers.TryGetValue(ticker, out var assetID)) return 0;

        long totalContratos = 0;
        TConnectorPriceGroup grupoPreco = new TConnectorPriceGroup { Version = 0 };

        // 1. Obtém a quantidade de níveis disponíveis no lado da VENDA (nSide = 1)
        int niveisDisponiveis = ProfitDLL.GetPriceDepthSideCount(assetID, 1);
        int maxIter = Math.Min(niveisDisponiveis, niveis);

        // 2. Loop para percorrer os níveis do book
        for (int i = 0; i < maxIter; i++)
        {
            // Recupera os dados do nível atual
            int resultado = ProfitDLL.GetPriceGroup(assetID, 1, i, ref grupoPreco);

            if (resultado == NL_OK)
            {
                // Verificação de segurança: Ignora se for o preço teórico do leilão no topo
                if (i == 0 && (grupoPreco.PriceGroupFlags & 1) != 0) continue;

                // Se o preço do nível for menor ou igual ao alvo, acumulamos a quantidade
                if (grupoPreco.Price <= targetPrice && grupoPreco.Price > 0)
                {
                    totalContratos += (long)grupoPreco.Quantity;
                }
                else if (grupoPreco.Price > targetPrice)
                {
                    // Como o book de venda é crescente, se ultrapassar o alvo, paramos
                    break;
                }
            }
        }

        return totalContratos;
    }


    public long GetTotalBookVolume(string ticker, int niveis)
    {
        if (!_assetIdentifiers.TryGetValue(ticker, out var assetID)) return 0;

        long totalVolume = 0;
        TConnectorPriceGroup pg = new TConnectorPriceGroup { Version = 0 };

        // Lado da Compra (0)
        int buyNiveis = ProfitDLL.GetPriceDepthSideCount(assetID, 0);
        int maxBuy = Math.Min(buyNiveis, niveis);
        for (int i = 0; i < maxBuy; i++)
        {
            if (ProfitDLL.GetPriceGroup(assetID, 0, i, ref pg) == NL_OK)
            {
                totalVolume += (long)pg.Quantity;
            }
        }

        // Lado da Venda (1)
        int sellNiveis = ProfitDLL.GetPriceDepthSideCount(assetID, 1);
        int maxSell = Math.Min(sellNiveis, niveis);
        for (int i = 0; i < maxSell; i++)
        {
            if (ProfitDLL.GetPriceGroup(assetID, 1, i, ref pg) == NL_OK)
            {
                totalVolume += (long)pg.Quantity;
            }
        }

        return totalVolume;
    }

    public long TestBuyBook(string ticker, double targetPrice, int niveis)
    {
        if (!_assetIdentifiers.TryGetValue(ticker, out var assetID)) return 0;

        long totalContratos = 0;
        TConnectorPriceGroup grupoPreco = new TConnectorPriceGroup { Version = 0 };

        // 1. Obtém a quantidade de níveis disponíveis no lado da COMPRA (nSide = 0)
        int niveisDisponiveis = ProfitDLL.GetPriceDepthSideCount(assetID, 0);
        int maxIter = Math.Min(niveisDisponiveis, niveis);

        // 2. Loop para percorrer os níveis do book
        for (int i = 0; i < maxIter; i++)
        {
            // Recupera os dados do nível atual
            int resultado = ProfitDLL.GetPriceGroup(assetID, 0, i, ref grupoPreco);

            if (resultado == NL_OK)
            {
                // Verificação de segurança: Ignora se for o preço teórico do leilão no topo
                if (i == 0 && (grupoPreco.PriceGroupFlags & 1) != 0) continue;

                // Se o preço do nível for maior ou igual ao alvo (book de compra é decrescente), acumulamos a quantidade
                if (grupoPreco.Price >= targetPrice && grupoPreco.Price > 0)
                {
                    totalContratos += (long)grupoPreco.Quantity;
                }
                else if (grupoPreco.Price < targetPrice)
                {
                    // Como o book de compra é decrescente, se for menor que o alvo, paramos
                    break;
                }
            }
            else
            {
                _logger.LogInformation(
                    "[LEILAO ENCERRADO] {Ticker}",
                    ticker);

                MarketCache.UpdateAuction(
                    ticker,
                    0,
                    0,
                    0);
            }
        }
        return totalContratos;
    }
    private string GetTickerState(int state)
    {
        return state switch
        {
            0 => "Normal",
            1 => "Fechado",
            2 => "Call Fechamento",
            3 => "Call Abertura",
            4 => "Leilao",
            5 => "Volatilidade",
            _ => "Desconhecido"
        };
    }
}

public class PriceGroupCache
{
    public double Price { get; set; }
    public double Quantity { get; set; }
}

public class TickerBookCache
{
    public List<PriceGroupCache> Bids { get; set; } = new();
    public List<PriceGroupCache> Asks { get; set; } = new();
}

public class TinyBookTopCache
{
    public double BidPrice { get; set; }
    public int BidQty { get; set; }
    public double AskPrice { get; set; }
    public int AskQty { get; set; }
    public DateTime LastUpdateUtc { get; set; }

    public bool HasBid => BidPrice > 0;
    public bool HasAsk => AskPrice > 0;
}
