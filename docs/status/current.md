# Status atual

**Atualizado:** 20 de junho de 2026

## Concluído

### Fase 0 — Segurança
- Credenciais removidas do repositório; uso de `appsettings.Local.json` e env vars
- `.gitignore` raiz + `docker/.env.example`

### Fase 2 — Arquitetura .NET 9
- Solution `src/NtBot.sln` (14 projetos)
- Clean Architecture: Domain, Application (MediatR), Infrastructure, Api, Web
- Código de trading migrado do monólito legado (removido)
- Docker: `docker/docker-compose.yml`, `Dockerfile.Api`, `Dockerfile.Web`
- Build e testes unitários básicos passando

### Fase 3 — PostgreSQL (parcial)
- Connection strings alinhadas ao BarberAI, banco **`ntquant`**
- Entidades billing: `Plan`, `Subscription`, `BillingHistory`, `WebhookEvent`
- Migration `AddBillingTables` aplicada em dev
- Health check com `database: connected`

### Fase 6 — Blazor (início ~15%)
- Landing `/`, `/pricing`, layout dashboard dark theme
- `/app` consome `/api/health`; stubs para rotas `/app/*`

### Limpeza
- Monólito `NTBot.Api/` (raiz) e `NTBot.sln` **removidos**
- Documentação consolidada em `docs/`

## Em andamento / pendente

| Fase | Descrição | Status |
|------|-----------|--------|
| 3 | Global query filters por tenant, índices | Pendente |
| 4 | Identity (port BarberAI → `NtBot.Identity`) | Pendente |
| 5 | Stripe (`NtBot.Billing`) | Pendente |
| 6 | Migração React → Blazor (telas completas) | ~15% |
| 7–14 | Connectors, observability, Coolify CI/CD | Pendente |

## O que usar hoje

| Componente | Caminho | Notas |
|------------|---------|-------|
| **API** | `src/NtBot.Api` | Única API backend |
| **Web** | `src/NtBot.Web` | Frontend alvo |
| **Dashboard React** | `ntbot-dashboard/` | Ativo até paridade Blazor |
| **DB** | PostgreSQL `ntquant` | Dev: `46.225.161.55:5435` |
| **Referência SaaS** | `C:\Projetos\barberai` | Auth + Stripe + deploy |

## Capacidades funcionais na API v3

- Wyckoff, Macro, Quant (GEX), ProfitChart RTD, GridEngine
- 8 controllers REST + 6 SignalR hubs
- Multi-tenant (modelo); auth endpoints ainda não portados
- JWT configurado; login/registro pendente (Fase 4)
