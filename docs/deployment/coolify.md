# Deploy — Coolify

## Serviços

| App Coolify | Dockerfile | Porta | Health |
|-------------|------------|-------|--------|
| **NTBot.Api** | `/docker/Dockerfile.Api` | 8080 | `/api/health` |
| **NTBot.Web** | `/docker/Dockerfile.Web` | 8080 | `/` |

- **Repositório:** `git@github.com:douglasfsin/ntbot.git`
- **Branch:** `main`
- **Private key Coolify:** `quant` (`ebtwu3tkliyc2bshsipdwp6h`)
- **Base directory:** `/` (raiz)
- **Build pack:** Dockerfile

## Variáveis — NTBot.Api

| Variável | Valor |
|----------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Postgres `ntquant` (host interno Coolify) |
| `JWT_SECRET` | chave 32+ chars |
| `Stripe__SecretKey` | configurado (test mode) |
| `Stripe__PublishableKey` | configurado (test mode) |
| `Stripe__WebhookSecret` | configurado — endpoint `/api/webhooks/stripe` |
| `Stripe__BackUrl` | URL pública do NTBot.Web |

## Variáveis — NTBot.Web

| Variável | Valor |
|----------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `API_BASE_URL` | URL pública da Api — **obrigatório** (ex. `http://q9ekfmucjzkyn45i715lv0z2.46.225.161.55.sslip.io`) |

## Deploy Coolify (produção)

| App | URL | UUID Coolify |
|-----|-----|--------------|
| **NTBot.Api** | http://q9ekfmucjzkyn45i715lv0z2.46.225.161.55.sslip.io | `q9ekfmucjzkyn45i715lv0z2` |
| **NTBot.Web** | http://hnoe3x858fi0ikuex9ubwr60.46.225.161.55.sslip.io | `hnoe3x858fi0ikuex9ubwr60` |

- Projeto Coolify: **NTBot** (`lbk5rfh2w9qe2ck0exs0l3eq`)
- Git: `git@github.com:douglasfsin/ntbot.git` (branch `main`, deploy key **quant**)
- Status: `running:healthy` (jun/2026)

Health: `GET /api/health` → `database: connected`

```powershell
$token = "Bearer ..."
$base = "http://46.225.161.55:8000/api/v1"
# POST $base/applications/private-deploy-key
# POST $base/applications/{uuid}/envs
# GET  $base/applications/{uuid}/start
```

## Notas

- Migrations rodam no startup da Api (`Program.cs`)
- ProfitChart RTD **não** funciona em Linux container
- `appsettings.Production.json` não vai para o Git — use env vars no Coolify
