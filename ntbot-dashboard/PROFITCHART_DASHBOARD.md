# ProfitChart Dashboard Integration

## Visão Geral

Integração completa do ProfitChart RTD com o dashboard React, fornecendo visualização em tempo real de dados de mercado através de REST API e WebSocket (SignalR).

## Arquitetura

```
Backend (`src/NtBot.Api`)
│
├── Services/Profit/
│   ├── IRtdService.cs          → Interface com contratos
│   └── RTDService.cs           → Implementação RTD + Cache + SignalR
│
├── Hubs/
│   └── ProfitChartHub.cs       → SignalR Hub (WebSocket)
│
└── Controllers/
    └── ProfitChartController   → 8 REST endpoints

Frontend (ntbot-dashboard)
│
├── types/
│   └── profitchart.ts          → TypeScript types (13 interfaces)
│
├── services/
│   ├── profitchart.api.ts      → REST API client (axios)
│   └── profitchart.signalr.ts  → SignalR client (WebSocket)
│
└── components/profitchart/
    ├── TickerCard.tsx          → Card de ticker com preço real-time
    ├── ProfitChartStats.tsx    → Widget de estatísticas
    └── BookOfertas.tsx         → Visualização de Book/DOM
```

## Componentes

### 1. TickerCard
Card compacto mostrando preço em tempo real de um ativo.

**Props:**
- `ticker: string` - Código do ativo (WIN, WDO, USD/BRL, etc.)
- `logicalName?: string` - Nome amigável (opcional)
- `autoSubscribe?: boolean` - Auto-subscrever no SignalR (padrão: false)

**Features:**
- Atualização em tempo real via SignalR
- Indicador de tendência (↑↓)
- Formatação de valores (K/M)
- Visual de variação com cores

**Exemplo:**
```tsx
<TickerCard 
  ticker="WIN" 
  logicalName="Mini Índice" 
  autoSubscribe={true} 
/>
```

### 2. ProfitChartStats
Widget mostrando estatísticas gerais do sistema RTD.

**Props:**
- `refreshInterval?: number` - Intervalo de refresh em ms (padrão: 5000)

**Exibe:**
- Status de conexão (Ativo/Inativo)
- Taxa de atualização (ticks/s)
- Uptime do servidor
- Contadores de tópicos/tickers

**Exemplo:**
```tsx
<ProfitChartStats refreshInterval={5000} />
```

### 3. BookOfertas
Visualização do Book de Ofertas (DOM).

**Props:**
- `ticker: string` - Ativo para mostrar book
- `levels?: number` - Níveis a exibir (padrão: 5)
- `autoRefresh?: boolean` - Auto-refresh a cada 1s (padrão: false)

**Features:**
- Níveis de compra (verde) e venda (vermelho)
- Barras visuais de quantidade
- Atualização via SignalR
- Fallback para REST API

**Exemplo:**
```tsx
<BookOfertas 
  ticker="WDO" 
  levels={10} 
  autoRefresh={true} 
/>
```

## Serviços

### profitchart.api.ts (REST)

Cliente REST para consumir ProfitChartController.

**Métodos:**
```typescript
getStatistics()                    // GET /api/profitchart/statistics
getAllTickers()                    // GET /api/profitchart/tickers
getTickerSnapshot(ticker)          // GET /api/profitchart/tickers/{ticker}
getTopicValue(ticker, topic)       // GET /api/profitchart/tickers/{ticker}/{topic}
getPrices(tickers)                 // POST /api/profitchart/prices
getBook(ticker, levels?)           // GET /api/profitchart/book/{ticker}
getConfig(logicalName)             // GET /api/profitchart/config/{logicalName}
checkHealth()                      // GET /api/profitchart/health
```

**Exemplo:**
```typescript
import { profitChartApi } from '../services/profitchart.api';

// Buscar todos os tickers
const tickers = await profitChartApi.getAllTickers();

// Buscar book de ofertas
const book = await profitChartApi.getBook('WIN', 10);

// Verificar saúde do serviço
const health = await profitChartApi.checkHealth();
```

### profitchart.signalr.ts (WebSocket)

Cliente SignalR para streaming em tempo real.

**Métodos:**
```typescript
connect()                          // Conectar ao hub
disconnect()                       // Desconectar
subscribeTicker(ticker)            // Inscrever em ticker específico
subscribeAll()                     // Inscrever em todos os tickers
unsubscribe(ticker?)               // Desinscrever (null = todos)

// Event Handlers
onTickUpdate(handler)              // Callback para tick updates
onStatistics(handler)              // Callback para estatísticas
onConnectionChange(handler)        // Callback para mudanças de conexão
onError(handler)                   // Callback para erros
```

**Exemplo:**
```typescript
import { profitChartSignalR } from '../services/profitchart.signalr';

// Conectar
await profitChartSignalR.connect();

// Inscrever em ticker
await profitChartSignalR.subscribeTicker('WIN');

// Receber atualizações
profitChartSignalR.onTickUpdate((update) => {
  console.log('Tick recebido:', update);
  // { ticker, topic, value, timestamp }
});

// Desconectar ao desmontar componente
profitChartSignalR.disconnect();
```

## Páginas

### Dashboard (`/`)
Dashboard principal com seção de ProfitChart mostrando 3 cards (WIN, WDO, USD/BRL).

### ProfitChart (`/profitchart`)
Página dedicada com:
- Estatísticas gerais em tempo real
- Grid de todos os tickers configurados
- Book de ofertas do ticker selecionado
- Indicador de conexão em tempo real

## Tipos TypeScript

### RtdStatistics
```typescript
interface RtdStatistics {
  isConnected: boolean;
  uptime: string;
  tickersRegistered: number;
  topicsRegistered: number;
  lastUpdate: string | null;
  updateRate: number;
}
```

### TickerStatus
```typescript
interface TickerStatus {
  ticker: string;
  logicalName: string | null;
  isConnected: boolean;
  lastUpdate: string | null;
  topicsCount: number;
}
```

### TickUpdate
```typescript
interface TickUpdate {
  ticker: string;
  topic: string;
  value: any;
  timestamp: string;
}
```

### BookData
```typescript
interface BookData {
  ticker: string;
  timestamp: string;
  buyLevels: BookLevel[];
  sellLevels: BookLevel[];
}

interface BookLevel {
  price: number;
  quantity: number;
}
```

## Configuração

### Backend
Edite `src/NtBot.Api/rtd_config.json`:
```json
{
  "LogicalNames": [
    {
      "Name": "WIN",
      "Topics": {
        "LastTrade": 1,
        "BestBid": 2,
        "BestAsk": 3,
        "Volume": 4
      }
    }
  ]
}
```

### Frontend
Configure URL da API em `profitchart.api.ts`:
```typescript
const API_BASE_URL = 'http://localhost:5053/api';
```

Configure URL do SignalR em `profitchart.signalr.ts`:
```typescript
const SIGNALR_HUB_URL = 'http://localhost:5053/hubs/profitchart';
```

## Instalação

### Backend
```bash
cd src/NtBot.Api
dotnet restore
dotnet build
dotnet run
```

### Frontend
```bash
cd ntbot-dashboard
npm install
npm run dev
```

## Uso

### 1. Iniciar Backend
```bash
cd src/NtBot.Api
dotnet run
```
Backend disponível em: http://localhost:5053

### 2. Iniciar Dashboard
```bash
cd ntbot-dashboard
npm run dev
```
Dashboard disponível em: http://localhost:5173

### 3. Abrir ProfitChart
Certifique-se de que o ProfitChart está aberto e conectado para que o RTD receba dados.

### 4. Acessar Dashboard
Navegue para http://localhost:5173 e veja os dados em tempo real!

## Fluxo de Dados

```
ProfitChart (COM)
    ↓ RTD Protocol
RTDService.cs (Cache)
    ↓
    ├─→ REST API (ProfitChartController)
    │       ↓
    │   profitchart.api.ts
    │       ↓
    │   React Components
    │
    └─→ SignalR Hub (ProfitChartHub)
            ↓ WebSocket
        profitchart.signalr.ts
            ↓
        React Components (Real-Time)
```

## Troubleshooting

### Erro: "Cannot connect to SignalR"
- Verifique se o backend está rodando
- Confirme a URL do hub (http://localhost:5053/hubs/profitchart)
- Check CORS settings em Program.cs

### Erro: "No data received"
- Verifique se o ProfitChart está aberto
- Confirme rtd_config.json tem tickers configurados
- Verifique logs em `src/NtBot.Api/logs/`

### Componentes não atualizam
- Confirme autoSubscribe={true} nos TickerCards
- Verifique console do browser para erros de WebSocket
- Teste REST API diretamente: http://localhost:5053/api/profitchart/tickers

## Features Avançadas

### Auto-Reconnect
O SignalR client tenta reconectar automaticamente até 5 vezes com backoff exponencial.

### Caching
O RTDService mantém cache em memória para reduzir chamadas RTD.

### Error Handling
Todos os serviços têm tratamento de erro com fallbacks graceful.

### Performance
- Throttling de broadcasts (100ms cooldown)
- Lazy loading de componentes
- Memoização de cálculos pesados

## API Reference

Documentação completa dos endpoints REST disponível em:
- `PROFITCHART_INTEGRATION_GUIDE.md`
- `PROFITCHART_SUMMARY.md`
- Swagger UI (quando disponível): http://localhost:5053/swagger

## Próximos Passos

- [ ] Adicionar gráficos de preço (Candlestick)
- [ ] Implementar alertas de preço
- [ ] Exportar dados para CSV
- [ ] Adicionar histórico de trades
- [ ] Integrar com estratégias de trading

## Suporte

Para issues e dúvidas, consulte:
- README_IMPLEMENTATION.md
- PROFITCHART_INTEGRATION_GUIDE.md
- Logs: `src/NtBot.Api/logs/`
