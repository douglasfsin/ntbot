# Deploy — Coolify

## Serviços

| App Coolify | Dockerfile | Porta | Health |
|-------------|------------|-------|--------|
| **NTBot.Api** | `/docker/Dockerfile.Api` | 8080 | `/api/health` |
| **NTBot.Web** | `/docker/Dockerfile.Web` | 8080 | `/` |

- **Repositório:** `git@github.com:douglasfsin/ntbot.git`
- **Branch (produção Nomos pré-merge):** `cursor/nomos-mfdash-tenant-5880` (PR [#5](https://github.com/douglasfsin/ntbot/pull/5); tip inclui home Nomos `9358920` + bootstrap `SponsorUserId`)
- **Branch default no Coolify após merge:** `main`
- **Private key Coolify:** `quant` (`ebtwu3tkliyc2bshsipdwp6h`)
- **Base directory:** `/` (raiz)
- **Build pack:** Dockerfile

## FQDN público (Web)

**Web FQDN canônico:** `https://mfdash.thinkautomated.com.br` — DNS/TLS ativo (mesmo padrão `barberai.thinkautomated.com.br`).

O sslip.io continua como fallback:

| App | FQDN / URL | UUID Coolify |
|-----|------------|--------------|
| **NTBot.Web** | **https://mfdash.thinkautomated.com.br** | `hnoe3x858fi0ikuex9ubwr60` |
| **NTBot.Web (fallback)** | http://hnoe3x858fi0ikuex9ubwr60.46.225.161.55.sslip.io | `hnoe3x858fi0ikuex9ubwr60` |
| **NTBot.Api** | http://q9ekfmucjzkyn45i715lv0z2.46.225.161.55.sslip.io | `q9ekfmucjzkyn45i715lv0z2` |

- Projeto Coolify: **NTBot** (`lbk5rfh2w9qe2ck0exs0l3eq`)
- Git: `git@github.com:douglasfsin/ntbot.git` (deploy key **quant**)

Login white-label Nomos: `https://mfdash.thinkautomated.com.br/nomos` (aliases `/nomos/login` e `/t/nomos/login`). Home pública `/` também é branded Nomos.

### Como apontar o FQDN no Coolify (REST)

Coolify MCP não é usado aqui. No dashboard: Application → NTBot.Web → **Domains** → `https://mfdash.thinkautomated.com.br`, **mantenha** o sslip.io como domínio extra se ainda precisar.

Via API (preservar `is_repository_enabled` / git no PATCH; token com **read + write + deploy**):

```bash
# GET atual (guarde is_repository_enabled / git fields)
curl -s -H "Authorization: Bearer $COOLIFY_ACCESS_TOKEN" \
  http://46.225.161.55:8000/api/v1/applications/hnoe3x858fi0ikuex9ubwr60

# PATCH FQDN — só com token que permita o campo fqdn; não desligue o repositório
curl -s -X PATCH -H "Authorization: Bearer $COOLIFY_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  http://46.225.161.55:8000/api/v1/applications/hnoe3x858fi0ikuex9ubwr60 \
  -d '{"fqdn":"https://mfdash.thinkautomated.com.br,http://hnoe3x858fi0ikuex9ubwr60.46.225.161.55.sslip.io"}'

# Deploy feature branch / commit (pré-merge PR #5)
for uuid in hnoe3x858fi0ikuex9ubwr60 q9ekfmucjzkyn45i715lv0z2; do
  curl -s -X PATCH -H "Authorization: Bearer $COOLIFY_ACCESS_TOKEN" \
    -H "Content-Type: application/json" \
    "http://46.225.161.55:8000/api/v1/applications/$uuid" \
    -d '{"git_branch":"cursor/nomos-mfdash-tenant-5880","git_commit_sha":"9731bf31ee6404b2a8271d98063971fa756ca61f"}'
  curl -s -H "Authorization: Bearer $COOLIFY_ACCESS_TOKEN" \
    "http://46.225.161.55:8000/api/v1/deploy?uuid=$uuid&force=true"
done
```

DNS: CNAME/A de `mfdash.thinkautomated.com.br` para o host Coolify (mesmo padrão `barberai.thinkautomated.com.br`). TLS via Traefik/Let's Encrypt no Coolify.

`Stripe__BackUrl` da Api deve apontar para `https://mfdash.thinkautomated.com.br`.

### Verificação pós-deploy

| Check | Esperado |
|-------|----------|
| `GET {Api}/api/health` | `database: connected` |
| `GET {Api}/api/tenants/branding/by-slug/nomos` | `appDisplayName: Nomos`, accent `#C8F542` |
| `https://mfdash.thinkautomated.com.br/` | título **Nomos — Norte financeiro** |
| `https://mfdash.thinkautomated.com.br/nomos` | **Login — Nomos** (não redirect `/not-found`) |

## Variáveis — NTBot.Api

| Variável | Valor |
|----------|-------|
| `PORT` | `8080` (padrão BarberAI / Coolify) |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` |
| `TZ` | `America/Sao_Paulo` |
| `CORS_ORIGINS` | `https://mfdash.thinkautomated.com.br` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Postgres `ntquant` (host interno Coolify) |
| `JWT_SECRET` | chave 32+ chars |
| `Stripe__SecretKey` | configurado (test mode) |
| `Stripe__PublishableKey` | configurado (test mode) |
| `Stripe__WebhookSecret` | configurado — endpoint `/api/webhooks/stripe` |
| `Stripe__BackUrl` | `https://mfdash.thinkautomated.com.br` |
| `NomosSeed__AdminPassword` | senha do admin Nomos (default temporário abaixo) |
| `NomosSeed__InfluencerPassword` | senha Douglas/Arthur + clientes demo |

## Variáveis — NTBot.Web

| Variável | Valor |
|----------|-------|
| `PORT` | `8080` |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` |
| `TZ` | `America/Sao_Paulo` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `API_BASE_URL` | URL pública da Api — **obrigatório** (ex. `http://q9ekfmucjzkyn45i715lv0z2.46.225.161.55.sslip.io`) |

## Tenant Nomos (seed runtime)

Idempotente no startup da Api (`NomosTenantSeeder`, depois de Migrate + bootstrap). **Não** usa `HasData`.

| Conta | Role | Email |
|-------|------|-------|
| Admin Nomos | ADMIN | `admin@somosnomos.com.br` |
| Douglas | ADVISOR (influencer) | `douglas@somosnomos.com.br` |
| Arthur | ADVISOR (influencer) | `arthur@somosnomos.com.br` |
| Demo Cliente Douglas | CLIENT (`SponsorUserId` = Douglas) | `demo.douglas@somosnomos.com.br` |
| Demo Cliente Arthur | CLIENT (`SponsorUserId` = Arthur) | `demo.arthur@somosnomos.com.br` |

Senha temporária (trocar no primeiro acesso): **`Nomos!ChangeMe2026`**

Override no Coolify (Api):

- `NomosSeed__AdminPassword`
- `NomosSeed__InfluencerPassword`

O seeder **não** regrava hash se o usuário já existe — senhas alteradas persistem.

Marca: logo [Verde Lima](https://somosnomos.com.br/wp-content/uploads/2025/06/Logo_VerdeLima_XP-negativa-1.png), site [somosnomos.com.br](https://www.somosnomos.com.br), suporte `atendimento@nomos.com.br`, accent `#C8F542`, fundo `#021E19` / `#000000`.

ADVISOR lista só CLIENT com `SponsorUserId` = o próprio id. ADMIN lista todos. Criar assinante: `POST /api/portfolio/clients`.

Health: `GET /api/health` → `database: connected`

```powershell
$token = "Bearer ..."
$base = "http://46.225.161.55:8000/api/v1"
# POST $base/applications/private-deploy-key
# POST $base/applications/{uuid}/envs
# GET  $base/applications/{uuid}/start
```

## Notas

- Migrations rodam no startup da Api (`Program.cs`); `SponsorUserId` também sobe via `WhiteLabelPortfolioSchemaBootstrap` (`ALTER TABLE ... IF NOT EXISTS`)
- ProfitChart RTD **não** funciona em Linux container
- `appsettings.Production.json` não vai para o Git — use env vars no Coolify
