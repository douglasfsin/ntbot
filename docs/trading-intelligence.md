# Trading Intelligence

Módulo `NtBot.TradingIntelligence` — workspace unificado comparável a Bloomberg/Koyfin.

## Componentes

- **ConfluenceEngine** — consolida scores de 9 engines com pesos configuráveis
- **OperationalZoneEngine** — zonas compradoras/vendedoras por interseção de timeframes
- **TradingIntelligenceService** — orquestra Macro, Market, Drivers, Wyckoff, SMC, Volume
- **DriverComposition** — configuração dinâmica de drivers por ativo (PostgreSQL)

## API

- `GET /api/trading-intelligence/{symbol}` — snapshot completo
- `GET /api/trading-intelligence/dashboard` — resumo WIN/WDO/PETR4
- `GET /api/trading-intelligence/status` — Redis, n8n, webhooks
- `POST /api/trading-intelligence/refresh?symbol=WIN` — invalida cache e reconsulta n8n
- `GET/POST/PUT/DELETE /api/driver-compositions/*` — CRUD de composição

## SignalR

- Hub: `/hubs/trading-intelligence`
- Evento: `TradingIntelligenceSnapshotUpdated`

## UI

- Workspace: `/app/drivers`
- Configurações: `/app/settings` → Trading Intelligence → Market Drivers

## Explicabilidade

Todo score inclui `Explanation`, `PositiveFactors`, `NegativeFactors` e contribuição ponderada por engine.
