# Connector Architecture

## Camadas

| Camada | Responsabilidade |
|--------|------------------|
| **Providers** | Profit RTD, Profit DDE, MT5, NinjaTrader — leitura nativa |
| **MarketData** | Bus, cache, normalização, health |
| **Workers** | Batch publisher, watchdog, ingest, OHLCV |
| **Services** | ProviderOrchestrator, DeltaAggregator, API client |
| **UI** | Tray, status, diagnostics |

## Contratos

- `IMarketDataBus` / `IMarketDataPublisher` — publicação event-driven
- `IMarketDataCache` — snapshot em memória
- `IProviderHealth` / `IProviderMonitor` — métricas e diagnostics
- `IMarketDataProvider` (Core) — interface de broker para ticks

## Princípios

1. Nenhum consumer (Web, Quant, Macro) acessa Profit/MT5 diretamente.
2. Todos os ticks passam por `MarketTick` → `NormalizedMarketTick`.
3. Providers são intercambiáveis via configuração.
