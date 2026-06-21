# Trading Intelligence

Módulo `NtBot.TradingIntelligence` — workspace unificado comparável a Bloomberg/Koyfin.

## Componentes

- **ConfluenceEngine** — consolida scores de 9 engines com pesos configuráveis
- **OperationalZoneEngine** — zonas compradoras/vendedoras por interseção de timeframes
- **TradingIntelligenceService** — orquestra Macro, Market, Drivers, Wyckoff, SMC, Volume
- **DriverComposition** — configuração dinâmica de drivers por ativo (PostgreSQL)

## API

- `GET /api/trading-intelligence/{symbol}` — snapshot completo
- `GET/POST/PUT/DELETE /api/driver-compositions/*` — CRUD de composição

## SignalR

- Hub: `/hubs/trading-intelligence`
- Evento: `TradingIntelligenceSnapshotUpdated`

## UI

- Workspace: `/app/drivers`
- Configurações: `/app/settings` → Trading Intelligence → Market Drivers

## Explicabilidade

Todo score inclui `Explanation`, `PositiveFactors`, `NegativeFactors` e contribuição ponderada por engine.
