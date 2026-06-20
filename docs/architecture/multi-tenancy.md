# Multi-tenancy

## Modelo

Cada **Tenant** representa um cliente SaaS (conta de trading).

```
Tenant
├── Users (ADMIN, TRADER, VIEWER)
├── AssetConfigurations (por símbolo: MNQ, ES, ...)
├── TradingSignals / Trades
├── Subscription (billing)
└── Limites: MaxActivePositions, MaxDailyTrades, MaxRiskPerTrade, ...
```

## Plano (duas representações)

1. **Enum legado** — `Tenant.Plan` (`SubscriptionPlan`: FREE, PRO, ENTERPRISE)
2. **Entidade billing** — `Plan` + `Subscription` (Stripe, períodos, status)

Na Fase 5, `Subscription` será a fonte de verdade; o enum pode ser derivado do slug do plano.

## Isolamento de dados

**Estado atual:** entidades têm `TenantId`; filtros globais EF **ainda não aplicados**.

**Meta (Fase 3):**

```csharp
// NtBotDbContext — exemplo futuro
modelBuilder.Entity<TradingSignal>()
    .HasQueryFilter(e => e.TenantId == _tenantContext.CurrentTenantId);
```

## Limites por plano

| Plano | MaxActivePositions | MaxStrategies | MaxBrokers |
|-------|-------------------|---------------|------------|
| Free | 1 | 1 | 1 |
| Pro | 3 | 5 | 2 |
| Enterprise | 20 | 50 | 10 |

Valores em seed `Plans`; enforcement no `RiskManager` / middleware (pendente).

## Stripe

`Tenant.StripeCustomerId` liga ao customer Stripe.  
`Subscription.StripeSubscriptionId` / `StripeCheckoutSessionId` para ciclo de vida.

## Implementação Identity (Fase 4)

Portar de BarberAI:

- Registro cria `Tenant` + `User` admin
- Cookie auth com claim `TenantId`
- OTP e-mail para login/registro
