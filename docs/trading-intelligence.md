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
- **Gráfico SMC** (Fase 1 + 2): candlestick Lightweight Charts, tabs 5/15/30/60 min, overlays SMC + zonas operacionais
- Configurações: `/app/settings` → Trading Intelligence → Market Drivers

### Gráfico — fases

| Fase | Entregável | Status |
|------|------------|--------|
| **1** | API `/candles`, candlestick básico | ✅ |
| **2** | Overlays SMC (`/smc-overlays`), zonas no chart | ✅ |
| **5** | TradingView widget avançado, multi-ativo | ⏳ roadmap |

## Explicabilidade

Todo score inclui `Explanation`, `PositiveFactors`, `NegativeFactors` e contribuição ponderada por engine.
