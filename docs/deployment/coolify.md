# Deploy — Coolify

Inventário resgatado do Coolify em produção (`http://46.225.161.55:8000`).

O MCP HTTP nativo do Coolify (`POST /mcp`) **não existe nesta instância**: o Laravel devolve CSRF 419 e o GET redireciona para `/login`. Apontar o Cursor Cloud Agent para `http://46.225.161.55:8000/mcp` quebra a descoberta de tools. Use o MCP **stdio** do repositório (REST `/api/v1`), não o endpoint `/mcp`.

## Serviços

| Recurso | Tipo | UUID | URL / host |
|---------|------|------|------------|
| **NTBot** (projeto) | project | `lbk5rfh2w9qe2ck0exs0l3eq` | — |
| **NTBot.Api** | application | `q9ekfmucjzkyn45i715lv0z2` | http://q9ekfmucjzkyn45i715lv0z2.46.225.161.55.sslip.io |
| **NTBot.Web** | application | `hnoe3x858fi0ikuex9ubwr60` | http://hnoe3x858fi0ikuex9ubwr60.46.225.161.55.sslip.io |
| **SigNoz** | service | `eva3s2kbg9a48onb3ws2hvgd` | http://signoz-eva3s2kbg9a48onb3ws2hvgd.46.225.161.55.sslip.io |
| **Postgres `ntquant`** | database | `q96lrxulc7eu01u8ln9tmszq` | host interno Coolify, porta `5432` |
| Deploy key | private key | `ebtwu3tkliyc2bshsipdwp6h` (`quant`) | GitHub `douglasfsin/ntbot` |

Checagem ao vivo (2026-08-17):

- `GET /api/health` na Api → `healthy` / `database: connected`
- Web → HTTP 200
- SigNoz → `v0.97.1`, `/api/v1/health` → `ok`
- Coolify API `/api/v1/version` → exige Bearer token

## Apps NtBot

| App Coolify | Dockerfile | Porta | Health |
|-------------|------------|-------|--------|
| **NTBot.Api** | `/docker/Dockerfile.Api` | 8080 | `/api/health` |
| **NTBot.Web** | `/docker/Dockerfile.Web` | 8080 | `/` |

- **Repositório:** `git@github.com:douglasfsin/ntbot.git`
- **Branch:** `cursor/signoz-otel-observability-5880` (apontado via API para testar OTEL; volte para `main` depois do merge)
- **Token de API:** precisa de **write + deploy**. Sem `read`, listagens (`/applications`, `/deployments`) devolvem 403 — POST env e GET `/deploy` ainda funcionam.
- **PATCH `git_branch`:** sempre enviar `is_preserve_repository_enabled: true` para não apagar o repositório Git.
- **Base directory:** `/` (raiz)
- **Build pack:** Dockerfile

## Variáveis — NTBot.Api

| Variável | Valor |
|----------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Postgres `ntquant` (host interno Coolify `q96lrxulc7eu01u8ln9tmszq`) |
| `JWT_SECRET` | chave 32+ chars |
| `Stripe__SecretKey` | configurado (test mode) |
| `Stripe__PublishableKey` | configurado (test mode) |
| `Stripe__WebhookSecret` | configurado — endpoint `/api/webhooks/stripe` |
| `Stripe__BackUrl` | URL pública do NTBot.Web |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP HTTP do collector SigNoz (rede Docker, porta **4318**) |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` |
| `OTEL_RESOURCE_ATTRIBUTES` | `service.namespace=NtBot,project=NtBot,deployment.environment=Production` |
| `OTEL_SERVICE_NAME` | `ntbot-api` |

Self-hosted SigNoz **não** usa `signoz-ingestion-key`. O collector (`otel-collector` no compose `eva3s2kbg9a48onb3ws2hvgd`) escuta `0.0.0.0:4318`. Portas 4317/4318 não estão publicadas de forma utilizável na internet (TCP reset); as apps no mesmo servidor Coolify devem usar o hostname interno do serviço, por exemplo `http://<uuid>-otel-collector:4318`. Confirme com `scripts/coolify/inspect_and_sync.py`.

## Variáveis — NTBot.Web

| Variável | Valor |
|----------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `API_BASE_URL` | `http://q9ekfmucjzkyn45i715lv0z2.46.225.161.55.sslip.io` |
| `OTEL_SERVICE_NAME` | `ntbot-web` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | mesmo collector OTLP do SigNoz |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` |
| `OTEL_RESOURCE_ATTRIBUTES` | `service.namespace=NtBot,project=NtBot,deployment.environment=Production` |

## API Coolify

```bash
export COOLIFY_BASE_URL=http://46.225.161.55:8000
export COOLIFY_ACCESS_TOKEN=...   # Settings → Keys & Tokens no Coolify

python3 scripts/coolify/inspect_and_sync.py
python3 scripts/coolify/inspect_and_sync.py --sync-otel --deploy --force --git-branch cursor/signoz-otel-observability-5880
```

```powershell
$token = "Bearer ..."
$base = "http://46.225.161.55:8000/api/v1"
# GET  $base/applications
# GET  $base/services
# POST $base/applications/{uuid}/envs
# GET  $base/deploy?uuid={uuid}
```

## MCP Coolify (Cursor)

Configuração no repositório: [`.cursor/mcp.json`](../../.cursor/mcp.json). O servidor é `python3 scripts/coolify/mcp_server.py` (MCP stdio → Coolify REST).

### Cloud Agents (obrigatório)

O MCP `coolify` deste workspace **não pode** ser HTTP `http://46.225.161.55:8000/mcp`. No dropdown MCP de [cursor.com/agents](https://cursor.com/agents):

1. Remova ou desative a entrada HTTP cujo URL termina em `/mcp`.
2. Recrie `coolify` como **stdio**:
   - Command: `python3`
   - Args: `${workspaceFolder}/scripts/coolify/mcp_server.py`
   - Env: `COOLIFY_BASE_URL=http://46.225.161.55:8000`
   - Env: `COOLIFY_ACCESS_TOKEN=${env:COOLIFY_ACCESS_TOKEN}`
3. Em [Cloud Agents → Secrets](https://cursor.com/dashboard/cloud-agents) adicione o secret `COOLIFY_ACCESS_TOKEN` (Coolify → Keys & Tokens, token de API da team).
4. Abra um **novo** cloud agent depois de gravar o MCP — a sessão atual não recarrega um servidor que já falhou na descoberta.

`tools/list` funciona sem o token (a descoberta não cai). Chamadas à API devolvem erro claro até o secret existir.

### Desktop

O mesmo `.cursor/mcp.json` vale no Cursor local. Alternativa npm (também stdio, **não** HTTP `/mcp`):

```json
{
  "mcpServers": {
    "coolify": {
      "command": "npx",
      "args": ["-y", "@masonator/coolify-mcp"],
      "env": {
        "COOLIFY_BASE_URL": "http://46.225.161.55:8000",
        "COOLIFY_ACCESS_TOKEN": "<token>"
      }
    }
  }
}
```

Só ative o MCP nativo `POST /mcp` depois de atualizar o Coolify e ligar **Settings → Advanced → MCP**. Nesta versão o toggle não está disponível.

### Smoke test

```bash
python3 scripts/coolify/mcp_server.py --self-test
python3 -m unittest tests.coolify.test_mcp_server
```

## Notas

- Migrations rodam no startup da Api (`Program.cs`)
- ProfitChart RTD **não** funciona em Linux container
- `appsettings.Production.json` não vai para o Git — use env vars no Coolify
