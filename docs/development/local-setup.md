# Setup local

## Connection strings

### Development (padrão)

Arquivo: `src/NtBot.Api/appsettings.Development.json`

```
Host=46.225.161.55;Port=5435;Database=ntquant;Username=postgres;Password=...
```

### Overrides locais

1. Copie `appsettings.Local.json.example` → `appsettings.Local.json`
2. Ou exporte variáveis:

```powershell
$env:DATABASE_URL = "Host=...;Port=5435;Database=ntquant;Username=postgres;Password=..."
$env:JWT_SECRET = "sua-chave-minimo-32-caracteres"
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

### Production

`appsettings.Production.json` — host Coolify interno. Nunca commitar secrets reais em branches públicas; preferir env vars no Coolify.

## Redis

Dev: `localhost:6379` (opcional). Compose local sobe Redis em `docker/docker-compose.yml`.

## JWT

`appsettings.json` → seção `Jwt`. Em produção use `JWT_SECRET` via ambiente.

## Stripe (dev)

Chaves de teste em `appsettings.Development.json` (mesmas do BarberAI dev). Webhook local: Stripe CLI ou ngrok.

## ProfitChart COM

1. Instale ProfitChart Pro
2. Coloque `Interop.RTDTrading.dll` em `src/NtBot.Api/Libs/`
3. Configure `rtd_config.json` na raiz do projeto Api

Sem a DLL, a API inicia normalmente; RTD fica desabilitado.

## CORS

Origens permitidas em `Program.cs`: `localhost:3000`, `5173`, `5001`.

## Logs

Serilog → console + `src/NtBot.Api/logs/ntbot-*.txt`

## Portas

| Serviço | Porta |
|---------|-------|
| NtBot.Api | 5053 |
| NtBot.Web | 5001 |
| ntbot-dashboard (Vite) | 5173 |
| Postgres (compose local) | 5432 |
| Redis (compose local) | 6379 |
