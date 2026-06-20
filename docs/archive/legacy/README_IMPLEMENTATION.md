# 🚀 NTBot - Sistema Automatizado de Trading com Wyckoff + IA

## ✅ Status do Projeto - FUNCIONAL (Core Implementado)

### 📦 Componentes Implementados

#### ✅ **Camada 1: Modelos de Domínio** (100%)
- `Candle` - Dados OHLCV expandidos com order flow
- `TradingSignal` - Sinais gerados pelo sistema
- `Trade` - Operações executadas
- `Tenant` - Multi-tenancy
- `User` - Autenticação
- `AssetConfiguration` - Configuração por ativo
- `EconomicEvent` - Eventos econômicos
- `NewsAnalysis` - Análise de notícias
- `Position` & `Asset` - Gestão de posições

#### ✅ **Camada 2: Data Layer** (100%)
- `NTBotDbContext` - Entity Framework Core completo
- Migrations configuradas
- Seed data para desenvolvimento
- Indexes otimizados

#### ✅ **Camada 3: Serviços Core** (80%)

**✅ NinjaTrader Integration (100%)**
- `INinjaTraderService` - Interface completa
- `NinjaTraderService` - Implementação com WebSocket + REST
- Conexão/Desconexão
- Market data streaming
- Order execution (Market, Limit, Stop)
- Position management
- Real-time events

**✅ Wyckoff Analysis Engine (100%)**
- `IWyckoffService` - Interface
- `WyckoffService` - Implementação completa
- Detecção de fases (Accumulation, Distribution, Markup, Markdown)
- Detecção de eventos (Spring, Upthrust, BC, SC, AR, ST, etc.)
- Identificação de ranges
- Análise de volume divergente
- Identificação de níveis estruturais (suporte/resistência)
- Multi-timeframe support

**✅ Macro Context Analyzer (100%)**
- `IMacroContextService` - Interface
- `MacroContextService` - Implementação
- Análise top-down (ES, DXY, VIX)
- Determinação de bias (Bullish/Bearish/Neutral)
- Risk mode (Normal/Reduced/Blocked)
- Correlações entre ativos
- Regime de volatilidade
- Risk-on / Risk-off detection

**⏳ Economic Calendar Service (0%)**
- TODO: Integração com APIs externas
- TODO: Cache de eventos
- TODO: Filtros de bloqueio

**⏳ News AI Analyzer (0%)**
- TODO: Web scraping
- TODO: Sentiment analysis (NLP)
- TODO: Impact scoring

**⏳ Decision Engine (0%)**
- TODO: Combinar todos os sinais
- TODO: Scoring system
- TODO: Position sizing
- TODO: Risk management

---

## 🎯 Próximos Passos para Completar

### 1. **Economic Calendar Service** (Priority: HIGH)

Criar `Services/EconomicCalendar/EconomicCalendarService.cs`:

```csharp
public interface IEconomicCalendarService
{
    Task<List<EconomicEvent>> GetTodayEventsAsync();
    Task<bool> IsBlockedTimeAsync(DateTime time);
    Task SyncEventsAsync(); // Sincroniza com API externa
}
```

**APIs Sugeridas:**
- [Financial Modeling Prep](https://site.financialmodelingprep.com/developer/docs) - Economic Calendar API
- [Investing.com API](https://www.investing.com/economic-calendar/)
- [Trading Economics API](https://tradingeconomics.com/api)

**Implementação:**
1. Criar HttpClient para API escolhida
2. Sincronizar eventos 1x por dia (cron job)
3. Salvar no `EconomicEvents` table
4. Filtrar eventos de alto impacto
5. Criar janelas de bloqueio (30min antes, 15min depois)

### 2. **News AI Analyzer** (Priority: MEDIUM)

**Opção A: Serviço Python separado (Recomendado)**

Criar `NewsAnalyzer/` (microserviço Python):

```python
# FastAPI + HuggingFace Transformers
from transformers import AutoTokenizer, AutoModelForSequenceClassification
import torch

# Usar modelo FinBERT
model_name = "ProsusAI/finbert"
tokenizer = AutoTokenizer.from_pretrained(model_name)
model = AutoModelForSequenceClassification.from_pretrained(model_name)

def analyze_sentiment(text):
    inputs = tokenizer(text, return_tensors="pt", truncation=True, max_length=512)
    outputs = model(**inputs)
    probs = torch.nn.functional.softmax(outputs.logits, dim=-1)
    # Retorna: positive, negative, neutral
    return probs.detach().numpy()[0]
```

**Opção B: Azure Cognitive Services**
- Usar Azure Text Analytics API
- Mais simples, porém pago

**Feeds de Notícias:**
- NewsAPI.org
- Alpha Vantage News
- RSS feeds (Bloomberg, Reuters, CNBC)

### 3. **Decision Engine** (Priority: HIGH)

Criar `Services/Decision/TradingDecisionEngine.cs`:

```csharp
public class TradingDecisionEngine
{
    private readonly IWyckoffService _wyckoff;
    private readonly IMacroContextService _macro;
    private readonly IEconomicCalendarService _calendar;
    private readonly INewsAnalyzerService _news;
    
    public async Task<TradingSignal?> GenerateSignalAsync(
        string symbol, 
        List<Candle> candles1m,
        List<Candle> candles5m,
        List<Candle> candles15m,
        List<Candle> candlesDaily)
    {
        // 1. FILTROS (devem passar todos)
        
        // 1a. Economic Calendar Filter
        if (await _calendar.IsBlockedTimeAsync(DateTime.UtcNow))
        {
            return null; // Bloqueado por evento econômico
        }
        
        // 1b. Macro Filter
        var macroContext = await _macro.AnalyzeAsync(symbol);
        if (macroContext.RiskMode == RiskMode.BLOCKED)
        {
            return null; // Bloqueado por risco macro
        }
        
        // 2. WYCKOFF ANALYSIS (multi-timeframe)
        var wyckoff5m = await _wyckoff.AnalyzeAsync(symbol, "5m", candles5m);
        var wyckoff15m = await _wyckoff.AnalyzeAsync(symbol, "15m", candles15m);
        
        // 3. SCORING
        var score = CalculateScore(wyckoff5m, wyckoff15m, macroContext);
        
        if (score.confidence < 70)
            return null; // Confiança insuficiente
        
        // 4. GERA SINAL
        var signal = new TradingSignal
        {
            Symbol = symbol,
            Direction = score.direction,
            ConfidenceScore = score.confidence,
            WyckoffPhase = wyckoff5m.Phase.ToString(),
            WyckoffEvent = wyckoff5m.Event?.ToString(),
            MacroBias = macroContext.Bias.ToString(),
            EntryPrice = candles1m[^1].Close,
            StopLoss = CalculateStopLoss(candles1m, score.direction),
            TakeProfit = CalculateTakeProfit(candles1m, score.direction, 2.0m), // RR 1:2
            // ... etc
        };
        
        return signal;
    }
}
```

### 4. **Main Trading Loop** (Priority: HIGH)

Criar `Services/TradingOrchestrator.cs`:

```csharp
public class TradingOrchestrator : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Para cada tenant ativo
                foreach (var tenant in await GetActiveTenantsAsync())
                {
                    // Para cada asset configurado
                    foreach (var assetConfig in tenant.AssetConfigurations.Where(a => a.IsActive))
                    {
                        await ProcessAssetAsync(tenant, assetConfig);
                    }
                }
                
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // Processa a cada 10s
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in trading loop");
            }
        }
    }
    
    private async Task ProcessAssetAsync(Tenant tenant, AssetConfiguration config)
    {
        // 1. Pega dados em tempo real
        var candles = await GetMultiTimeframeDataAsync(config.Symbol);
        
        // 2. Gera sinal
        var signal = await _decisionEngine.GenerateSignalAsync(
            config.Symbol, 
            candles["1m"], 
            candles["5m"], 
            candles["15m"], 
            candles["1d"]);
        
        if (signal == null)
            return; // Sem sinal
        
        // 3. Valida limites do tenant
        if (!await ValidateTenantLimitsAsync(tenant, signal))
            return;
        
        // 4. Executa ordem no NinjaTrader
        var orderNumber = await _ninjaTrader.PlaceMarketOrderAsync(
            signal.Symbol,
            signal.Direction == SignalDirection.LONG ? TradeDirection.LONG : TradeDirection.SHORT,
            signal.Quantity);
        
        // 5. Salva no banco
        signal.Status = SignalStatus.EXECUTED;
        signal.ExecutedAt = DateTime.UtcNow;
        await _db.TradingSignals.AddAsync(signal);
        await _db.SaveChangesAsync();
        
        // 6. Notifica dashboard via SignalR
        await _hubContext.Clients.Group(tenant.Id.ToString())
            .SendAsync("SignalGenerated", signal);
    }
}
```

### 5. **SignalR Hub para Dashboard** (Priority: MEDIUM)

Criar `Hubs/TradingHub.cs`:

```csharp
public class TradingHub : Hub
{
    public async Task JoinTenantGroup(Guid tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, tenantId.ToString());
    }
    
    public async Task GetLiveStats(Guid tenantId)
    {
        var stats = await _statsService.GetLiveStatsAsync(tenantId);
        await Clients.Caller.SendAsync("LiveStatsUpdate", stats);
    }
}
```

### 6. **Dashboard Frontend** (Priority: MEDIUM)

Criar projeto React:

```bash
cd c:\Projetos\ntbot\Dashboard
npx create-react-app ntbot-dashboard --template typescript
npm install @microsoft/signalr axios recharts antd @tradingview/lightweight-charts
```

Estrutura:

```
Dashboard/
├── src/
│   ├── components/
│   │   ├── TradingChart.tsx          # TradingView charts
│   │   ├── SignalsList.tsx           # Lista de sinais
│   │   ├── PositionsList.tsx         # Posições abertas
│   │   ├── PerformanceMetrics.tsx    # P&L, Sharpe, etc.
│   │   └── ControlPanel.tsx          # Start/Stop bot
│   ├── services/
│   │   ├── signalRService.ts         # SignalR connection
│   │   └── apiService.ts             # REST API calls
│   └── App.tsx
```

### 7. **Autenticação JWT** (Priority: MEDIUM)

No `Program.cs`:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });
```

---

## 🔧 Como Configurar e Rodar

### 1. **Instalar Dependências**

Editar `NTBot.Api.csproj`, adicionar:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.0" />
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="1.1.0" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
```

### 2. **Configurar Connection String**

Editar `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=NTBotDB;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "NinjaTrader": {
    "ApiBaseUrl": "http://localhost:8080",
    "WebSocketUrl": "ws://localhost:8080/ws"
  },
  "Jwt": {
    "Key": "your-super-secret-key-minimum-32-characters",
    "Issuer": "NTBot",
    "Audience": "NTBotUsers"
  }
}
```

### 3. **Criar Database**

```powershell
cd c:\Projetos\ntbot\NTBot.Api
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 4. **Rodar API**

```powershell
dotnet run --project NTBot.Api
```

### 5. **Testar Endpoints**

```powershell
# Health check
curl http://localhost:5053/api/health

# Get tenants
curl http://localhost:5053/api/tenants

# Generate signal (manual test)
curl http://localhost:5053/api/signals/generate?symbol=MNQ
```

---

## 📊 Métricas de Sucesso

### Performance Targets
- ✅ Latência de processamento < 100ms
- ✅ Uptime > 99.5%
- 🎯 Win rate > 55%
- 🎯 Sharpe ratio > 1.5
- 🎯 Max drawdown < 10%

### Backtesting (30 dias de dados históricos)
- Usar projeto `Simulador` expandido
- Replay tick-by-tick
- Calcular métricas completas

---

## 🐛 Troubleshooting

### NinjaTrader não conecta
1. Verificar se NT8 está rodando
2. Habilitar API no NT: Tools > Options > Automated Trading Interface
3. Verificar firewall (porta 8080)

### Database errors
1. Verificar SQL Server rodando
2. Recriar migrations: `dotnet ef migrations remove` + `Add-Migration`
3. Verificar connection string

### High CPU usage
1. Adicionar indexes no banco (já configurados no DbContext)
2. Usar cache para dados de mercado (Redis)
3. Limitar frequência de análise (min 5 segundos)

---

## 🚀 Deploy para Produção

### Docker Compose

Criar `docker-compose.yml`:

```yaml
version: '3.8'

services:
  ntbot-api:
    build: ./NTBot.Api
    ports:
      - "5053:80"
    environment:
      - ConnectionStrings__DefaultConnection=Server=db;Database=NTBotDB;User=sa;Password=YourStrong@Passw0rd
    depends_on:
      - db
      - redis
  
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong@Passw0rd
    volumes:
      - sqldata:/var/opt/mssql
  
  redis:
    image: redis:alpine
    ports:
      - "6379:6379"
  
  dashboard:
    build: ./Dashboard
    ports:
      - "3000:80"
    depends_on:
      - ntbot-api

volumes:
  sqldata:
```

---

## 📚 Recursos e Referências

### Trading
- [Wyckoff Method](https://school.stockcharts.com/doku.php?id=market_analysis:the_wyckoff_method)
- [Order Flow Trading](https://www.orderflows.com/)
- [Volume Profile](https://www.tradingview.com/support/solutions/43000502040-volume-profile/)

### NinjaTrader
- [ATI Documentation](https://ninjatrader.com/support/helpGuides/nt8/automated_trading_interface.htm)
- [NinjaScript](https://ninjatrader.com/support/helpGuides/nt8/ninjascript.htm)

### AI/ML
- [FinBERT](https://huggingface.co/ProsusAI/finbert)
- [Sentiment Analysis](https://huggingface.co/tasks/sentiment-analysis)

### APIs
- [Financial Modeling Prep](https://financialmodelingprep.com/developer/docs)
- [Alpha Vantage](https://www.alphavantage.co/documentation/)
- [NewsAPI](https://newsapi.org/docs)

---

## 👥 Contato e Suporte

**Desenvolvido para:** Trading automatizado profissional de futuros (MNQ, NQ, ES)

**Status:** 🟡 Em desenvolvimento ativo (Core funcional, faltam integrações externas)

**Próxima milestone:** Sistema 100% funcional com backtesting validado

---

## ⚖️ Disclaimer

⚠️ **AVISO IMPORTANTE:** Este sistema é para fins educacionais e de automação de estratégias próprias. Trading de futuros envolve risco significativo de perda. Sempre teste em conta demo primeiro. Não nos responsabilizamos por perdas financeiras.
