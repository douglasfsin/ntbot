# Setup e Instalação - Estratégia Quantitativa

## 📦 Dependências Backend (.NET)

O projeto já possui as dependências necessárias do .NET. Não são necessários pacotes adicionais.

---

## ⚙️ Configuração do Backend

### 1. Registrar Serviços no Program.cs

Adicione as seguintes linhas no arquivo `NTBot.Api/Program.cs`:

```csharp
using NTBot.Api.Services.Correlation;
using NTBot.Api.Services.GammaExposure;
using NTBot.Api.Services.Wyckoff;
using NTBot.Api.Strategies;

// ... (código existente)

// Registrar serviços da estratégia quantitativa
builder.Services.AddScoped<IGlobalCorrelationService, GlobalCorrelationService>();
builder.Services.AddScoped<IGammaExposureService, GammaExposureService>();
builder.Services.AddScoped<IWyckoffService, WyckoffService>(); // Se ainda não registrado
builder.Services.AddScoped<QuantStrategy>();

// HttpClient para APIs externas (correlação global)
builder.Services.AddHttpClient<GlobalCorrelationService>();

// ... (resto do código)
```

### 2. Compilar Backend

```powershell
cd NTBot.Api
dotnet build
dotnet run
```

Backend estará rodando em: `http://localhost:5000`

---

## 🎨 Configuração do Frontend

### 1. Instalar Dependências (se necessário)

```powershell
cd ntbot-dashboard
npm install
```

### 2. Configurar Variáveis de Ambiente

Criar arquivo `.env` em `ntbot-dashboard/`:

```env
VITE_API_URL=http://localhost:5000/api
```

### 3. Executar Frontend

```powershell
npm run dev
```

Frontend estará rodando em: `http://localhost:5173`

---

## 🧪 Testando a Implementação

### 1. Teste via Browser

Acesse: `http://localhost:5173/quant`

Você verá:
- Cards de overview (Preço, Bias Global, GEX, Wyckoff)
- Gráficos de correlação e GEX
- Card de sinal (se houver)

### 2. Teste via API

**Obter Dashboard:**
```bash
curl http://localhost:5000/api/quantstrategy/dashboard?symbol=WINFUT&leaderSymbol=NQ
```

**Gerar Sinal:**
```bash
curl -X POST http://localhost:5000/api/quantstrategy/analyze \
  -H "Content-Type: application/json" \
  -d '{"symbol":"WINFUT","leaderSymbol":"NQ"}'
```

**Obter Correlação:**
```bash
curl http://localhost:5000/api/quantstrategy/correlation?leaderSymbol=NQ&followerSymbol=WINFUT&lookback=50
```

**Obter GEX:**
```bash
curl http://localhost:5000/api/quantstrategy/gex?symbol=WINFUT
```

---

## 🔗 Integrando Dados Reais

### Opção 1: NinjaTrader (Recomendado)

Se você já usa NinjaTrader, pode integrar diretamente:

```csharp
// Em GlobalCorrelationService.cs
public async Task<List<Candle>> GetLeaderDataAsync(string symbol, int periods)
{
    // Usar o serviço existente NinjaTraderService
    var ninjaService = /* injetar INinjaTraderService */;
    return await ninjaService.GetHistoricalDataAsync(symbol, periods);
}
```

### Opção 2: Yahoo Finance (Gratuito)

Instalar pacote:
```powershell
dotnet add package YahooFinanceApi
```

```csharp
using YahooFinanceApi;

public async Task<List<Candle>> GetLeaderDataAsync(string symbol, int periods)
{
    var securities = await Yahoo.Symbols(symbol).Fields(Field.Close, Field.High, Field.Low, Field.Open, Field.Volume)
        .QueryAsync();
    
    var candles = securities[symbol].Select(item => new Candle
    {
        Symbol = symbol,
        Timestamp = item.DateTime,
        Open = (decimal)item.Open,
        High = (decimal)item.High,
        Low = (decimal)item.Low,
        Close = (decimal)item.Close,
        Volume = (long)item.Volume
    }).ToList();
    
    return candles;
}
```

### Opção 3: Alpha Vantage

Obter API key gratuita em: https://www.alphavantage.co/

```csharp
public async Task<List<Candle>> GetLeaderDataAsync(string symbol, int periods)
{
    var apiKey = "YOUR_API_KEY";
    var url = $"https://www.alphavantage.co/query?function=TIME_SERIES_INTRADAY&symbol={symbol}&interval=5min&apikey={apiKey}";
    
    var response = await _httpClient.GetStringAsync(url);
    // Parse JSON e converter para List<Candle>
    return candles;
}
```

### Opção 4: Interactive Brokers

Instalar pacote:
```powershell
dotnet add package IBApi
```

Configurar conexão TWS/Gateway.

---

## 📊 Integrando Dados de Opções (GEX)

### Para Opções Brasileiras (B3):

**Opção 1: B3 OpLab**
- Acesso via API (requer credenciais)
- Dados de opções sobre WIN e WDO

**Opção 2: Web Scraping (não recomendado para produção)**
- Infomoney
- B3 Market Data

### Para Opções Americanas:

**CBOE DataShop:**
```csharp
public async Task<List<OptionData>> GetOptionsDataAsync(string symbol)
{
    var apiKey = "YOUR_CBOE_API_KEY";
    // Requisitar dados de opções do CBOE
    // Parsear e retornar como List<OptionData>
}
```

**Interactive Brokers:**
```csharp
// Usar IBApi para obter chain de opções
var contract = new Contract
{
    Symbol = symbol,
    SecType = "OPT",
    Exchange = "SMART"
};

// Solicitar dados de opções via TWS
```

---

## 🗄️ Persistência de Dados (Opcional)

Para salvar sinais no banco de dados:

### 1. Adicionar DbSet no `NTBotDbContext.cs`:

```csharp
public DbSet<QuantSignal> QuantSignals { get; set; }
public DbSet<CorrelationData> CorrelationData { get; set; }
public DbSet<GammaExposureData> GammaExposureData { get; set; }
public DbSet<OptionData> OptionsData { get; set; }
```

### 2. Criar Migration:

```powershell
cd NTBot.Api
dotnet ef migrations add AddQuantStrategyTables
dotnet ef database update
```

### 3. Implementar Repositório:

```csharp
public interface IQuantSignalRepository
{
    Task<QuantSignal> SaveSignalAsync(QuantSignal signal);
    Task<List<QuantSignal>> GetSignalHistoryAsync(string? symbol, int limit);
}
```

---

## 🔔 Notificações (Opcional)

### Email:

```csharp
// Adicionar ao Program.cs
builder.Services.AddTransient<IEmailService, EmailService>();

// No QuantStrategy.cs, após gerar sinal:
await _emailService.SendSignalNotificationAsync(signal);
```

### Telegram:

```powershell
dotnet add package Telegram.Bot
```

```csharp
public async Task SendTelegramSignalAsync(QuantSignal signal)
{
    var bot = new TelegramBotClient("YOUR_BOT_TOKEN");
    var message = $"🚨 Novo Sinal: {signal.Direction} {signal.Symbol} @ {signal.EntryPrice}";
    await bot.SendTextMessageAsync(chatId, message);
}
```

---

## 📈 Backtesting (Próxima Implementação)

Framework para testar a estratégia com dados históricos:

```csharp
public class BacktestEngine
{
    public async Task<BacktestResult> RunBacktestAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        QuantStrategy strategy)
    {
        // 1. Obter dados históricos
        // 2. Iterar período por período
        // 3. Gerar sinais
        // 4. Simular execuções
        // 5. Calcular métricas (win rate, profit factor, sharpe, etc.)
    }
}
```

---

## 🐛 Troubleshooting

### Backend não inicia:
```powershell
# Verificar logs
cat NTBot.Api/logs/ntbot-*.txt

# Verificar porta
netstat -ano | findstr :5000
```

### Frontend não conecta ao backend:
1. Verificar CORS no Program.cs
2. Verificar VITE_API_URL no .env
3. Verificar console do navegador (F12)

### Dados não aparecem:
1. Mock data está ativo por padrão
2. Para dados reais, implementar integrações conforme seção acima

### Erros de compilação:
```powershell
# Limpar e recompilar
dotnet clean
dotnet restore
dotnet build
```

---

## ✅ Checklist de Setup

Backend:
- [ ] Serviços registrados no Program.cs
- [ ] Backend compilando sem erros
- [ ] API acessível em http://localhost:5000
- [ ] Endpoint /api/quantstrategy/dashboard respondendo

Frontend:
- [ ] Dependências instaladas (npm install)
- [ ] Arquivo .env configurado
- [ ] Frontend rodando em http://localhost:5173
- [ ] Rota /quant acessível
- [ ] Gráficos carregando

Dados (Opcional para produção):
- [ ] Fonte de dados NQ configurada
- [ ] Fonte de dados de opções configurada
- [ ] Testes com dados reais

---

## 📞 Suporte

Se encontrar problemas:
1. Verificar logs do backend
2. Verificar console do navegador
3. Verificar se todos os serviços estão registrados
4. Consultar QUANT_STRATEGY_GUIDE.md para mais detalhes

---

**Boa sorte com sua estratégia quantitativa! 🚀📊**
