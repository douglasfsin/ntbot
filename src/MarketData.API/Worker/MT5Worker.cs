using MarketData.API.Configuration;
using MarketData.API.Data;
using MarketData.API.Data.Model;
using MarketData.API.SignalR;
using MarketData.API.Worker;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ProfitDLLClient;
using System.Collections.Concurrent;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarketData.API.MT5Worker
{
    /// <summary>
    /// Worker que consome dados em tempo real da API Python (Flask + MT5)
    /// via Server-Sent Events (SSE) e os distribui via SignalR (MT5Hub).
    ///
    /// Fluxo: Python Flask API â†’ SSE â†’ MT5Worker â†’ MT5Hub (SignalR) â†’ BussolaDaLiga (MT5Service)
    ///
    /// Allowlist only: for each symbol in <c>MT5:Symbols</c> (appsettings), opens SSE:
    ///   GET {PythonApiBaseUrl}/api/stream/all/{symbol}
    /// and processes events: tick | book | volume. Never auto-subscribes Market Watch.
    /// </summary>
    public class MT5Worker : BackgroundService
    {
        private readonly ILogger<MT5Worker> _logger;
        private readonly MT5Settings _settings;
        private readonly IHubContext<MarketHub> _hub;
        //private readonly IHubContext<MT5Hub> _hub;
        private readonly IHttpClientFactory _httpFactory;

        // Cache de snapshots indexado por sÃ­mbolo
        private readonly ConcurrentDictionary<string, MT5Snapshot> _cache = new();

        // DiagnÃ³stico
        private int _ticksReceived = 0;
        private int _booksReceived = 0;
        private int _broadcastsSent = 0;
        private DateTime _lastDataReceived = DateTime.MinValue;
        private System.Timers.Timer? _diagnosticTimer;

        public bool IsConnected => _lastDataReceived != DateTime.MinValue && (DateTime.Now - _lastDataReceived).TotalSeconds < 60;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public MT5Worker(
            ILogger<MT5Worker> logger,
            IOptions<MT5Settings> settings,
            IHubContext<MarketHub> hub,
            IHttpClientFactory httpFactory)
        {
            _logger = logger;
            _settings = settings.Value;
            _hub = hub;
            _httpFactory = httpFactory;
        }

        // =========================================================================
        // BackgroundService
        // =========================================================================

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_settings.Enabled)
            {
                _logger.LogInformation("[MT5Worker] Desabilitado por configuraÃ§Ã£o. Encerrando.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_settings.PythonApiBaseUrl))
            {
                _logger.LogWarning("[MT5Worker] PythonApiBaseUrl nÃ£o configurada. Encerrando.");
                return;
            }

            var symbolsToStream = GetSymbolsToStream();
            if (symbolsToStream == null || symbolsToStream.Count == 0)
            {
                _logger.LogWarning("[MT5Worker] Nenhum sÃ­mbolo configurado ou encontrado para monitoramento. Encerrando.");
                return;
            }

            _logger.LogInformation("[MT5Worker] Iniciado. Python API: {Url} | SÃ­mbolos: {S}",
                _settings.PythonApiBaseUrl, string.Join(", ", symbolsToStream));

            StartDiagnosticTimer();

            // Uma task SSE independente por sÃ­mbolo
            var tasks = symbolsToStream
                .Select(symbol => Task.Run(() => StreamSymbolAsync(symbol, stoppingToken), stoppingToken));

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Allowlist only — <c>MT5:Symbols</c> from appsettings. Never auto-subscribes Market Watch
        /// or external ticker files (e.g. yfinance).
        /// </summary>
        private List<string> GetSymbolsToStream()
        {
            if (_settings.Symbols is null || _settings.Symbols.Count == 0)
                return [];

            return _settings.Symbols
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _diagnosticTimer?.Stop();
            _diagnosticTimer?.Dispose();
            _logger.LogInformation("[MT5Worker] Encerrando.");
            await base.StopAsync(cancellationToken);
        }

        // =========================================================================
        // SSE: loop por sÃ­mbolo com reconexÃ£o e backoff exponencial
        // =========================================================================

        private async Task StreamSymbolAsync(string symbol, CancellationToken ct)
        {
            var url = $"{_settings.PythonApiBaseUrl.TrimEnd('/')}/api/stream/all/{symbol}";
            int attempt = 0;

            while (!ct.IsCancellationRequested)
            {
                attempt++;
                try
                {
                    _logger.LogInformation("[MT5Worker][{Symbol}] Conectando SSE (#{A}): {Url}", symbol, attempt, url);

                    using var client = _httpFactory.CreateClient("MT5Python");
                    
                    // 1. Busca fechamento anterior para cálculo de variação
                    try
                    {
                        var histUrl = $"{_settings.PythonApiBaseUrl.TrimEnd('/')}/api/ohlcv/{symbol}?timeframe=D1&count=2";
                        var histResp = await client.GetAsync(histUrl, ct);
                        if (histResp.IsSuccessStatusCode)
                        {
                            var histJson = await histResp.Content.ReadAsStringAsync(ct);
                            using var doc = JsonDocument.Parse(histJson);
                            var candles = doc.RootElement.GetProperty("candles");
                            if (candles.GetArrayLength() >= 2)
                            {
                                // O último candle [1] é o dia atual, o anterior [0] é o fechamento de referência
                                var prevClose = candles[0].GetProperty("close").GetDouble();
                                var snap = _cache.GetOrAdd(symbol, _ => new MT5Snapshot { Symbol = symbol });
                                snap.PreviousClose = prevClose;
                                _logger.LogInformation("[MT5Worker][{Symbol}] Preço de referência (Ontem): {P}", symbol, prevClose);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("[MT5Worker][{Symbol}] Falha ao obter histórico para variação: {Msg}", symbol, ex.Message);
                    }

                    // 2. Conecta ao Stream SSE
                    // ResponseHeadersRead = não faz buffer do body, lê como stream
                    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                    response.EnsureSuccessStatusCode();

                    _logger.LogInformation("[MT5Worker][{Symbol}] ✓ SSE conectado.", symbol);
                    attempt = 0; // reset após conexão bem-sucedida

                    using var stream = await response.Content.ReadAsStreamAsync(ct);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    await ReadSseStreamAsync(symbol, reader, ct);

                    _logger.LogWarning("[MT5Worker][{Symbol}] Stream SSE encerrado pelo servidor.", symbol);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    int delay = GetBackoffSeconds(attempt);
                    _logger.LogWarning("[MT5Worker][{Symbol}] Erro SSE: {Msg}. Reconectando em {D}s...",
                        symbol, ex.Message, delay);
                    try { await Task.Delay(TimeSpan.FromSeconds(delay), ct); }
                    catch (OperationCanceledException) { break; }
                }
            }

            _logger.LogInformation("[MT5Worker][{Symbol}] Task SSE encerrada.", symbol);
        }

        // =========================================================================
        // Parser SSE (Server-Sent Events)
        // =========================================================================

        private async Task ReadSseStreamAsync(string symbol, StreamReader reader, CancellationToken ct)
        {
            string? eventType = null;
            var dataBuffer = new StringBuilder();

            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break; // stream fechado

                if (line.StartsWith("event:"))
                {
                    eventType = line["event:".Length..].Trim();
                }
                else if (line.StartsWith("data:"))
                {
                    dataBuffer.Append(line["data:".Length..].Trim());
                }
                else if (line.Length == 0) // linha vazia = fim do evento SSE
                {
                    if (eventType != null && dataBuffer.Length > 0)
                        await ProcessSseEventAsync(symbol, eventType, dataBuffer.ToString(), ct);

                    eventType = null;
                    dataBuffer.Clear();
                }
                // Linhas ": comment" ou "id:" sÃ£o ignoradas
            }
        }

        private async Task ProcessSseEventAsync(string symbol, string eventType, string json, CancellationToken ct)
        {
            try
            {
                _lastDataReceived = DateTime.Now;

                switch (eventType)
                {
                    case "tick":
                        var tick = JsonSerializer.Deserialize<PyTickDto>(json, _jsonOpts);
                        if (tick != null) await ProcessTickAsync(symbol, tick);
                        break;

                    case "book":
                        var book = JsonSerializer.Deserialize<PyBookDto>(json, _jsonOpts);
                        if (book != null) await ProcessBookAsync(symbol, book);
                        break;

                    case "volume":
                        var vol = JsonSerializer.Deserialize<PyVolumeDto>(json, _jsonOpts);
                        if (vol != null) UpdateVolumeCache(symbol, vol);
                        break;

                    case "variation":
                        var variation = JsonSerializer.Deserialize<PyVariationDto>(json, _jsonOpts);
                        if (variation != null) await ProcessVariationAsync(symbol, variation);
                        break;

                    case "error":
                        _logger.LogWarning("[MT5Worker][{Symbol}] Erro reportado pela Python API: {Json}", symbol, json);
                        break;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug("[MT5Worker][{Symbol}] JSON invÃ¡lido no evento '{E}': {Ex}", symbol, eventType, ex.Message);
            }
        }

        // =========================================================================
        // Processamento e broadcast
        // =========================================================================

        private async Task ProcessTickAsync(string symbol, PyTickDto tick)
        {
            var snap = _cache.GetOrAdd(symbol, _ => new MT5Snapshot { Symbol = symbol });
            snap.Last = tick.Last;
            snap.Bid = tick.Bid;
            snap.Ask = tick.Ask;
            snap.Spread = tick.Spread;
            snap.Volume = tick.Volume;
            snap.LastUpdate = DateTime.UtcNow;

            Interlocked.Increment(ref _ticksReceived);

            // Cálculo de variação
            // Opcional: Se o tick trouxer as infos de variação nativamente e ainda quisermos a local, mantemos o if.
            // Pelo novo modelo, a variação oficial virá pelo evento 'variation'.
            if (snap.PreviousClose > 0)
                snap.Variation = ((tick.Last > 0 ? tick.Last : tick.Bid) - snap.PreviousClose) / snap.PreviousClose * 100.0;

            // Broadcast tópicos compatíveis com MT5Service do BussolaDaLiga
            if (_hub != null)
            {
                await _hub.Clients.Group(symbol).SendAsync("tick", symbol, "ULT", tick.Last > 0 ? tick.Last : tick.Bid);
                await _hub.Clients.Group(symbol).SendAsync("tick", symbol, "BID", tick.Bid);
                await _hub.Clients.Group(symbol).SendAsync("tick", symbol, "ASK", tick.Ask);
                await _hub.Clients.Group(symbol).SendAsync("tick", symbol, "SPREAD", tick.Spread);
                await _hub.Clients.Group(symbol).SendAsync("tick", symbol, "VAR", snap.Variation);
            }

            Interlocked.Increment(ref _broadcastsSent);
        }

        private async Task ProcessVariationAsync(string symbol, PyVariationDto variation)
        {
            var snap = _cache.GetOrAdd(symbol, _ => new MT5Snapshot { Symbol = symbol });
            snap.IntradayVariation = variation.Intraday?.Percentage ?? 0;
            snap.PrevDayVariation = variation.PrevDay?.Percentage ?? 0;

            if (_hub != null)
            {
                await _hub.Clients.Group(symbol).SendAsync("tick", symbol, "VAR_INTRADAY", snap.IntradayVariation);
                await _hub.Clients.Group(symbol).SendAsync("tick", symbol, "VAR_PREVDAY", snap.PrevDayVariation);
            }
        }

        private async Task ProcessBookAsync(string symbol, PyBookDto book)
        {
            var snap = _cache.GetOrAdd(symbol, _ => new MT5Snapshot { Symbol = symbol });
            snap.Bids = book.Bids ?? [];
            snap.Asks = book.Asks ?? [];
            snap.TotalBidVolume = book.TotalBidVolume;
            snap.TotalAskVolume = book.TotalAskVolume;
            snap.LastUpdate = DateTime.UtcNow;

            // Melhor bid/ask vem do primeiro nÃ­vel do book
            var bestBid = book.Bids?.FirstOrDefault()?.Price ?? snap.Bid;
            var bestBidVolume = book.Bids?.FirstOrDefault()?.Volume ?? snap.Volume;

            var bestAsk = book.Asks?.FirstOrDefault()?.Price ?? snap.Ask;
            var bestAskVolume = book.Asks?.FirstOrDefault()?.Volume ?? snap.Volume;

            snap.Bid = bestBid;
            snap.Ask = bestAsk;
            
            Interlocked.Increment(ref _booksReceived);

            // Atualização rápida do cache (sem somas de profundidade)
            // COMPRA (Bid)
            double bidN = bestBidVolume;
            double bidFull = snap.TotalBidVolume;
            MarketCache.UpdateBidDepth(symbol, bidN);
            MarketCache.UpdateBidFullDepth(symbol, bidFull);
            MarketCache.UpdateBook(symbol, true, bestBid, bestBidVolume, 0);
            _logger.LogDebug("[BOOK-DEBUG] Ticker={T} SIDE=BID Price={P} Qty={Q}", symbol, bestBid, bestBidVolume);
            // VENDA (Ask)
            double askN = bestAskVolume;
            double askFull = snap.TotalAskVolume;
            MarketCache.UpdateAskDepth(symbol, askN);
            MarketCache.UpdateAskFullDepth(symbol, askFull);
            MarketCache.UpdateBook(symbol, false, bestAsk, bestAskVolume, 0);
            _logger.LogDebug("[BOOK-DEBUG] Ticker={T} SIDE=ASK Price={P} Qty={Q}", symbol, bestAsk, bestAskVolume);


            var publishBook = new OrderBook();
            var bidsCount = snap.Bids.Count;
            var asksCount = snap.Asks.Count;
            foreach (var bidObject in snap.Bids)
                publishBook.Bids.Add(--bidsCount, new OrderBookLevel { Price = bidObject.Price, Quantity = (int)bidObject.Volume });

            foreach (var askObject in snap.Asks)
                publishBook.Asks.Add(--asksCount, new OrderBookLevel { Price = askObject.Price, Quantity = (int)askObject.Volume });
            //_ = _hub.Clients.Group(payload.Ticker).SendAsync("book", payload.Ticker, payload.Bid, payload.Ask);
            _= PublishTopOfBook(snap.Symbol, publishBook);

            Interlocked.Increment(ref _broadcastsSent);
        }
        private void UpdateVolumeCache(string symbol, PyVolumeDto vol)
        {
            var snap = _cache.GetOrAdd(symbol, _ => new MT5Snapshot { Symbol = symbol });
            snap.TickVolumeCurrentBar = vol.TickVolumeCurrentBar;
            snap.VolumeToday = vol.VolumeToday;
            snap.LastUpdate = DateTime.UtcNow;
        }

        // =========================================================================
        // IngestÃ£o manual via REST (MT5Controller â€” compatibilidade com EA MQL5)
        // =========================================================================

        public void IngestTick(string ticker, string topic, double value)
        {
            if (string.IsNullOrWhiteSpace(ticker)) return;

            var snap = _cache.GetOrAdd(ticker, _ => new MT5Snapshot { Symbol = ticker });
            if (topic == "ULT") snap.Last = value;
            else if (topic == "BID") snap.Bid = value;
            else if (topic == "ASK") snap.Ask = value;
            snap.LastUpdate = DateTime.UtcNow;

            Interlocked.Increment(ref _ticksReceived);
            _lastDataReceived = DateTime.Now;

            _ = _hub.Clients.Group(ticker).SendAsync("tick", ticker, topic, value);
        }

        public void IngestBook(MT5BookPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Ticker)) return;

            var snap = _cache.GetOrAdd(payload.Ticker, _ => new MT5Snapshot { Symbol = payload.Ticker });
            snap.Bid = payload.Bid;
            snap.Ask = payload.Ask;
            snap.LastUpdate = DateTime.UtcNow;

            Interlocked.Increment(ref _booksReceived);
            _lastDataReceived = DateTime.Now;
            var book = new OrderBook();
            int bidsIdx = 0;
            foreach (var bidObject in snap.Bids)
                book.Bids.Add(bidsIdx++, new OrderBookLevel { Price = bidObject.Price, Quantity = (int)bidObject.Volume });
            
            int asksIdx = 0;
            foreach (var askObject in snap.Asks)
                book.Asks.Add(asksIdx++, new OrderBookLevel { Price = askObject.Price, Quantity = (int)askObject.Volume });
            _= PublishTopOfBook(snap.Symbol, book);
        }
        private async Task PublishTopOfBook(string ticker, OrderBook book)
        {
            OrderBookLevel? bestBid = null;
            OrderBookLevel? bestAsk = null;

            lock (book.Bids)
            {
                if (book.Bids.Count > 0)
                    bestBid = book.Bids.First().Value;
            }

            lock (book.Asks)
            {
                if (book.Asks.Count > 0)
                    bestAsk = book.Asks.First().Value;
            }

            if (bestBid == null && bestAsk == null)
                return;

            var snapshot = new BookSnapshot
            {
                Ticker = ticker,
                BestBid = bestBid?.Price ?? 0,
                BestBidQty = bestBid?.Quantity ?? 0,
                BestAsk = bestAsk?.Price ?? 0,
                BestAskQty = bestAsk?.Quantity ?? 0,
                BestBidQty5 = bestBid?.Quantity ?? 0,
                BestAskQty5 = bestAsk?.Quantity ?? 0,
                Timestamp = DateTime.UtcNow
            };

            // 🔥 cache leve
            MarketCache.UpdateBook(
                ticker,
                true,
                snapshot.BestBid,
                snapshot.BestBidQty,
                snapshot.BestBidQty5
            );

            MarketCache.UpdateBook(
                ticker,
                false,
                snapshot.BestAsk,
                snapshot.BestAskQty,
                snapshot.BestAskQty5
            );

            // 🔥 tempo real
            await _hub.Clients.Group(ticker)
                .SendAsync("book", snapshot);
        }
        // =========================================================================
        // Backoff exponencial (em segundos)
        // =========================================================================

        private static int GetBackoffSeconds(int attempt) => attempt switch
        {
            <= 2 => 5,
            <= 4 => 15,
            <= 6 => 30,
            _ => 60
        };

        // =========================================================================
        // Consulta de cache (usada pelo MT5Controller)
        // =========================================================================

        public MT5Snapshot? GetSnapshot(string symbol) =>
            _cache.TryGetValue(symbol, out var snap) ? snap : null;

        public IReadOnlyDictionary<string, MT5Snapshot> GetAllSnapshots() => _cache;

        // =========================================================================
        // DiagnÃ³stico
        // =========================================================================

        private void StartDiagnosticTimer()
        {
            _diagnosticTimer = new System.Timers.Timer(30_000);
            _diagnosticTimer.Elapsed += (_, _) =>
            {
                var inactiveSecs = _lastDataReceived == DateTime.MinValue
                    ? -1 : (int)(DateTime.Now - _lastDataReceived).TotalSeconds;
                _logger.LogInformation(
                    "[MT5Worker DIAG] Ticks={T} | Books={B} | Broadcasts={C} | Ãšltimo dado hÃ¡ {S}s | SÃ­mbolos={N}",
                    _ticksReceived, _booksReceived, _broadcastsSent, inactiveSecs, _cache.Count);
            };
            _diagnosticTimer.AutoReset = true;
            _diagnosticTimer.Start();
        }
    }

    // =========================================================================
    // Modelo de cache interno
    // =========================================================================

    public class MT5Snapshot
    {
        public string Symbol { get; set; } = "";
        public double Last { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double Spread { get; set; }
        public long Volume { get; set; }
        public double PreviousClose { get; set; }
        public double Variation { get; set; }
        public double IntradayVariation { get; set; }
        public double PrevDayVariation { get; set; }
        public List<PyBookLevel> Bids { get; set; } = [];
        public List<PyBookLevel> Asks { get; set; } = [];
        public double TotalBidVolume { get; set; }
        public double TotalAskVolume { get; set; }
        public long TickVolumeCurrentBar { get; set; }
        public long VolumeToday { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    // =========================================================================
    // DTOs da Python API (desserializaÃ§Ã£o SSE JSON)
    // =========================================================================

    public record PyTickDto(
        [property: JsonPropertyName("symbol")] string Symbol,
        [property: JsonPropertyName("bid")] double Bid,
        [property: JsonPropertyName("ask")] double Ask,
        [property: JsonPropertyName("last")] double Last,
        [property: JsonPropertyName("spread")] double Spread,
        [property: JsonPropertyName("volume")] long Volume,
        [property: JsonPropertyName("volume_real")] double VolumeReal
    );

    public record PyBookDto(
        [property: JsonPropertyName("symbol")] string Symbol,
        [property: JsonPropertyName("bids")] List<PyBookLevel> Bids,
        [property: JsonPropertyName("asks")] List<PyBookLevel> Asks,
        [property: JsonPropertyName("total_bid_volume")] double TotalBidVolume,
        [property: JsonPropertyName("total_ask_volume")] double TotalAskVolume
    );

    public record PyBookLevel(
        [property: JsonPropertyName("price")] double Price,
        [property: JsonPropertyName("volume")] long Volume,
        [property: JsonPropertyName("volume_dbl")] double VolumeDbl
    );

    public record PyVolumeDto(
        [property: JsonPropertyName("symbol")] string Symbol,
        [property: JsonPropertyName("tick_volume_current_bar")] long TickVolumeCurrentBar,
        [property: JsonPropertyName("real_volume_current_bar")] double RealVolumeCurrentBar,
        [property: JsonPropertyName("volume_today")] long VolumeToday
    );

    public record PyVariationDetailsDto(
        [property: JsonPropertyName("points")] double Points,
        [property: JsonPropertyName("percentage")] double Percentage
    );

    public record PyVariationDto(
        [property: JsonPropertyName("symbol")] string Symbol,
        [property: JsonPropertyName("price_current")] double PriceCurrent,
        [property: JsonPropertyName("intraday")] PyVariationDetailsDto Intraday,
        [property: JsonPropertyName("prev_day")] PyVariationDetailsDto PrevDay
    );

    // =========================================================================
    // Payloads para ingestÃ£o manual via REST (compatibilidade MT5Controller)
    // =========================================================================

    public class MT5BookPayload
    {
        public string Ticker { get; set; } = "";
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double BidVolume { get; set; }
        public double AskVolume { get; set; }
    }

    public class MT5TickPayload
    {
        public string Ticker { get; set; } = "";
        public string Topic { get; set; } = "ULT";
        public double Value { get; set; }
    }
}

