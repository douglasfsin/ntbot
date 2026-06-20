# Estrutura de projetos

Solution: `src/NtBot.sln`

## Projetos ativos

| Projeto | Pasta | Responsabilidade |
|---------|-------|------------------|
| **NtBot.Domain** | `src/NtBot.Domain/` | Entidades EF, enums. Sem dependências externas. |
| **NtBot.Shared** | `src/NtBot.Shared/` | Constantes compartilhadas (`NtBotRoles`, `ITenantEntity`). |
| **NtBot.Application** | `src/NtBot.Application/` | MediatR handlers, FluentValidation, DI `AddApplication()`. |
| **NtBot.Infrastructure** | `src/NtBot.Infrastructure/` | `NtBotDbContext`, migrations, `AddInfrastructure()`. |
| **NtBot.Api** | `src/NtBot.Api/` | Host HTTP: controllers, hubs, services de trading legados. Porta **5053**. |
| **NtBot.Web** | `src/NtBot.Web/` | Blazor Interactive Server. Porta **5001**. |
| **NtBot.UnitTests** | `tests/NtBot.UnitTests/` | xUnit. |

## Projetos scaffold (implementar nas fases 4–7)

| Projeto | Fase | Conteúdo planejado |
|---------|------|-------------------|
| `NtBot.Identity` | 4 | Auth cookie/JWT, OTP, registro tenant |
| `NtBot.Billing` | 5 | Stripe checkout, webhooks, subscription sync |
| `NtBot.MarketData` | 7 | Providers (Polygon, Yahoo, TradingView) |
| `NtBot.Trading` | 7 | Extração de engines da Api |
| `NtBot.Notifications` | 8 | Email, push, in-app |
| `NtBot.Analytics` | 9 | AI prep (OpenAI, Ollama) |
| `NtBot.Worker` | 10 | Background jobs (Hangfire/Quartz) |

## NtBot.Api — layout interno

```
NtBot.Api/
├── Controllers/       # REST (Analysis, Grid, MarketData, MT5, Orders, ProfitChart, Quant, Tenants)
├── Hubs/              # SignalR (ProfitChart, Trading, Market, Execution, Risk, Notification)
├── Services/          # Wyckoff, Macro, Profit, NinjaTrader, GridEngine, RiskManager, ...
├── Strategies/        # ChochStrategy, QuantStrategy
├── Program.cs         # DI, migrations auto, Swagger, CORS
├── appsettings*.json
└── rtd_config.json    # ProfitChart RTD
```

## NtBot.Web — layout interno

```
NtBot.Web/
├── Components/
│   ├── Layout/        # MainLayout (público), AppLayout (dashboard)
│   └── Pages/         # Home, Pricing, App/*
├── wwwroot/css/       # design-system.css (dark theme)
└── Program.cs
```

## Fora de `src/` (mantidos)

| Pasta | Função |
|-------|--------|
| `Simulador/` | Replay CSV → `POST /orders/next` |
| `MT5/` | Expert Advisor + include `NTBot.mqh` |
| `NinjaScript/` | AddOn HTTP server NinjaTrader 8 |
| `ntbot-dashboard/` | UI React até migração Blazor |
| `docker/` | Compose e Dockerfiles |

## Dependências entre projetos

```
NtBot.Api → Application, Infrastructure, Domain, Shared
NtBot.Web → (standalone, HttpClient → Api)
NtBot.Infrastructure → Domain
NtBot.Application → Domain, Shared
NtBot.UnitTests → Application
```
