using NtBot.Domain.Entities;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace NtBot.Api.Services.NinjaTrader
{
    /// <summary>
    /// Implementação da comunicação com NinjaTrader via WebSocket/REST
    /// Nota: Esta é uma implementação de referência. A integração real depende da API do NinjaTrader.
    /// Para NT8, usar ATI (Automated Trading Interface) ou NinjaScript exportado como serviço.
    /// </summary>
    public class NinjaTraderService : INinjaTraderService, IDisposable
    {
        private readonly ILogger<NinjaTraderService> _logger;
        private readonly HttpClient _httpClient;
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cancellationTokenSource;
        private string? _apiKey;
        private string? _accountId;

        private bool _isConnected;

        // Subscribers para market data em tempo real
        private readonly Dictionary<string, List<Action<Candle>>> _marketDataSubscribers = new();

        // Events
        public event EventHandler<string>? ConnectionLost;
        public event EventHandler<string>? OrderFilled;
        public event EventHandler<string>? OrderCancelled;
        public event EventHandler<Trade>? PositionOpened;
        public event EventHandler<Trade>? PositionClosed;

        public bool IsConnected => _isConnected;

        public NinjaTraderService(ILogger<NinjaTraderService> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("NinjaTrader");
        }

        #region Connection

        public async Task<bool> ConnectAsync(string apiKey, string accountId)
        {
            try
            {
                _apiKey = apiKey;
                _accountId = accountId;
                
                // Teste de conexão REST
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                var testResponse = await _httpClient.GetAsync($"/api/accounts/{accountId}/status");
                
                if (!testResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to connect to NinjaTrader API: {StatusCode}", testResponse.StatusCode);
                    return false;
                }

                // Conecta WebSocket para dados em tempo real
                await ConnectWebSocketAsync();
                
                _isConnected = true;
                _logger.LogInformation("Successfully connected to NinjaTrader");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to NinjaTrader");
                _isConnected = false;
                return false;
            }
        }

        private async Task ConnectWebSocketAsync()
        {
            try
            {
                _webSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();
                
                var wsUri = new Uri($"wss://localhost:8080/ws/marketdata?apiKey={_apiKey}");
                await _webSocket.ConnectAsync(wsUri, _cancellationTokenSource.Token);
                
                // Inicia loop de recebimento de mensagens
                _ = Task.Run(() => ReceiveMessagesAsync(_cancellationTokenSource.Token));
                
                _logger.LogInformation("WebSocket connected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting WebSocket");
                throw;
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[1024 * 4];
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await HandleConnectionLostAsync();
                        break;
                    }
                    
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessWebSocketMessageAsync(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving WebSocket messages");
                await HandleConnectionLostAsync();
            }
        }

        private async Task ProcessWebSocketMessageAsync(string message)
        {
            try
            {
                var data = JsonSerializer.Deserialize<WebSocketMessage>(message);
                
                if (data == null) return;

                switch (data.Type)
                {
                    case "CANDLE":
                        if (data.Candle != null)
                        {
                            NotifyMarketDataSubscribers(data.Symbol, data.Candle);
                        }
                        break;
                    
                    case "ORDER_FILLED":
                        OrderFilled?.Invoke(this, data.OrderNumber ?? "");
                        break;
                    
                    case "ORDER_CANCELLED":
                        OrderCancelled?.Invoke(this, data.OrderNumber ?? "");
                        break;
                    
                    case "POSITION_OPENED":
                        if (data.Trade != null)
                        {
                            PositionOpened?.Invoke(this, data.Trade);
                        }
                        break;
                    
                    case "POSITION_CLOSED":
                        if (data.Trade != null)
                        {
                            PositionClosed?.Invoke(this, data.Trade);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WebSocket message: {Message}", message);
            }
        }

        private async Task HandleConnectionLostAsync()
        {
            _isConnected = false;
            ConnectionLost?.Invoke(this, "WebSocket connection lost");
            _logger.LogWarning("Connection to NinjaTrader lost. Attempting to reconnect...");
            
            // TODO: Implementar lógica de reconexão automática
            await Task.Delay(5000);
            if (_apiKey != null && _accountId != null)
            {
                await ConnectAsync(_apiKey, _accountId);
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                
                if (_webSocket?.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);
                }
                
                _isConnected = false;
                _logger.LogInformation("Disconnected from NinjaTrader");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from NinjaTrader");
            }
        }

        #endregion

        #region Market Data

        public async Task<Candle?> GetLatestCandleAsync(string symbol, string timeframe)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/marketdata/{symbol}/{timeframe}/latest");
                response.EnsureSuccessStatusCode();
                
                var candle = await response.Content.ReadFromJsonAsync<Candle>();
                return candle;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest candle for {Symbol} {Timeframe}", symbol, timeframe);
                return null;
            }
        }

        public async Task<List<Candle>> GetHistoricalCandlesAsync(string symbol, string timeframe, DateTime from, DateTime to)
        {
            try
            {
                var fromStr = from.ToString("yyyy-MM-ddTHH:mm:ss");
                var toStr = to.ToString("yyyy-MM-ddTHH:mm:ss");
                
                var response = await _httpClient.GetAsync(
                    $"/api/marketdata/{symbol}/{timeframe}/history?from={fromStr}&to={toStr}");
                response.EnsureSuccessStatusCode();
                
                var candles = await response.Content.ReadFromJsonAsync<List<Candle>>();
                return candles ?? new List<Candle>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting historical candles for {Symbol} {Timeframe}", symbol, timeframe);
                return new List<Candle>();
            }
        }

        public async Task SubscribeToMarketDataAsync(string symbol, Action<Candle> onCandleReceived)
        {
            if (!_marketDataSubscribers.ContainsKey(symbol))
            {
                _marketDataSubscribers[symbol] = new List<Action<Candle>>();
                
                // Envia mensagem de subscrição via WebSocket
                if (_webSocket?.State == WebSocketState.Open)
                {
                    var subscribeMessage = new
                    {
                        action = "SUBSCRIBE",
                        symbol = symbol
                    };
                    var json = JsonSerializer.Serialize(subscribeMessage);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            
            _marketDataSubscribers[symbol].Add(onCandleReceived);
            _logger.LogInformation("Subscribed to market data for {Symbol}", symbol);
        }

        public async Task UnsubscribeFromMarketDataAsync(string symbol)
        {
            if (_marketDataSubscribers.ContainsKey(symbol))
            {
                _marketDataSubscribers.Remove(symbol);
                
                // Envia mensagem de unsubscribe via WebSocket
                if (_webSocket?.State == WebSocketState.Open)
                {
                    var unsubscribeMessage = new
                    {
                        action = "UNSUBSCRIBE",
                        symbol = symbol
                    };
                    var json = JsonSerializer.Serialize(unsubscribeMessage);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                
                _logger.LogInformation("Unsubscribed from market data for {Symbol}", symbol);
            }
        }

        private void NotifyMarketDataSubscribers(string symbol, Candle candle)
        {
            if (_marketDataSubscribers.TryGetValue(symbol, out var subscribers))
            {
                foreach (var subscriber in subscribers)
                {
                    try
                    {
                        subscriber(candle);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error notifying market data subscriber for {Symbol}", symbol);
                    }
                }
            }
        }

        #endregion

        #region Order Execution

        public async Task<string> PlaceMarketOrderAsync(string symbol, TradeDirection direction, int quantity)
        {
            try
            {
                var order = new
                {
                    symbol,
                    direction = direction.ToString(),
                    quantity,
                    type = "MARKET",
                    accountId = _accountId
                };
                
                var response = await _httpClient.PostAsJsonAsync("/api/orders", order);
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<OrderResponse>();
                _logger.LogInformation("Market order placed: {OrderNumber}", result?.OrderNumber);
                
                return result?.OrderNumber ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error placing market order for {Symbol}", symbol);
                throw;
            }
        }

        public async Task<string> PlaceLimitOrderAsync(string symbol, TradeDirection direction, int quantity, decimal price)
        {
            try
            {
                var order = new
                {
                    symbol,
                    direction = direction.ToString(),
                    quantity,
                    price,
                    type = "LIMIT",
                    accountId = _accountId
                };
                
                var response = await _httpClient.PostAsJsonAsync("/api/orders", order);
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<OrderResponse>();
                return result?.OrderNumber ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error placing limit order for {Symbol}", symbol);
                throw;
            }
        }

        public async Task<string> PlaceStopOrderAsync(string symbol, TradeDirection direction, int quantity, decimal stopPrice)
        {
            try
            {
                var order = new
                {
                    symbol,
                    direction = direction.ToString(),
                    quantity,
                    stopPrice,
                    type = "STOP",
                    accountId = _accountId
                };
                
                var response = await _httpClient.PostAsJsonAsync("/api/orders", order);
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<OrderResponse>();
                return result?.OrderNumber ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error placing stop order for {Symbol}", symbol);
                throw;
            }
        }

        public async Task<bool> CancelOrderAsync(string orderNumber)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/orders/{orderNumber}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderNumber}", orderNumber);
                return false;
            }
        }

        public async Task<bool> ModifyOrderAsync(string orderNumber, decimal? newStopLoss, decimal? newTakeProfit)
        {
            try
            {
                var modification = new
                {
                    stopLoss = newStopLoss,
                    takeProfit = newTakeProfit
                };
                
                var response = await _httpClient.PutAsJsonAsync($"/api/orders/{orderNumber}", modification);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error modifying order {OrderNumber}", orderNumber);
                return false;
            }
        }

        #endregion

        #region Position Management

        public async Task<List<Position>> GetOpenPositionsAsync(string? symbol = null)
        {
            try
            {
                var url = symbol != null 
                    ? $"/api/positions?symbol={symbol}&accountId={_accountId}"
                    : $"/api/positions?accountId={_accountId}";
                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var positions = await response.Content.ReadFromJsonAsync<List<Position>>();
                return positions ?? new List<Position>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting open positions");
                return new List<Position>();
            }
        }

        public async Task<bool> ClosePositionAsync(string orderNumber)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/positions/{orderNumber}/close", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing position {OrderNumber}", orderNumber);
                return false;
            }
        }

        public async Task<bool> CloseAllPositionsAsync(string symbol)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/positions/close-all?symbol={symbol}&accountId={_accountId}", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing all positions for {Symbol}", symbol);
                return false;
            }
        }

        #endregion

        #region Account Info

        public async Task<decimal> GetAccountBalanceAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/accounts/{_accountId}/balance");
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<AccountInfo>();
                return result?.Balance ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account balance");
                return 0;
            }
        }

        public async Task<decimal> GetBuyingPowerAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/accounts/{_accountId}/buying-power");
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<AccountInfo>();
                return result?.BuyingPower ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting buying power");
                return 0;
            }
        }

        #endregion

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _webSocket?.Dispose();
            _httpClient?.Dispose();
        }

        #region Helper Classes

        private class WebSocketMessage
        {
            public string Type { get; set; } = string.Empty;
            public string Symbol { get; set; } = string.Empty;
            public string? OrderNumber { get; set; }
            public Candle? Candle { get; set; }
            public Trade? Trade { get; set; }
        }

        private class OrderResponse
        {
            public string OrderNumber { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }

        private class AccountInfo
        {
            public decimal Balance { get; set; }
            public decimal BuyingPower { get; set; }
        }

        #endregion
    }
}
