# NTBot — Status de Execução das Fases

**Atualizado:** 20 de junho de 2026

## Concluído nesta sessão

### Fase 0 — Segurança
- [x] Credenciais removidas de `NTBot.Api/appsettings.json`
- [x] Suporte a `appsettings.Local.json`, `DATABASE_URL`, `JWT_SECRET`
- [x] `.gitignore` raiz + `docker/.env.example`
- [x] `NTBot.Api/appsettings.Local.json.example`

### Fase 2 — Nova arquitetura (.NET 9)
- [x] Solution `src/NtBot.sln` com 14 projetos
- [x] `NtBot.Domain` — 21 entidades migradas
- [x] `NtBot.Infrastructure` — DbContext + migrations + DI
- [x] `NtBot.Application` — MediatR + FluentValidation + `GetHealthQuery`
- [x] `NtBot.Api` v3 — controllers, hubs, services (legado migrado)
- [x] `NtBot.Web` — Blazor Interactive Server + landing SSR + dashboard shell
- [x] `tests/NtBot.UnitTests` — 1 teste passando
- [x] `docker/docker-compose.yml` + Dockerfiles Api/Web
- [x] **Build:** `dotnet build src/NtBot.sln` — sucesso

### Fase 6 — Início migração frontend
- [x] Landing `/` e `/pricing` (SSR)
- [x] Layout dashboard dark theme + sidebar
- [x] `/app` dashboard consumindo `/api/health`
- [x] Stubs para demais rotas `/app/*`

### Fase 3 — PostgreSQL (parcial)
- [x] `appsettings.Development.json` — mesmo host/porta do BarberAI, banco `ntquant`
- [x] `appsettings.Production.json` — mesmo host/porta do BarberAI prod, banco `ntquant`
- [x] Entidades billing: `Plan`, `Subscription`, `BillingHistory`, `WebhookEvent`
- [x] Migration `AddBillingTables` aplicada no PostgreSQL `ntquant` (dev)
- [x] `NtBotDbContextFactory` lê appsettings do projeto Api (EF tools → Postgres)
- [x] Health check: `database: connected`

## Próximas fases

| Fase | Status |
|------|--------|
| 3 PostgreSQL (tenant filters, índices) | Em andamento |
| 4 Identity (port BarberAI) | Pendente |
| 5 Stripe Billing | Pendente |
| 6 React → Blazor (telas completas) | Em andamento (~15%) |
| 7–14 | Pendente |

## Como executar

```powershell
# API v3 (Clean Architecture)
cd C:\Projetos\ntbot\src\NtBot.Api
# Development usa appsettings.Development.json → ntquant @ 46.225.161.55:5435
# Production usa appsettings.Production.json → ntquant @ host Coolify
dotnet run
# http://localhost:5053/swagger

# Migrations (Development → Postgres ntquant)
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet ef database update --project ..\NtBot.Infrastructure --startup-project .
```

# Web (Blazor)
cd C:\Projetos\ntbot\src\NtBot.Web
dotnet run
# http://localhost:5001

# Testes
dotnet test C:\Projetos\ntbot\tests\NtBot.UnitTests
```

## Notas

- **Legado:** `NTBot.Api/` na raiz permanece; use `src/NtBot.Api` como target.
- **React:** `ntbot-dashboard/` ainda ativo até Fase 6 completar.
- **BarberAI:** `C:\Projetos\barberai` — fonte para Fases 4–5.
