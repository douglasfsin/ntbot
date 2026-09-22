# Market Data Bus

Implementação: `MarketDataBus` em `src/NtBot.Connector.Windows/MarketData/`.

- `System.Threading.Channels` bounded (capacidade configurável)
- Modo `DropOldest` sob pressão
- Single reader (`MarketDataBatchPublisherWorker`)
- Multi writer (providers)

## Métricas

- `PublishedCount` — ticks publicados
- `DroppedCount` — ticks descartados (fila cheia)
- `QueuedCount` — profundidade atual

## Configuração

`appsettings.json` → `MarketDataGateway:ChannelCapacity` (padrão 8192).
