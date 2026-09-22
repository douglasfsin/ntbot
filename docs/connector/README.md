# NTBot Connector — Market Data Gateway

O **NtBot.Connector.Windows** é o gateway de market data entre plataformas (Profit, MT5, NinjaTrader) e a **NtBot.Api**.

## Fluxo

```
Provider (RTD / DDE / MT5 SSE)
    → MarketDataBus (Channel)
    → MarketTickNormalizer
    → MarketDataCache
    → ProviderOrchestrator (batch 50ms)
    → ConnectorIngestWorker → API SignalR
```

## Configuração

| Arquivo | Uso |
|---------|-----|
| `appsettings.json` → `MarketDataGateway` | Batch, fila, watchdog |
| `Configuration/profit_dde_config.json` | Ativos DDE Profit |
| `Configuration/rtd_config.json` | Ativos RTD Profit |
| `Configuration/mt5_config.json` | Símbolos MT5 |

## Habilitar DDE Profit

```json
"EnableProfit": true,
"EnableProfitDde": true
```

Com `EnableProfitDde=true`, ticks vêm do DDE; RTD continua disponível para posições/conta.

## Diagnostics

Menu da bandeja → **Market Data diagnostics** (equivalente local a `/connector/diagnostics`).

## Documentos

- [architecture.md](./architecture.md)
- [market-data-bus.md](./market-data-bus.md)
- [profit-dde-provider.md](./profit-dde-provider.md)
