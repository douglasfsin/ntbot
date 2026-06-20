# Banco de dados

## PostgreSQL

| Ambiente | Host | Porta | Database | Usuário |
|----------|------|-------|----------|---------|
| Development | `46.225.161.55` | `5435` | `ntquant` | `postgres` |
| Production | `q96lrxulc7eu01u8ln9tmszq` | `5432` | `ntquant` | `postgres` |

Mesma infraestrutura do [BarberAI](C:\Projetos\barberai); apenas o nome do banco difere (`barberai_db` → **`ntquant`**).

Configuração: `src/NtBot.Api/appsettings.Development.json` e `appsettings.Production.json`.

## DbContext

- Classe: `NtBot.Infrastructure.Persistence.NtBotDbContext`
- Migrations: `src/NtBot.Infrastructure/Migrations/`
- Design-time: `NtBotDbContextFactory` lê appsettings de `NtBot.Api`

## Entidades principais

### Trading / core
`Tenant`, `User`, `AssetConfiguration`, `TradingSignal`, `Trade`, `Candle`, `EconomicEvent`, `NewsAnalysis`, `TradePosition`, `TradeExecution`, `OrderBook`, `OrderBookLevel`, `TickData`, `RiskConfig`, `GridOrder`, `GridLevel`, `StrategySignal`, `DailyResult`, `AccountInfo`, `TradingSession`

### Billing (Fase 3)
`Plan`, `Subscription`, `BillingHistory`, `WebhookEvent`

### Relacionamentos billing
- `Tenant` 1:1 `Subscription`
- `Plan` 1:N `Subscription`
- `Subscription` 1:N `BillingHistory`

## Seed data (dev)

Planos: **Free**, **Trader Pro**, **Enterprise**  
Tenant de teste: `11111111-1111-1111-1111-111111111111`  
User admin: `admin@ntbot.com` (hash placeholder — substituir na Fase 4)

## Migrations

Ver [development/migrations.md](../development/migrations.md).

## Próximos passos (Fase 3)

- [ ] Global query filter `TenantId` em entidades `ITenantEntity`
- [ ] Índices compostos para queries de dashboard
- [ ] Soft delete onde aplicável
