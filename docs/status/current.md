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

### Fase 4 — Identity ✅
- Módulo `NtBot.Identity`: BCrypt, JWT, OTP, email (MailKit)
- Entidades: `OtpVerification`, `User.EmailConfirmed`
- Migration `AddIdentityTables` aplicada em dev
- API: `AuthController` (`/api/auth/*`)
- Web: páginas login/registro/recuperação + `JwtAuthStateProvider`
- Rotas `/app/*` protegidas com `[Authorize]`
- Documentação: `docs/api/auth.md`

### Fase 6 — Blazor (início ~20%)
- Landing `/`, `/pricing`, layout dashboard dark theme
- Auth UI completa; `/app` consome `/api/health`; stubs para rotas `/app/*`

### Deploy Coolify
- [x] Projeto **NTBot** no Coolify
- [x] Apps **NTBot.Api** + **NTBot.Web** (Dockerfile, porta 8080)
- [x] GitHub `douglasfsin/ntbot` sync via deploy key **quant**
- [x] Produção: ambos `running:healthy`
- [ ] Redeploy pós-Fase 4 (JWT + migration + SMTP env)

### Limpeza
- Monólito `NTBot.Api/` (raiz) e `NTBot.sln` **removidos**
- Documentação consolidada em `docs/`

## Em andamento / pendente

| Fase | Descrição | Status |
|------|-----------|--------|
| 3 | Global query filters por tenant, índices | Pendente |
| 4 | Persistência JWT no browser (localStorage), SignalR auth | Pendente |
| 5 | Stripe (`NtBot.Billing`) | Pendente |
| 6 | Migração React → Blazor (telas completas) | ~20% |
| 7–14 | Connectors, observability, Coolify CI/CD | Pendente |

## O que usar hoje

| Componente | Caminho | Notas |
|------------|---------|-------|
| **API** | `src/NtBot.Api` | Única API backend |
| **Web** | `src/NtBot.Web` | Frontend alvo + auth |
| **Dashboard React** | `ntbot-dashboard/` | Ativo até paridade Blazor |
| **DB** | PostgreSQL `ntquant` | Dev: `46.225.161.55:5435` |
| **Referência SaaS** | `C:\Projetos\barberai` | Auth + Stripe + deploy |

## Capacidades funcionais na API v3

- Wyckoff, Macro, Quant (GEX), ProfitChart RTD, GridEngine
- 8+ controllers REST + 6 SignalR hubs
- Multi-tenant (modelo) + JWT auth (login, registro OTP, reset senha)
- Seed admin (`admin@ntbot.com`) ainda com hash placeholder — use `/register` ou atualize hash BCrypt
