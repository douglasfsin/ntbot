# SignalR — Trading Intelligence

## Hub

```
/hubs/trading-intelligence
```

## Eventos

| Evento | Payload |
|--------|---------|
| `TradingIntelligenceSnapshotUpdated` | `TradingIntelligenceSnapshot` |

## Refresh

`TradingIntelligenceRefreshWorker` — ciclo configurável (`DefaultRefreshSeconds`, default 60s).

## Cliente Blazor

`TradingIntelligenceHubService` — JWT, auto-reconnect, evento `SnapshotUpdated`.
