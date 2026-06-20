# Blazor Web (`NtBot.Web`)

Frontend alvo da plataforma — Blazor Interactive Server (.NET 9).

## Run

```powershell
cd C:\Projetos\ntbot\src\NtBot.Web
dotnet run
# http://localhost:5001
```

## Rotas

| Rota | Status | Descrição |
|------|--------|-----------|
| `/` | ✅ | Landing SSR |
| `/pricing` | ✅ | Planos + checkout Stripe |
| `/app` | ✅ | Dashboard com atalhos aos módulos |
| `/app/quant` | ✅ | Estratégia quant (GEX, correlação, sinais) |
| `/app/profitchart` | ✅ | ProfitChart RTD + SignalR |
| `/app/wyckoff` | ✅ | Análise Wyckoff |
| `/app/macro` | ✅ | Contexto macro (VIX, risk mode) |
| `/app/settings` | ✅ | Conta, tenant, assinatura |
| `/app/scalping` | stub | Scalping panel |
| `/app/grid` | stub | Grid manager |
| `/app/positions` | stub | Positions |
| `/app/risk` | stub | Risk management |
| `/app/signals` | stub | Sinais |
| `/app/trades` | stub | Trades |

## Layouts

- `MainLayout.razor` — páginas públicas
- `AppLayout.razor` — sidebar dashboard (dark theme, health API)

## API clients (`Services/`)

- `QuantStrategyApiClient` — `/api/quantstrategy/*`
- `ProfitChartApiClient` — `/api/profitchart/*`
- `AnalysisApiClient` — `/api/analysis/*`
- `ProfitChartHubService` — SignalR `/hubs/profitchart`
- `BillingApiClient`, `AuthApiClient`, `HealthApiClient`

## Design system

- `wwwroot/css/design-system.css` — tokens base
- `wwwroot/css/app-pages.css` — layout das telas `/app/*`

HttpClient `NtBotApi` — base URL via `API_BASE_URL` ou `appsettings.Development.json`.

## Migração React → Blazor (Fase 6)

**Concluído (~55%):** Quant, ProfitChart, Wyckoff, Macro, Settings, dashboard.

**Pendente:** Grid, Scalping, Positions, Risk, Signals, Trades + gráficos avançados.

Referência UI funcional: [react-dashboard.md](react-dashboard.md)

## Deploy

Container: `docker/Dockerfile.Web` — porta 8080 interna.
