# NTBot Architecture

## Camadas

| Camada | Projetos | Responsabilidade |
|--------|----------|------------------|
| Domain | `NtBot.Domain` | Entidades, enums |
| Application | `NtBot.Application` | CQRS (MediatR) |
| Infrastructure | `NtBot.Infrastructure` | EF Core, PostgreSQL |
| Intelligence | `NtBot.Macro`, `NtBot.MarketIntelligence`, `NtBot.MarketDrivers`, `NtBot.TradingIntelligence` | Engines de análise |
| API | `NtBot.Api` | REST, SignalR, adapters |
| Web | `NtBot.Web` | Blazor Interactive Server |

## Princípios

- **Clean Architecture** — dependências apontam para dentro (Domain no centro)
- **Engines** — toda regra de negócio em engines (`ConfluenceEngine`, `MacroEngine`, etc.)
- **Component Driven UI** — Blazor consome apenas componentes e API clients
- **Sem duplicação** — Trading Intelligence compõe Macro + Market + Drivers + Wyckoff

## SignalR

Hubs por módulo com padrão `I*UpdateNotifier` → `*SignalRNotifier` → Blazor `*HubService`.

## Persistência

PostgreSQL via `NtBotDbContext`. Configurações dinâmicas em tabelas (`MacroProviders`, `DriverCompositions`).
