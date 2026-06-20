# NTBot — Análise Completa do Repositório

**Data:** 20 de junho de 2026  
**Fase:** 1 — Análise (sem implementação)  
**Versão analisada:** NTBot 2.0.0 (.NET 8.0)

---

## 1. Visão Executiva

O repositório `ntbot` é um **sistema de trading automatizado** com foco inicial em futuros (MNQ/NQ) e integração B3 via ProfitChart. Possui backend funcional em monolito ASP.NET Core, dashboard React parcial e documentação extensa, porém **não está pronto para SaaS enterprise** na forma desejada.

| Dimensão | Status | Nota |
|----------|--------|------|
| Backend core (Wyckoff, Macro, Quant) | 🟢 ~75% | Serviços implementados, dados mock em partes |
| ProfitChart / B3 | 🟢 ~80% | RTD + SignalR + REST funcionais |
| NinjaTrader / MT5 | 🟡 ~50% | Código de referência, integração não validada |
| Multi-tenancy | 🟡 ~40% | Schema + CRUD tenant, sem isolamento runtime |
| Autenticação | 🔴 ~10% | JWT configurado, sem endpoints |
| Billing / Stripe | 🔴 0% | Não existe no ntbot |
| Frontend React | 🟡 ~45% | 3 telas reais, resto mock/placeholder |
| Observabilidade | 🔴 ~5% | Serilog apenas |
| Docker / CI/CD | 🟡 ~20% | Dockerfiles básicos, sem compose/Actions |
| Testes | 🔴 0% | Sem projetos de teste na solution |

**Projeto relacionado para reaproveitamento:** `C:\Projetos\barberai` (BarbeAI) — Clean Architecture, MVC, PostgreSQL, Stripe, auth completa, deploy Coolify.

---

## 2. Estrutura do Repositório

```
ntbot/
├── NTBot.Api/              # Monolito ASP.NET Core 8 — API + Hubs + Services
├── ntbot-dashboard/        # Frontend React 19 + Vite 7 + TypeScript + Tailwind
├── Simulador/              # Backtest/replay CSV → chama /orders/next
├── MT5/                    # Expert Advisor + Include (MetaTrader 5)
├── NinjaScript/            # Scripts NinjaTrader (referência)
├── Libs/                   # Interop.RTDTrading.dll (ProfitChart COM)
├── Dashboard/              # Pasta vazia (legado documentado)
├── docs/                   # Documentação desta análise (novo)
├── *.md                    # 27 arquivos de documentação na raiz
├── NTBot.sln               # 2 projetos: NTBot.Api + Simulador
└── .github/workflows/      # Pasta existe, sem workflows ativos
```

### 2.1 Solution (.NET)

| Projeto | Target | Função |
|---------|--------|--------|
| `NTBot.Api` | net8.0 | API REST, SignalR, EF Core, trading engines |
| `Simulador` | net8.0 | Replay histórico via CSV → API legacy |

**Ausentes vs. arquitetura desejada:** NtBot.Web, Application, Domain (isolado), Infrastructure, Identity, Billing, MarketData, Trading, testes.

---

## 3. NTBot.Api — Análise Profunda

### 3.1 Organização Interna

```
NTBot.Api/
├── Controllers/        # 8 controllers REST
├── Hubs/               # 6 hubs SignalR
├── Services/           # Engines de trading e integrações
├── Strategies/         # ChochStrategy, QuantStrategy
├── Models/             # Entidades EF Core (usadas de fato)
├── Domain/             # Entidades duplicadas — NÃO USADAS
├── Data/               # NTBotDbContext + Factory
├── Migrations/         # InitialCreate (20260512)
├── Application/        # Pastas Commands/Queries/Handlers — VAZIAS
├── Infrastructure/     # ExternalServices, Repositories — parcial/vazio
├── Examples/           # ProfitChartExamples
└── Program.cs          # Bootstrap monolítico
```

### 3.2 Controllers e Endpoints

| Controller | Rota | Auth | Estado |
|------------|------|------|--------|
| `AnalysisController` | `/api/analysis/*` | ❌ | Wyckoff, Macro, Complete |
| `TenantsController` | `/api/tenants` | ❌ | CRUD completo |
| `OrdersController` | `/orders/next` | ❌ | Legacy CHOCH |
| `ProfitChartController` | `/api/profitchart/*` | ❌ | 8 endpoints RTD |
| `QuantStrategyController` | `/api/quantstrategy/*` | ❌ | GEX + Correlação |
| `GridController` | `/api/grid` | ✅ `[Authorize]` | Grid engine |
| `MarketDataController` | `/api/marketdata` | ❌ | Ticks externos |
| `MT5Controller` | `/api/mt5` | ❌ | Bridge MT5 |
| Minimal API | `/api/health` | ❌ | Health check básico |

**Endpoints ausentes críticos:** `/api/auth/*`, `/api/signals`, `/api/trades`, `/api/users`, billing, admin.

### 3.3 SignalR Hubs

| Hub | Rota | Integração Real |
|-----|------|-----------------|
| `ProfitChartHub` | `/hubs/profitchart` | ✅ Conectado ao ProfitService |
| `TradingHub` | `/hubs/trading` | 🟡 Stub — grupos apenas |
| `MarketHub` | `/hubs/market` | 🟡 Stub |
| `RiskHub` | `/hubs/risk` | 🟡 Stub |
| `ExecutionHub` | `/hubs/execution` | 🟡 Stub |
| `NotificationHub` | `/hubs/notification` | 🟡 Stub |

### 3.4 Serviços Implementados

| Serviço | Linhas ~ | Qualidade | Notas |
|---------|----------|-----------|-------|
| `WyckoffService` | 500+ | 🟢 Produção-ready | Fases, eventos, multi-TF |
| `MacroContextService` | 400+ | 🟢 Produção-ready | VIX, bias, correlações |
| `NinjaTraderService` | 700+ | 🟡 Referência | REST/WS, não testado E2E |
| `ProfitService` (IRtdService) | — | 🟢 Funcional | SignalR client + cache |
| `GlobalCorrelationService` | — | 🟡 Mock data | NQ/WIN Pearson/Spearman |
| `GammaExposureService` | — | 🟡 Mock options | GEX, walls, flip |
| `QuantStrategy` | — | 🟢 Lógica completa | Integra 3 módulos |
| `GridEngine` | — | 🟡 Parcial | Persistência DB stub |
| `TradingService` | — | 🔴 Mock | Posições/ordens fake |
| `RiskManager` | — | 🟡 Parcial | Usa TradingService mock |
| `TenantService` | — | 🟢 CRUD EF | Sem row-level security |

**Comentados no Program.cs (não implementados):**
- `EconomicCalendarService`
- `NewsAnalyzerService`
- `TradingDecisionEngine`
- `TradingOrchestrator` (BackgroundService)

### 3.5 Banco de Dados

- **Provider:** PostgreSQL (configurado), fallback SQLite/SQL Server
- **ORM:** EF Core 8.0
- **Migration:** `20260512091406_InitialCreate`
- **Connection string em `appsettings.json`:** ⚠️ **CREDENCIAL EXPOSTA** — rotacionar imediatamente

**Tabelas mapeadas (22 DbSets):**

| Grupo | Entidades |
|-------|-----------|
| SaaS base | Tenants, Users, AssetConfigurations |
| Trading core | TradingSignals, Trades, Candles |
| Market intel | EconomicEvents, NewsAnalyses |
| Multi-broker | TradePositions, TradeExecutions, AccountInfos |
| Grid/Risk | GridOrders, GridLevels, RiskConfigs, StrategySignals |
| Market data | OrderBooks, OrderBookLevels, TickData |
| Analytics | DailyResults, TradingSessions |

**Problema estrutural:** pasta `Domain/Entities/` duplica `Models/` mas **nunca é referenciada**. EF usa apenas `Models/`.

### 3.6 Pacotes NuGet

**Usados:** EF Core (Sqlite, Npgsql, SqlServer), SignalR, JWT Bearer, Serilog, Swashbuckle, HttpClient.

**Não usados apesar de referenciados:** BCrypt.Net-Next, Polly, Interop.RTDTrading.dll.

**Ausentes vs. spec:** MediatR, FluentValidation, AutoMapper, Redis, Stripe, OpenTelemetry.

### 3.7 Configuração (`appsettings.json`)

| Seção | Estado |
|-------|--------|
| ConnectionStrings | PostgreSQL remoto configurado |
| Jwt | Chave placeholder insegura |
| NinjaTrader | localhost:8080 |
| Trading | MNQ defaults |
| EconomicCalendar | API key placeholder |
| NewsAnalyzer | URL localhost:8000 |
| Wyckoff/Macro | Parâmetros tuning |

---

## 4. ntbot-dashboard — Análise do Frontend React

### 4.1 Stack

- React 19.2, TypeScript 5.9, Vite 7.2
- Tailwind CSS 3.4, Radix UI (parcial)
- Zustand 5, Axios, SignalR Client 10
- lightweight-charts (instalado, não usado)
- vite-plugin-pwa

### 4.2 Rotas

| Rota | Arquivo | Implementação |
|------|---------|-----------------|
| `/` | Dashboard.tsx | 🟡 Parcial — REST + ProfitChart |
| `/scalping` | ScalpingPanel.tsx | 🟡 Mock + SignalR condicional |
| `/grid` | GridManager.tsx | 🔴 Mock local |
| `/positions` | Positions.tsx | 🔴 Dados hardcoded |
| `/risk` | RiskManagement.tsx | 🔴 Mock |
| `/wyckoff` | WyckoffAnalysis.tsx | 🔴 Placeholder |
| `/macro` | MacroAnalysis.tsx | 🔴 Placeholder |
| `/quant` | QuantStrategy.tsx | 🟢 Completo |
| `/profitchart` | ProfitChart.tsx | 🟢 Completo |
| `/signals` | Signals.tsx | 🔴 Placeholder |
| `/trades` | Trades.tsx | 🔴 Placeholder |
| `/settings` | Settings.tsx | 🔴 Placeholder |

**Ausentes:** Auth, Admin, Billing, Área do Cliente, Landing pública.

### 4.3 Serviços Frontend

| Serviço | Base URL | Uso real |
|---------|----------|----------|
| `api.service.ts` | `/api` | Dashboard parcial |
| `quantStrategyApi.ts` | `/api/quantstrategy` | Quant page |
| `profitchart.api.ts` | `/api/profitchart` | ProfitChart |
| `signalr.service.ts` | `/hubs/trading` | **Desabilitado** |
| `profitchart.signalr.ts` | `/hubs/profitchart` | Ativo |

### 4.4 State Management

- `useTradingStore` — candles, signals, trades, isConnected (nunca populado via hub)
- `useAuthStore` — **nunca importado na UI**

### 4.5 Design System Atual

```css
/* tailwind.config.js */
primary: sky (#0ea5e9)
success: #10b981 | danger: #ef4444 | warning: #f59e0b
background: slate-900 (#0f172a) | cards: slate-800
font: Inter
```

Compatível com dark theme TradingView/Binance — base sólida para migração Blazor.

---

## 5. BarbeAI (`C:\Projetos\barberai`) — Código Reaproveitável

> **Nota:** O projeto referenciado como `/BarbeAI` está em `C:\Projetos\barberai`, fora deste repositório.

### 5.1 Arquitetura BarberAI

```
barberai/src/
├── BarberAI.Api/           # MVC + API + Razor Views
├── BarberAI.Application/   # Services, DTOs
├── BarberAI.Domain/        # Entities, Interfaces
├── BarberAI.Infrastructure/# EF, Repositories, Migrations
├── BarberAI.Tests/
└── BarberAI.AK/            # Agentes IA
```

**Padrão:** Clean Architecture real — diferente do NTBot monolito.

### 5.2 Componentes Reaproveitáveis para NtBot

| Módulo BarberAI | Destino NtBot | Prioridade |
|-----------------|---------------|------------|
| `AuthViewController` + Views | NtBot.Identity / NtBot.Web | Alta |
| Cookie auth + DataProtection PG | NtBot.Web SSR | Alta |
| `OtpVerificationService` | MFA / Forgot Password | Alta |
| `StripeService` + `PaymentService` | NtBot.Billing | Alta |
| `PaymentController` + webhooks | NtBot.Billing | Alta |
| `Subscription`, `Plan` entities | NtBot.Domain | Alta |
| Repository + UnitOfWork pattern | NtBot.Infrastructure | Média |
| `ConfigureCoolifyHosting()` | NtBot deploy | Média |
| Docker deploy scripts | deploy/ | Média |
| MercadoPago (opcional) | Descartar para NTBot | Baixa |

### 5.3 Auth BarberAI — Detalhes

- **MVC Cookie Authentication** (não JWT puro para web)
- Login, Register, ForgotPassword, OTP email
- Validação de assinatura ativa no login
- Roles: Admin, Barber, etc. (adaptar para trading roles)
- API separada com `X-Application-Key` + `X-API-Key` (tenant)

**Gap:** NTBot spec pede JWT + Refresh Token para API/Blazor WASM interactive — combinar cookie (SSR) + JWT (API/SignalR).

---

## 6. Integrações Existentes

| Integração | Local | Maturidade |
|------------|-------|------------|
| **ProfitChart RTD** | Services/Profit, Hub, Controller | 🟢 Alta |
| **NinjaTrader 8** | Services/NinjaTrader | 🟡 Média |
| **MetaTrader 5** | MT5/Experts, MT5Controller | 🟡 Baixa |
| **NinjaScript** | NinjaScript/ | Referência |
| **TradingView** | Documentado, não implementado | 🔴 |
| **Stripe** | Apenas em barberai | 🔴 |
| **Redis** | Documentado, não implementado | 🔴 |
| **Economic Calendar (FMP)** | Config only | 🔴 |
| **News AI (Python)** | Documentado, pasta ausente | 🔴 |

---

## 7. Documentação Existente (27 arquivos .md)

| Categoria | Arquivos |
|-----------|----------|
| Arquitetura | ARCHITECTURE.md, README.md, README_IMPLEMENTATION.md |
| Status | STATUS.md, EXECUTIVE_SUMMARY.md, DASHBOARD_STATUS.md |
| ProfitChart | PROFITCHART_*.md (6 arquivos) |
| Quant | QUANT_*.md (4 arquivos) |
| NinjaTrader | NINJATRADER_*.md (3 arquivos) |
| Quick starts | QUICK_START.md, GETTING_STARTED.md, TEST_SCRIPT.md |
| Dashboard | ntbot-dashboard/README.md, INSTALL.md, BACKEND_SETUP.md |

**Problema:** Documentação na raiz é extensa mas **desatualizada** vs. código real (ex.: afirma 70–80% completo quando auth/billing/testes estão ausentes).

---

## 8. Código Reaproveitável — Inventário

### 8.1 Migrar com mínima alteração

- `WyckoffService` → `NtBot.Trading` ou `NtBot.Application`
- `MacroContextService` → `NtBot.MarketData` / Analytics
- `QuantStrategy` + GEX/Correlation → `NtBot.Trading.Strategies`
- `ProfitService` + Hubs → `NtBot.MarketData.Providers.ProfitChart`
- `NTBotDbContext` + Models → base `NtBot.Infrastructure` (refatorar)
- `GridEngine`, `RiskManager` → `NtBot.Trading`
- Componentes React (design tokens) → CSS/Tailwind em Blazor
- `quant/` e `profitchart/` UI patterns → Blazor Components

### 8.2 Descartar ou arquivar

- `Domain/Entities/` (duplicata morta)
- `Application/Commands|Queries|Handlers/` (vazias)
- `App.backup.tsx`, `App-test.tsx`, `TestComponent.tsx`
- Pasta `Dashboard/` vazia
- Documentação de status obsoleta (mover para `docs/archive/`)

---

## 9. Riscos Técnicos

### 9.1 Críticos (P0)

| Risco | Impacto | Mitigação |
|-------|---------|-----------|
| **Secrets em appsettings.json commitado** | Comprometimento DB | Rotacionar credenciais, User Secrets, env vars |
| **API sem autenticação** | Acesso total aos dados | Implementar auth antes de deploy |
| **JWT key placeholder** | Tokens forjáveis | Key vault + rotação |
| **BarbeAI fora do monorepo** | Dependência implícita | Submodule ou copy controlado |

### 9.2 Altos (P1)

| Risco | Impacto | Mitigação |
|-------|---------|-----------|
| Monolito sem separação de camadas | Escalabilidade limitada | Split em projetos src/ |
| Domain/Models duplicados | Bugs de inconsistência | Unificar em NtBot.Domain |
| Hubs SignalR stub | Dashboard realtime quebrado | Integrar com services reais |
| ProfitChart Windows-only | Deploy Linux impossível para RTD | Worker Windows separado |
| .NET 8 vs spec .NET 9 | Gap de versão | Upgrade na Fase 2 |
| Zero testes | Regressões | Criar tests/ desde Fase 2 |

### 9.3 Médios (P2)

| Risco | Impacto | Mitigação |
|-------|---------|-----------|
| Mock data em Quant/Macro | Sinais incorretos | Providers reais |
| GridEngine DB stub | Perda de grids | Implementar persistência |
| Node.js 18 vs Vite 7 | Build frontend falha | Atualizar ou migrar para Blazor |
| Documentação inflada | Decisões erradas | Esta análise como source of truth |

---

## 10. Gap Analysis — Estado Atual vs. Objetivo

| Requisito | Atual | Gap |
|-----------|-------|-----|
| ASP.NET MVC + Blazor Interactive | React SPA | 100% rewrite frontend |
| .NET 9 | .NET 8 | Upgrade |
| Clean Architecture multi-projeto | Monolito | Restructure |
| PostgreSQL | ✅ Configurado | Migrations + billing tables |
| Redis | ❌ | Implementar |
| Stripe Billing | ❌ (barberai ✅) | Port from barberai |
| Identity completa | ❌ (barberai ✅) | Port + adapt |
| Multi-tenant isolado | Parcial | Row-level security + middleware |
| TradingView | ❌ | Novo módulo |
| IB/MT5/NT connectors | Parcial | Plugin architecture |
| SignalR realtime | Hubs existem, stubs | Wire-up |
| OpenTelemetry/Grafana | ❌ | Novo |
| Docker/Coolify/CI | Parcial | Completar |
| Testes | ❌ | Criar suite |

---

## 11. Métricas do Codebase

| Métrica | Valor estimado |
|---------|----------------|
| Arquivos C# (NTBot.Api) | ~126 |
| Arquivos TS/TSX (dashboard) | ~48 |
| Linhas C# | ~12.000+ |
| Linhas TS/React | ~3.500+ |
| Linhas documentação .md | ~8.000+ |
| Controllers | 8 |
| SignalR Hubs | 6 |
| Services | 12+ |
| EF Entities | 22 DbSets |
| Test coverage | 0% |

---

## 12. Conclusão da Fase 1

O NTBot possui **valor significativo no backend de trading** (Wyckoff, Macro, Quant, ProfitChart) e **base de UI React** parcialmente funcional. Porém, para a plataforma SaaS enterprise desejada, é necessário:

1. **Reestruturar** em Clean Architecture (.NET 9)
2. **Portar** Identity + Stripe do BarberAI
3. **Substituir** React por MVC + Blazor Interactive Components
4. **Completar** auth, billing, observabilidade e deploy
5. **Remediar** riscos de segurança imediatamente

**Próximo passo:** Revisar `ARCHITECTURE_PROPOSAL.md`, `MIGRATION_PLAN.md` e demais docs desta pasta antes de iniciar Fase 2.

---

*Documento gerado automaticamente — Fase 1 concluída.*
