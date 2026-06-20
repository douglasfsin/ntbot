#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using Newtonsoft.Json;
#endregion

//This namespace holds AddOns in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.AddOns
{
    /// <summary>
    /// NTBot HTTP Server - Integração com NTBot.Api via REST API
    /// 
    /// Instalação:
    /// 1. Copie este código para: Tools → Edit NinjaScript → AddOn → New
    /// 2. Nomeie como: NTBotHttpServer
    /// 3. Compile (F5)
    /// 4. Ative em: Tools → AddOns → NTBot HTTP Server → Enable
    /// 
    /// Endpoints disponíveis:
    /// - GET  /api/health      - Health check
    /// - GET  /api/accounts    - Lista de contas
    /// - GET  /api/positions   - Posições abertas
    /// - GET  /api/orders      - Lista de ordens
    /// - POST /api/orders      - Criar nova ordem
    /// - GET  /api/marketdata  - Dados de mercado (último preço)
    /// </summary>
    public class NTBotHttpServer : AddOnBase
    {
        private HttpListener listener;
        private const int PORT = 8080;
        private bool isRunning = false;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"HTTP REST API Server para integração com NTBot
Porta: 8080
Endpoints: /api/health, /api/accounts, /api/positions, /api/orders, /api/marketdata";
                Name = "NTBot HTTP Server";
            }
            else if (State == State.Active)
            {
                StartServer();
            }
            else if (State == State.Terminated)
            {
                StopServer();
            }
        }

        private void StartServer()
        {
            try
            {
                if (listener != null && listener.IsListening)
                {
                    Print("⚠️ Servidor já está rodando");
                    return;
                }

                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{PORT}/");
                listener.Prefixes.Add($"http://127.0.0.1:{PORT}/");
                listener.Start();
                isRunning = true;

                Task.Run(() => HandleRequests());

                Print($"✅ NTBot HTTP Server iniciado com sucesso!");
                Print($"📡 Porta: {PORT}");
                Print($"🔗 URL: http://localhost:{PORT}");
                Print($"🏥 Health Check: http://localhost:{PORT}/api/health");
            }
            catch (HttpListenerException ex)
            {
                Print($"❌ Erro ao iniciar servidor: {ex.Message}");
                Print($"💡 Dica: Execute o NinjaTrader como Administrador");
                Print($"💡 Ou libere a porta {PORT} com: netsh http add urlacl url=http://+:{PORT}/ user=Everyone");
            }
            catch (Exception ex)
            {
                Print($"❌ Erro inesperado: {ex.Message}");
            }
        }

        private async void HandleRequests()
        {
            while (isRunning && listener != null && listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    // Processar em background para não bloquear
                    Task.Run(() => ProcessRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Listener foi parado
                    break;
                }
                catch (Exception ex)
                {
                    Print($"⚠️ Erro ao receber request: {ex.Message}");
                }
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                // CORS Headers - Permitir acesso do dashboard React
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
                response.ContentType = "application/json; charset=utf-8";

                // Handle OPTIONS (preflight)
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                string responseString = "";
                string path = request.Url.AbsolutePath.ToLower();
                string method = request.HttpMethod;

                Print($"📥 {method} {path}");

                // Roteamento
                if (path == "/api/health" && method == "GET")
                {
                    responseString = HandleHealth();
                }
                else if (path == "/api/accounts" && method == "GET")
                {
                    responseString = HandleAccounts();
                }
                else if (path == "/api/positions" && method == "GET")
                {
                    responseString = HandlePositions();
                }
                else if (path == "/api/orders" && method == "GET")
                {
                    responseString = HandleGetOrders();
                }
                else if (path == "/api/orders" && method == "POST")
                {
                    responseString = await HandleCreateOrder(request);
                }
                else if (path == "/api/marketdata" && method == "GET")
                {
                    responseString = HandleMarketData(request);
                }
                else
                {
                    response.StatusCode = 404;
                    responseString = JsonConvert.SerializeObject(new
                    {
                        error = "Endpoint não encontrado",
                        path = path,
                        availableEndpoints = new[]
                        {
                            "GET /api/health",
                            "GET /api/accounts",
                            "GET /api/positions",
                            "GET /api/orders",
                            "POST /api/orders",
                            "GET /api/marketdata?symbol=ES"
                        }
                    });
                }

                if (response.StatusCode == 0)
                    response.StatusCode = 200;

                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Print($"❌ Erro ao processar request: {ex.Message}");
                try
                {
                    response.StatusCode = 500;
                    var errorJson = JsonConvert.SerializeObject(new { error = ex.Message, stack = ex.StackTrace });
                    byte[] buffer = Encoding.UTF8.GetBytes(errorJson);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                catch { }
            }
            finally
            {
                try
                {
                    response.Close();
                }
                catch { }
            }
        }

        private string HandleHealth()
        {
            return JsonConvert.SerializeObject(new
            {
                status = "healthy",
                service = "NinjaTrader 8",
                version = "1.0.0",
                timestamp = DateTime.UtcNow,
                isConnected = true,
                accountCount = Account.All.Count
            });
        }

        private string HandleAccounts()
        {
            var accounts = new List<object>();

            lock (Account.All)
            {
                foreach (Account account in Account.All)
                {
                    accounts.Add(new
                    {
                        name = account.Name,
                        type = account.Connection?.ToString() ?? "Unknown",
                        connection = account.Connection?.ToString() ?? "Unknown",
                        isConnected = true,
                        cashValue = Math.Round(account.Get(AccountItem.CashValue, Currency.UsDollar), 2),
                        realizedPnL = Math.Round(account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar), 2),
                        unrealizedPnL = Math.Round(account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar), 2),
                        buyingPower = Math.Round(account.Get(AccountItem.BuyingPower, Currency.UsDollar), 2)
                    });
                }
            }

            return JsonConvert.SerializeObject(new { accounts, count = accounts.Count });
        }

        private string HandlePositions()
        {
            var positions = new List<object>();

            lock (Account.All)
            {
                foreach (Account account in Account.All)
                {
                    foreach (Position position in account.Positions)
                    {
                        if (position.MarketPosition != MarketPosition.Flat)
                        {
                            positions.Add(new
                            {
                                account = account.Name,
                                symbol = position.Instrument.FullName,
                                marketPosition = position.MarketPosition.ToString(),
                                quantity = position.Quantity,
                                averagePrice = Math.Round(position.AveragePrice, 2),
                                currentPrice = Math.Round(position.Instrument.MarketData.Last.Price, 2),
                                unrealizedPnL = Math.Round(position.GetUnrealizedProfitLoss(PerformanceUnit.Currency), 2)
                            });
                        }
                    }
                }
            }

            return JsonConvert.SerializeObject(new { positions, count = positions.Count });
        }

        private string HandleGetOrders()
        {
            var orders = new List<object>();

            lock (Account.All)
            {
                foreach (Account account in Account.All)
                {
                    foreach (Order order in account.Orders)
                    {
                        orders.Add(new
                        {
                            orderId = order.OrderId,
                            account = account.Name,
                            instrument = order.Instrument.FullName,
                            orderAction = order.OrderAction.ToString(),
                            orderType = order.OrderType.ToString(),
                            quantity = order.Quantity,
                            limitPrice = order.LimitPrice,
                            stopPrice = order.StopPrice,
                            orderState = order.OrderState.ToString(),
                            filled = order.Filled,
                            averageFillPrice = order.AverageFillPrice,
                            time = order.Time
                        });
                    }
                }
            }

            return JsonConvert.SerializeObject(new { orders, count = orders.Count });
        }

        private async Task<string> HandleCreateOrder(HttpListenerRequest request)
        {
            try
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string body = await reader.ReadToEndAsync();
                    var orderRequest = JsonConvert.DeserializeObject<OrderRequestDto>(body);

                    // Validação básica
                    if (string.IsNullOrEmpty(orderRequest.Symbol) || orderRequest.Quantity <= 0)
                    {
                        return JsonConvert.SerializeObject(new
                        {
                            success = false,
                            error = "Symbol e Quantity são obrigatórios"
                        });
                    }

                    // Buscar conta
                    Account account = null;
                    lock (Account.All)
                    {
                        foreach (Account acc in Account.All)
                        {
                            if (string.IsNullOrEmpty(orderRequest.Account) || acc.Name == orderRequest.Account)
                            {
                                account = acc;
                                break;
                            }
                        }
                    }

                    if (account == null)
                    {
                        return JsonConvert.SerializeObject(new
                        {
                            success = false,
                            error = "Conta não encontrada"
                        });
                    }

                    // Buscar instrumento
                    Instrument instrument = Instrument.GetInstrument(orderRequest.Symbol);
                    if (instrument == null)
                    {
                        return JsonConvert.SerializeObject(new
                        {
                            success = false,
                            error = $"Instrumento '{orderRequest.Symbol}' não encontrado"
                        });
                    }

                    // Criar ordem
                    OrderAction action = orderRequest.Action.ToUpper() == "BUY" ? OrderAction.Buy : OrderAction.Sell;
                    Order order = null;

                    if (orderRequest.OrderType.ToUpper() == "MARKET")
                    {
                        order = account.CreateOrder(instrument, action, OrderType.Market, TimeInForce.Day, 
                            orderRequest.Quantity, 0, 0, "", "NTBot", null);
                    }
                    else if (orderRequest.OrderType.ToUpper() == "LIMIT")
                    {
                        order = account.CreateOrder(instrument, action, OrderType.Limit, TimeInForce.Day,
                            orderRequest.Quantity, orderRequest.LimitPrice, 0, "", "NTBot", null);
                    }
                    else if (orderRequest.OrderType.ToUpper() == "STOP")
                    {
                        order = account.CreateOrder(instrument, action, OrderType.StopMarket, TimeInForce.Day,
                            orderRequest.Quantity, 0, orderRequest.StopPrice, "", "NTBot", null);
                    }

                    if (order != null)
                    {
                        account.Submit(new[] { order });

                        return JsonConvert.SerializeObject(new
                        {
                            success = true,
                            orderId = order.OrderId,
                            message = "Ordem criada com sucesso"
                        });
                    }

                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Falha ao criar ordem"
                    });
                }
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        private string HandleMarketData(HttpListenerRequest request)
        {
            string symbol = request.QueryString["symbol"];

            if (string.IsNullOrEmpty(symbol))
            {
                return JsonConvert.SerializeObject(new
                {
                    error = "Parâmetro 'symbol' é obrigatório. Ex: /api/marketdata?symbol=ES"
                });
            }

            Instrument instrument = Instrument.GetInstrument(symbol);

            if (instrument == null)
            {
                return JsonConvert.SerializeObject(new
                {
                    error = $"Instrumento '{symbol}' não encontrado"
                });
            }

            return JsonConvert.SerializeObject(new
            {
                symbol = instrument.FullName,
                lastPrice = instrument.MarketData.Last?.Price ?? 0,
                bid = instrument.MarketData.Bid?.Price ?? 0,
                ask = instrument.MarketData.Ask?.Price ?? 0,
                volume = instrument.MarketData.Last?.Volume ?? 0,
                time = instrument.MarketData.Last?.Time ?? DateTime.MinValue
            });
        }

        private void StopServer()
        {
            try
            {
                isRunning = false;

                if (listener != null && listener.IsListening)
                {
                    listener.Stop();
                    listener.Close();
                    listener = null;
                    Print("🛑 NTBot HTTP Server parado");
                }
            }
            catch (Exception ex)
            {
                Print($"⚠️ Erro ao parar servidor: {ex.Message}");
            }
        }
    }

    // DTO para receber ordens via POST
    public class OrderRequestDto
    {
        public string Account { get; set; }
        public string Symbol { get; set; }
        public string Action { get; set; } // "BUY" ou "SELL"
        public string OrderType { get; set; } // "MARKET", "LIMIT", "STOP"
        public int Quantity { get; set; }
        public double LimitPrice { get; set; }
        public double StopPrice { get; set; }
    }
}
