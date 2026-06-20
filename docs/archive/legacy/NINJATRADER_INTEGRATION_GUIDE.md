# Guia Completo: Integração NinjaTrader 8 com NTBot

## 📋 Visão Geral

O NinjaTrader 8 oferece duas formas principais de integração externa:

1. **ATI (Automated Trading Interface)** - Interface nativa para comunicação externa
2. **NinjaScript** - Scripts personalizados que podem expor funcionalidades

## 🎯 Método 1: ATI (Automated Trading Interface) - RECOMENDADO

### Passo 1: Habilitar ATI no NinjaTrader

#### 1.1 Abrir Configurações

```
NinjaTrader 8 Control Center
  ↓
Tools (Menu Superior)
  ↓
Options...
  ↓
Aba: "Automated Trading Interface"
```

#### 1.2 Configurar ATI

Marque as seguintes opções:

```
☑️ Enable outbound connections
☑️ Enable inbound connections
Port: 36973 (porta padrão ATI)
☑️ Allow connections from localhost only (recomendado para desenvolvimento)
```

**⚠️ Importante:** Se a aba "Automated Trading Interface" não existir, você precisa instalar o ATI separadamente.

### Passo 2: Instalar ATI (se não estiver instalado)

#### 2.1 Download

1. Acesse: https://ninjatrader.com/
2. Faça login na sua conta
3. Vá em: **Support** → **Download**
4. Procure por: **"Automated Trading Interface (ATI)"**
5. Baixe e instale

#### 2.2 Instalação

1. Execute o instalador `ATI_Setup.exe`
2. Siga o assistente de instalação
3. Reinicie o NinjaTrader após instalar

### Passo 3: Configurar Firewall

Liberar porta ATI no Windows Firewall:

```powershell
# Abrir PowerShell como Administrador

# Regra para porta ATI
New-NetFirewallRule -DisplayName "NinjaTrader ATI" `
  -Direction Inbound `
  -LocalPort 36973 `
  -Protocol TCP `
  -Action Allow

# Regra para aplicação NinjaTrader
New-NetFirewallRule -DisplayName "NinjaTrader Application" `
  -Direction Inbound `
  -Program "C:\Program Files\NinjaTrader 8\bin\NinjaTrader.exe" `
  -Action Allow
```

### Passo 4: Testar Conexão ATI

Criar script de teste:

```csharp
// Salvar como: TestATI.cs no projeto NTBot.Api

using System;
using System.Net.Sockets;
using System.Text;

public class ATITester
{
    public static void TestConnection()
    {
        try
        {
            using var client = new TcpClient("127.0.0.1", 36973);
            using var stream = client.GetStream();
            
            // Comando ATI para obter versão
            string command = "VERSION\r\n";
            byte[] data = Encoding.ASCII.GetBytes(command);
            
            stream.Write(data, 0, data.Length);
            Console.WriteLine($"Enviado: {command}");
            
            // Ler resposta
            byte[] buffer = new byte[1024];
            int bytes = stream.Read(buffer, 0, buffer.Length);
            string response = Encoding.ASCII.GetString(buffer, 0, bytes);
            
            Console.WriteLine($"Resposta: {response}");
            Console.WriteLine("✅ ATI funcionando!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro: {ex.Message}");
        }
    }
}
```

## 🎯 Método 2: NinjaScript com Comunicação HTTP

### Passo 1: Criar NinjaScript Addon

#### 1.1 Abrir Editor NinjaScript

```
NinjaTrader 8
  ↓
Tools
  ↓
Edit NinjaScript
  ↓
AddOn (no menu lateral)
  ↓
Clique com botão direito → New...
```

#### 1.2 Criar Addon HTTP Server

Nome: `NTBotHttpServer`

```csharp
#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.AddOns
{
    public class NTBotHttpServer : AddOnBase
    {
        private HttpListener listener;
        private const int PORT = 8080;
        
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "HTTP Server for NTBot Integration";
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
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{PORT}/");
                listener.Start();
                
                Task.Run(() => HandleRequests());
                
                Print($"✅ NTBot HTTP Server iniciado na porta {PORT}");
            }
            catch (Exception ex)
            {
                Print($"❌ Erro ao iniciar servidor: {ex.Message}");
            }
        }
        
        private async void HandleRequests()
        {
            while (listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    await ProcessRequest(context);
                }
                catch (Exception ex)
                {
                    Print($"Erro ao processar request: {ex.Message}");
                }
            }
        }
        
        private async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            
            // CORS Headers
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
            
            string responseString = "";
            
            try
            {
                // Roteamento
                string path = request.Url.AbsolutePath;
                
                switch (path)
                {
                    case "/api/health":
                        responseString = HandleHealth();
                        break;
                        
                    case "/api/accounts":
                        responseString = HandleAccounts();
                        break;
                        
                    case "/api/positions":
                        responseString = HandlePositions();
                        break;
                        
                    case "/api/orders":
                        if (request.HttpMethod == "GET")
                            responseString = HandleGetOrders();
                        else if (request.HttpMethod == "POST")
                            responseString = await HandleCreateOrder(request);
                        break;
                        
                    default:
                        response.StatusCode = 404;
                        responseString = "{\"error\":\"Endpoint não encontrado\"}";
                        break;
                }
                
                response.StatusCode = 200;
                response.ContentType = "application/json";
            }
            catch (Exception ex)
            {
                response.StatusCode = 500;
                responseString = $"{{\"error\":\"{ex.Message}\"}}";
            }
            
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }
        
        private string HandleHealth()
        {
            return "{\"status\":\"healthy\",\"service\":\"NinjaTrader\",\"version\":\"8.0\"}";
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
                        type = account.Connection.ToString(),
                        cashValue = account.Get(AccountItem.CashValue, Currency.UsDollar),
                        realizedPnL = account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar)
                    });
                }
            }
            
            return Newtonsoft.Json.JsonConvert.SerializeObject(accounts);
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
                                instrument = position.Instrument.FullName,
                                marketPosition = position.MarketPosition.ToString(),
                                quantity = position.Quantity,
                                averagePrice = position.AveragePrice,
                                unrealizedPnL = position.GetUnrealizedProfitLoss(PerformanceUnit.Currency)
                            });
                        }
                    }
                }
            }
            
            return Newtonsoft.Json.JsonConvert.SerializeObject(positions);
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
                            instrument = order.Instrument.FullName,
                            orderAction = order.OrderAction.ToString(),
                            orderType = order.OrderType.ToString(),
                            quantity = order.Quantity,
                            limitPrice = order.LimitPrice,
                            stopPrice = order.StopPrice,
                            orderState = order.OrderState.ToString()
                        });
                    }
                }
            }
            
            return Newtonsoft.Json.JsonConvert.SerializeObject(orders);
        }
        
        private async Task<string> HandleCreateOrder(HttpListenerRequest request)
        {
            using (var reader = new System.IO.StreamReader(request.InputStream))
            {
                string body = await reader.ReadToEndAsync();
                // Parse JSON e criar ordem
                // Implementação depende dos seus requisitos
                return "{\"success\":true,\"message\":\"Ordem criada\"}";
            }
        }
        
        private void StopServer()
        {
            if (listener != null && listener.IsListening)
            {
                listener.Stop();
                listener.Close();
                Print("🛑 NTBot HTTP Server parado");
            }
        }
    }
}
```

#### 1.3 Compilar NinjaScript

1. Salve o código
2. Pressione **F5** ou clique em **Compile**
3. Verifique se não há erros
4. O addon será compilado e ficará disponível

### Passo 2: Ativar o Addon

```
NinjaTrader Control Center
  ↓
Tools
  ↓
AddOns
  ↓
Procure "NTBot HTTP Server"
  ↓
Clique em "Enable"
```

### Passo 3: Verificar se Está Rodando

Teste no PowerShell:

```powershell
Invoke-WebRequest -Uri "http://localhost:8080/api/health" -Method GET
```

Resposta esperada:
```json
{
  "status": "healthy",
  "service": "NinjaTrader",
  "version": "8.0"
}
```

## 🎯 Método 3: Usar Biblioteca de Terceiros

### Opção A: NinjaTrader REST API (Third-Party)

Alguns desenvolvedores criaram APIs REST para NT8:

1. **NT8API** - GitHub
2. **NinjaTrader HTTP Bridge**
3. **TradeSharp NT Connector**

Busque no Google: "NinjaTrader 8 REST API GitHub"

### Opção B: Interactive Brokers TWS API

Se você usa IB como broker através do NT:

```csharp
// Conectar direto na TWS API
// Mais estável e documentado
```

## 📊 Estrutura de Comunicação Completa

### Diagrama de Arquitetura

```
┌─────────────────────────────────────────────┐
│           NinjaTrader 8                     │
│  ┌─────────────────────────────────────┐   │
│  │   NTBotHttpServer Addon             │   │
│  │   (HTTP Server na porta 8080)       │   │
│  └─────────────────┬───────────────────┘   │
│                    │ HTTP/REST              │
└────────────────────┼───────────────────────┘
                     │
                     ↓
┌────────────────────┼───────────────────────┐
│                    │   NTBot.Api           │
│  ┌─────────────────▼───────────────────┐   │
│  │  NinjaTraderService                 │   │
│  │  - GetPositions()                   │   │
│  │  - CreateOrder()                    │   │
│  │  - GetMarketData()                  │   │
│  └─────────────────┬───────────────────┘   │
│                    │                        │
│  ┌─────────────────▼───────────────────┐   │
│  │  Controllers (REST Endpoints)       │   │
│  └─────────────────┬───────────────────┘   │
└────────────────────┼───────────────────────┘
                     │
                     ↓
┌────────────────────┼───────────────────────┐
│                    │   Dashboard React     │
│                  WebSocket/HTTP             │
└─────────────────────────────────────────────┘
```

## 🔧 Implementação no NTBot.Api

Atualizar o `NinjaTraderService.cs`:

```csharp
using System.Net.Http;
using System.Text.Json;

public class NinjaTraderService : INinjaTraderService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    
    public NinjaTraderService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClient = httpClientFactory.CreateClient("NinjaTrader");
        _baseUrl = config["NinjaTrader:ApiBaseUrl"] ?? "http://localhost:8080";
    }
    
    public async Task<List<Position>> GetPositionsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/positions");
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var positions = JsonSerializer.Deserialize<List<Position>>(json);
            
            return positions ?? new List<Position>();
        }
        catch (HttpRequestException ex)
        {
            // NinjaTrader não está rodando ou addon não está ativo
            throw new Exception("Não foi possível conectar ao NinjaTrader", ex);
        }
    }
    
    public async Task<string> CreateOrderAsync(OrderRequest order)
    {
        var json = JsonSerializer.Serialize(order);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/orders", content);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadAsStringAsync();
        return result;
    }
}
```

## ✅ Checklist de Configuração

### NinjaTrader
- [ ] NinjaTrader 8 instalado e funcionando
- [ ] ATI instalado (se usando Método 1)
- [ ] NTBotHttpServer addon compilado (se usando Método 2)
- [ ] Addon ativado em Tools → AddOns
- [ ] Firewall configurado
- [ ] Conta demo ou live conectada
- [ ] Símbolos adicionados (ES, NQ, etc)

### NTBot.Api
- [ ] `appsettings.json` configurado com URL correta
- [ ] `NinjaTraderService` implementado
- [ ] HttpClient configurado
- [ ] Endpoints testados

### Teste Final
- [ ] `/api/health` responde
- [ ] `/api/accounts` retorna contas
- [ ] `/api/positions` retorna posições
- [ ] Dashboard conecta com sucesso

## 🚨 Troubleshooting Comum

### Erro: "Connection refused"
**Solução:** 
- Verificar se NinjaTrader está rodando
- Verificar se addon está ativo
- Verificar porta correta

### Erro: "Method not found"
**Solução:**
- Recompilar NinjaScript
- Reiniciar NinjaTrader

### Erro: "Access denied"
**Solução:**
- Configurar firewall
- Executar NT como administrador

## 📚 Recursos Adicionais

- [NinjaTrader Development](https://ninjatrader.com/support/helpGuides/nt8/NT HelpGuide English.html)
- [NinjaScript Programming Guide](https://ninjatrader.com/support/helpGuides/nt8/ninjascript_overview.htm)
- [ATI Documentation](https://ninjatrader.com/support/helpGuides/nt8/automated_trading_interface.htm)

## 🎓 Próximos Passos

1. Escolher método de integração (recomendo Método 2 - NinjaScript HTTP)
2. Implementar o addon no NinjaTrader
3. Testar conexão básica
4. Implementar endpoints necessários
5. Integrar com NTBot.Api
6. Testar com dashboard React
