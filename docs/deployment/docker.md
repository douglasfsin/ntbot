# Docker

## Compose local

```powershell
cd C:\Projetos\ntbot\docker
copy .env.example .env
# Edite POSTGRES_PASSWORD, DATABASE_URL se necessário
docker compose up --build
```

## Serviços

| Serviço | Porta host | Imagem / build |
|---------|------------|----------------|
| `postgres` | 5432 | postgres:16-alpine |
| `redis` | 6379 | redis:7-alpine |
| `ntbot-api` | 5053 | `docker/Dockerfile.Api` |
| `ntbot-web` | 5001 | `docker/Dockerfile.Web` |

## Dockerfiles

- `docker/Dockerfile.Api` — multi-stage build de `src/NtBot.Api` + dependências
- `docker/Dockerfile.Web` — build de `src/NtBot.Web`

Context de build: raiz do repositório (`..` relativo a `docker/`).

## Variáveis (Api)

```
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Host=postgres;Database=ntbot;...
ApiSettings__BaseUrl=http://ntbot-api:8081  # Web → Api
```

## Postgres local vs remoto

- **Compose:** banco `ntbot` local no container
- **Dev direto (`dotnet run`):** Postgres remoto `ntquant` via `appsettings.Development.json`

Para alinhar compose ao `ntquant` remoto, sobrescreva `DATABASE_URL` no `.env`.

## ProfitChart

RTD COM **não roda em Linux container**. Api em Docker funciona sem RTD; use Api nativa Windows para ProfitChart.
