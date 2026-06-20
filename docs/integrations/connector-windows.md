# NtBot Connector Windows

O **NtBot.Connector.Windows** é o único projeto responsável por integrações Windows (Profit, MT5, NinjaTrader, TradingView). A API e o Blazor consomem apenas modelos normalizados de `NtBot.Shared.Normalized`.

## Arquitetura

```
NtBot.Connector.Windows/
├── Core/              # IMarketDataProvider, ITradingProvider, IBrokerPlugin
├── Providers/         # Plugins internos (Profit, MT5, Ninja, TradingView)
├── Services/          # Delta aggregator, orchestrator
├── Workers/           # Ingest contínuo
├── SignalR/           # Hub client + HTTP ingest
├── Updater/           # Auto-update via API
└── Configuration/     # appsettings + rtd_config.json
```

## API (módulo NtBot.Connector)

| Endpoint | Auth | Descrição |
|----------|------|-----------|
| `GET /api/connector/status` | JWT | Status para UI Blazor |
| `POST /api/connector/keys` | JWT | Gera ApiKey `ntbot_live_…` |
| `POST /api/connector/keys/{id}/rotate` | JWT | Rotação |
| `DELETE /api/connector/keys/{id}` | JWT | Revogação |
| `POST /api/connector/session` | ApiKey | Inicia sessão |
| `POST /api/connector/ingest` | ApiKey | Recebe batch normalizado |
| `GET /api/connector/version` | ApiKey | Check de update |
| `GET /api/connector/download/{version}` | ApiKey | Download protegido |

SignalR: `/hubs/connector?apiKey=…`

## Tabelas

- `ConnectorKeys` — hash BCrypt, prefixo, expiração, último uso/IP
- `ConnectorVersions` — releases publicadas
- `ConnectorSessions` — heartbeat e versão conectada
- `ConnectorLogs` — logs do connector
- `ConnectorDownloads` — auditoria de downloads

## Configuração local

1. Gere a Api Key em **Configurações → Connector Windows** no dashboard Blazor.
2. Configure `appsettings.json` do connector:

```json
{
  "Connector": {
    "ApiBaseUrl": "http://localhost:5053",
    "ApiKey": "ntbot_live_…"
  }
}
```

3. Execute: `dotnet run --project src/NtBot.Connector.Windows`

## Modelos normalizados

Todos os plugins convertem para: `NormalizedMarketTick`, `NormalizedOrder`, `NormalizedPosition`, `NormalizedExecution`, `NormalizedSignal`, `NormalizedAccount`, `NormalizedBrokerStatus`.

Nenhum consumer (API/Web) deve referenciar detalhes de broker específico.
