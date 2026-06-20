# NTBot — Documentação

Plataforma SaaS de trading automatizado (.NET 9, Clean Architecture, PostgreSQL, Blazor + React).

**Última atualização:** 20 de junho de 2026

---

## Comece aqui

| Documento | Para quem | Conteúdo |
|-----------|-----------|----------|
| [Getting Started](getting-started.md) | Novo no projeto | Pré-requisitos, primeiro run, URLs |
| [Status atual](status/current.md) | PM / dev | O que está pronto e o que falta |
| [Roadmap](roadmap/phases.md) | Planejamento | Fases 0–14 de implementação |

---

## Arquitetura

| Documento | Conteúdo |
|-----------|----------|
| [Visão geral](architecture/overview.md) | Camadas, fluxo de dados, diagrama |
| [Estrutura de projetos](architecture/project-structure.md) | Cada projeto em `src/` e responsabilidade |
| [Banco de dados](architecture/database.md) | PostgreSQL `ntquant`, entidades, migrations |
| [Multi-tenancy](architecture/multi-tenancy.md) | Tenant, isolamento, limites por plano |

---

## Desenvolvimento

| Documento | Conteúdo |
|-----------|----------|
| [Setup local](development/local-setup.md) | Connection strings, env vars, secrets |
| [Migrations EF](development/migrations.md) | Comandos `dotnet ef`, design-time factory |
| [Testes](development/testing.md) | Unit tests, Simulador, backtest CSV |
| [Guia: nova feature](development/adding-features.md) | Onde colocar código (Domain → Api) |

---

## API e integrações

| Documento | Conteúdo |
|-----------|----------|
| [REST + SignalR](api/rest-and-signalr.md) | Endpoints, hubs, Swagger |
| [Auth](api/auth.md) | Login, registro OTP, JWT |
| [Integrações — visão geral](integrations/overview.md) | Mapa de brokers e serviços externos |
| [ProfitChart RTD](integrations/profitchart.md) | COM RTD, hub, config |
| [NinjaTrader](integrations/ninjatrader.md) | AddOn, REST/WebSocket |
| [MetaTrader 5](integrations/mt5.md) | Expert Advisor |
| [Stripe / Billing](integrations/stripe.md) | Planos, webhooks (Fase 5) |

---

## Frontend

| Documento | Conteúdo |
|-----------|----------|
| [Blazor Web (`NtBot.Web`)](frontend/blazor-web.md) | App principal em migração |
| [React Dashboard (legado UI)](frontend/react-dashboard.md) | `ntbot-dashboard/` até paridade Blazor |

---

## Deploy

| Documento | Conteúdo |
|-----------|----------|
| [Docker](deployment/docker.md) | Compose local, Dockerfiles |
| [Coolify / Produção](deployment/coolify.md) | Postgres prod, variáveis, serviços |

---

## Referências externas

- **BarberAI** (`C:\Projetos\barberai`) — padrão de Identity, Stripe, deploy Coolify
- **Documentação arquivada** — `docs/archive/legacy/` (guias antigos do monólito .NET 8)

---

## Estrutura do repositório (resumo)

```
ntbot/
├── src/NtBot.sln          ← Solution principal (.NET 9)
├── src/NtBot.Api/         ← API REST + SignalR (porta 5053)
├── src/NtBot.Web/         ← Blazor Interactive Server (porta 5001)
├── src/NtBot.Domain/      ← Entidades
├── src/NtBot.Infrastructure/  ← EF Core + migrations
├── tests/NtBot.UnitTests/
├── ntbot-dashboard/       ← React (transição → Blazor)
├── Simulador/             ← Backtest CSV → API
├── MT5/                   ← Expert Advisor
├── NinjaScript/           ← AddOn NinjaTrader 8
├── docker/                ← Compose + Dockerfiles
└── docs/                  ← Esta documentação
```
