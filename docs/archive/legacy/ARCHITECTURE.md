# NTBot - Arquitetura do Sistema de Trading Automatizado

## 📋 Visão Geral

Sistema automatizado de trading para MNQ (Micro Nasdaq-100) integrando:
- **Análise Wyckoff** (estrutura de mercado)
- **Contexto Macro** (viés top-down)
- **Agenda Econômica** (filtro de risco)
- **IA para Análise de Notícias** (sentiment analysis)
- **NinjaTrader Integration** (execução real)
- **Dashboard Web** (monitoramento e controle)
- **Multi-Tenancy** (SaaS)

---

## 🏗️ Arquitetura de Camadas

```
┌─────────────────────────────────────────────────────────────┐
│                     FRONTEND (Dashboard)                     │
│  React/Blazor + SignalR + TradingView Charts               │
└─────────────────────────────────────────────────────────────┘
                            ↕ REST API / WebSockets
┌─────────────────────────────────────────────────────────────┐
│                    NTBot.Api (Backend)                       │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  Controllers Layer                                   │   │
│  │  - OrdersController, SignalsController, etc.       │   │
│  └─────────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  Application Services                                │   │
│  │  - Trading Service, Signal Service, etc.           │   │
│  └─────────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  Domain Services (Core Logic)                       │   │
│  │  - Wyckoff Engine                                   │   │
│  │  - Macro Context Analyzer                           │   │
│  │  - Economic Calendar Service                        │   │
│  │  - News AI Analyzer                                 │   │
│  │  - Decision Engine                                  │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                            ↕
┌─────────────────────────────────────────────────────────────┐
│              NinjaTrader Integration Layer                   │
│  - ATI (Automated Trading Interface)                        │
│  - Market Data Feed (real-time)                            │
│  - Order Execution                                          │
└─────────────────────────────────────────────────────────────┘
                            ↕
┌─────────────────────────────────────────────────────────────┐
│                    External Services                         │
│  - Economic Calendar API (Investing.com / FMP)             │
│  - News APIs (NewsAPI, AlphaVantage)                       │
│  - AI/NLP Service (Azure Cognitive / Custom Model)         │
└─────────────────────────────────────────────────────────────┘
```

---

## 🎯 Componentes Principais

### 1. **NinjaTrader Integration Service**
- **Responsabilidade**: Comunicação bidirecional com NinjaTrader
- **Tecnologia**: C# + ATI (NinjaTrader API) / WebSockets
- **Funcionalidades**:
  - Conexão com NT8
  - Streaming de dados em tempo real (candles, volume, order flow)
  - Envio de ordens (Market, Limit, Stop)
  - Gerenciamento de posições abertas
  - Histórico de trades

### 2. **Wyckoff Analysis Engine**
- **Responsabilidade**: Detecção automatizada de padrões Wyckoff
- **Tecnologia**: C# (processamento em memória, high-performance)
- **Funcionalidades**:
  - Identificação de fases: Acumulação / Distribuição
  - Detecção de eventos: PS, SC, AR, ST, Spring, Upthrust
  - Análise multi-timeframe (1m, 5m, 15m, 1D)
  - Volume Profile e Delta Analysis
  - Range detection e breakout validation

### 3. **Macro Context Analyzer**
- **Responsabilidade**: Análise top-down de mercado
- **Tecnologia**: C# + APIs de mercado
- **Funcionalidades**:
  - Bias de mercado (NQ, ES, DXY, VIX)
  - Correlações entre ativos
  - Regime de volatilidade
  - Risk-on / Risk-off detection
  - Tendência multi-timeframe

### 4. **Economic Calendar Service**
- **Responsabilidade**: Filtro de risco baseado em eventos econômicos
- **Tecnologia**: C# + APIs externas (FMP, Investing.com)
- **Funcionalidades**:
  - Cache de eventos econômicos (diário)
  - Filtro de alto impacto (FOMC, CPI, NFP, etc.)
  - Janelas de bloqueio automático
  - Notificações pré-evento

### 5. **News AI Analyzer**
- **Responsabilidade**: Análise de sentimento e impacto de notícias
- **Tecnologia**: Python + NLP (BERT, FinBERT) / Azure Cognitive Services
- **Funcionalidades**:
  - Web scraping (Bloomberg, Reuters, Twitter/X)
  - Sentiment analysis (positivo/negativo/neutro)
  - Entity recognition (empresas, países, eventos)
  - Impact scoring por ativo
  - Real-time news feed

### 6. **Decision Engine**
- **Responsabilidade**: Lógica central de decisão
- **Tecnologia**: C# (Rules Engine)
- **Funcionalidades**:
  - Combina sinais: Wyckoff + Macro + Calendar + News
  - Calcula score de confiança
  - Define direção (Long/Short/Neutral)
  - Calcula sizing (ATR-based)
  - Gerencia risk/reward (RR mínimo 1:2)
  - Stop loss dinâmico (trailing)

### 7. **Backtesting Engine**
- **Responsabilidade**: Validação de estratégias
- **Tecnologia**: C# (processamento paralelo)
- **Funcionalidades**:
  - Replay histórico (tick-by-tick)
  - Métricas: Sharpe, Sortino, Max DD, Win Rate
  - Walk-forward analysis
  - Monte Carlo simulation
  - Optimization (grid search)

### 8. **Dashboard Web**
- **Responsabilidade**: Interface de controle e monitoramento
- **Tecnologia**: React + TypeScript / Blazor
- **Funcionalidades**:
  - Gráficos em tempo real (TradingView Charting Library)
  - Painel de controle (start/stop, manual override)
  - Histórico de sinais e trades
  - Performance analytics
  - Configuração de parâmetros
  - Multi-asset monitoring

### 9. **Multi-Tenancy & SaaS**
- **Responsabilidade**: Suporte a múltiplos clientes
- **Tecnologia**: ASP.NET Core + JWT + EF Core
- **Funcionalidades**:
  - Autenticação/Autorização (JWT)
  - Tenant isolation (database per tenant / shared schema)
  - Subscription plans (Free, Pro, Enterprise)
  - Billing integration (Stripe)
  - Asset selection per user
  - Usage analytics

---

## 🗄️ Estrutura de Dados

### Database Schema (SQL Server / PostgreSQL)

```sql
-- Tenants
CREATE TABLE Tenants (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(100),
    Plan NVARCHAR(50), -- Free, Pro, Enterprise
    CreatedAt DATETIME2,
    IsActive BIT
);

-- Users
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER REFERENCES Tenants(Id),
    Email NVARCHAR(255),
    PasswordHash NVARCHAR(MAX),
    Role NVARCHAR(50), -- Admin, Trader
    CreatedAt DATETIME2
);

-- Assets Configuration
CREATE TABLE AssetConfigurations (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER REFERENCES Tenants(Id),
    Symbol NVARCHAR(20),
    IsActive BIT,
    MaxPositionSize INT,
    RiskPerTrade DECIMAL(5,2), -- %
    Timeframes NVARCHAR(100), -- JSON: ["1m","5m","15m"]
    CreatedAt DATETIME2
);

-- Signals
CREATE TABLE Signals (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER REFERENCES Tenants(Id),
    Symbol NVARCHAR(20),
    Direction NVARCHAR(10), -- LONG, SHORT
    ConfidenceScore DECIMAL(5,2),
    WyckoffPhase NVARCHAR(50),
    MacroBias NVARCHAR(20),
    NewsImpact DECIMAL(5,2),
    EconomicEventActive BIT,
    EntryPrice DECIMAL(18,8),
    StopLoss DECIMAL(18,8),
    TakeProfit DECIMAL(18,8),
    Status NVARCHAR(20), -- PENDING, EXECUTED, CANCELLED
    CreatedAt DATETIME2
);

-- Trades
CREATE TABLE Trades (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER REFERENCES Tenants(Id),
    SignalId UNIQUEIDENTIFIER REFERENCES Signals(Id),
    Symbol NVARCHAR(20),
    Direction NVARCHAR(10),
    EntryPrice DECIMAL(18,8),
    ExitPrice DECIMAL(18,8),
    Quantity INT,
    PnL DECIMAL(18,2),
    Commission DECIMAL(18,2),
    EntryTime DATETIME2,
    ExitTime DATETIME2,
    Duration INT, -- seconds
    Status NVARCHAR(20) -- OPEN, CLOSED
);

-- Market Data (Cache)
CREATE TABLE MarketData (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Symbol NVARCHAR(20),
    Timeframe NVARCHAR(10),
    OpenTime DATETIME2,
    Open DECIMAL(18,8),
    High DECIMAL(18,8),
    Low DECIMAL(18,8),
    Close DECIMAL(18,8),
    Volume BIGINT,
    Delta BIGINT, -- Order Flow Delta
    INDEX IX_Symbol_Time (Symbol, OpenTime)
);

-- Economic Events
CREATE TABLE EconomicEvents (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    EventName NVARCHAR(200),
    Country NVARCHAR(50),
    Impact NVARCHAR(20), -- HIGH, MEDIUM, LOW
    EventTime DATETIME2,
    Actual NVARCHAR(50),
    Forecast NVARCHAR(50),
    Previous NVARCHAR(50),
    CreatedAt DATETIME2,
    INDEX IX_EventTime (EventTime)
);

-- News Analysis
CREATE TABLE NewsAnalysis (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Title NVARCHAR(500),
    Source NVARCHAR(100),
    PublishedAt DATETIME2,
    SentimentScore DECIMAL(5,2), -- -1 to +1
    ImpactScore DECIMAL(5,2), -- 0 to 100
    RelatedSymbols NVARCHAR(MAX), -- JSON array
    Content NVARCHAR(MAX),
    CreatedAt DATETIME2
);
```

---

## 🔄 Fluxo de Execução

### 1. **Inicialização**
```
1. NTBot.Api inicia
2. Conecta com NinjaTrader (ATI)
3. Carrega configurações do tenant
4. Inicia serviços (Wyckoff, Macro, Calendar, News)
5. Abre WebSocket para Dashboard
```

### 2. **Loop Principal (Real-Time)**
```
A cada tick/candle do NinjaTrader:

1. RECEBE dados de mercado (price, volume, delta)
2. ATUALIZA cache em memória (últimas 8 horas)
3. PROCESSA Wyckoff Engine
   - Identifica fase atual
   - Detecta eventos (Spring, Upthrust, etc.)
4. PROCESSA Macro Context
   - Atualiza bias (Daily/H1)
   - Calcula correlações
5. VERIFICA Economic Calendar
   - Se evento de alto impacto próximo → BLOQUEIA trades
6. CONSULTA News AI
   - Sentiment atual do mercado
7. DECISION ENGINE processa todos os inputs
   - Gera sinal (se condições atendidas)
   - Calcula entry, SL, TP
8. Se SINAL VÁLIDO:
   - ENVIA ordem para NinjaTrader
   - SALVA no banco (Signals + Trades)
   - NOTIFICA Dashboard via WebSocket
9. GERENCIA posições abertas
   - Trailing stop
   - Proteção de lucro
   - Exit por tempo
```

### 3. **Dashboard Update**
```
A cada 1 segundo:
- Envia status de conexão NT
- Envia posições abertas
- Envia últimos sinais
- Envia P&L do dia
- Envia gráficos atualizados (via SignalR)
```

---

## 🚀 Stack Tecnológica

### Backend
- **C# .NET 8** (Performance e integração NT)
- **ASP.NET Core Web API** (REST + SignalR)
- **Entity Framework Core** (ORM)
- **Dapper** (queries otimizadas)
- **MediatR** (CQRS pattern)
- **FluentValidation** (validação)
- **Serilog** (logging estruturado)

### Frontend
- **React 18 + TypeScript** (UI moderna)
- **TradingView Charting Library** (gráficos profissionais)
- **Material-UI / Ant Design** (componentes)
- **SignalR Client** (real-time)
- **Redux Toolkit** (state management)
- **React Query** (data fetching)

### IA & ML
- **Python 3.11** (NLP e ML)
- **Transformers (HuggingFace)** (BERT, FinBERT)
- **spaCy** (NLP)
- **FastAPI** (micro-serviço Python)
- **Azure Cognitive Services** (alternativa cloud)

### Database
- **SQL Server** (principal - compatibilidade NT)
- **Redis** (cache de alta performance)
- **TimescaleDB** (opcional - time-series data)

### DevOps
- **Docker + Docker Compose** (containers)
- **GitHub Actions** (CI/CD)
- **Azure / AWS** (cloud deployment)
- **Prometheus + Grafana** (monitoring)
- **Seq / ELK** (log aggregation)

---

## 📊 Métricas de Performance

### SLA Targets
- **Latência de Sinal**: < 100ms (da leitura do tick até decisão)
- **Latência de Execução**: < 50ms (envio de ordem ao NT)
- **Uptime**: 99.9% (durante horário de mercado)
- **Dashboard Update**: 1 FPS mínimo (real-time)

### Monitoramento
- CPU / Memory usage
- Tick processing rate (ticks/segundo)
- Signal generation rate
- Order execution success rate
- Database query performance
- API response times

---

## 🔒 Segurança

1. **Autenticação**: JWT com refresh tokens
2. **Autorização**: Role-based (Admin, Trader, Viewer)
3. **Encryption**: HTTPS obrigatório, senhas com bcrypt
4. **Tenant Isolation**: Query filters automáticos (EF Core)
5. **Rate Limiting**: Proteção contra abuso de API
6. **Audit Log**: Todas as ações críticas logadas
7. **API Keys**: Para integração NT (criptografadas no DB)

---

## 📈 Roadmap de Implementação

### Phase 1: Foundation (Semanas 1-2)
- ✅ Setup do projeto
- ✅ Estrutura de dados (migrations)
- ✅ NinjaTrader Integration básica
- ✅ API básica + autenticação

### Phase 2: Core Engine (Semanas 3-4)
- ⏳ Wyckoff Engine
- ⏳ Macro Context Analyzer
- ⏳ Decision Engine v1

### Phase 3: Intelligence (Semanas 5-6)
- ⏳ Economic Calendar Integration
- ⏳ News AI Analyzer
- ⏳ Signal scoring system

### Phase 4: Dashboard (Semanas 7-8)
- ⏳ Frontend React
- ⏳ Real-time charts
- ⏳ Control panel

### Phase 5: SaaS (Semanas 9-10)
- ⏳ Multi-tenancy
- ⏳ Subscription management
- ⏳ Billing integration

### Phase 6: Production (Semanas 11-12)
- ⏳ Backtesting completo
- ⏳ Optimization
- ⏳ Deploy em cloud
- ⏳ Monitoring completo

---

## 🎓 Referências Técnicas

- **Wyckoff Method**: Richard D. Wyckoff original works
- **NinjaTrader API**: https://ninjatrader.com/support/helpGuides/nt8/
- **Order Flow**: Market Profile, Footprint Charts
- **Sentiment Analysis**: FinBERT paper (Araci, 2019)
- **Trading Systems**: "Systematic Trading" by Robert Carver

---

**Última atualização**: 2025-12-28  
**Versão**: 2.0  
**Status**: 🚀 Em desenvolvimento ativo
