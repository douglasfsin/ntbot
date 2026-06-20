# Roadmap — Fases de implementação

Plano completo de migração para SaaS enterprise. Status detalhado: [status/current.md](../status/current.md).

| Fase | Nome | Entregáveis | Status |
|------|------|-------------|--------|
| **0** | Segurança | Secrets fora do repo, .env.example, Local.json | ✅ |
| **1** | Análise | Docs de arquitetura (arquivados em `archive/legacy`) | ✅ |
| **2** | Arquitetura .NET 9 | `src/NtBot.sln`, Domain, Infra, Api, Web | ✅ |
| **3** | PostgreSQL | `ntquant`, billing tables, query filters | 🔄 parcial |
| **4** | Identity | Port BarberAI → `NtBot.Identity`, login/registro | ⏳ |
| **5** | Stripe | `NtBot.Billing`, checkout, webhooks | ⏳ |
| **6** | React → Blazor | Telas completas em `NtBot.Web` | 🔄 ~15% |
| **7** | Market data | `NtBot.MarketData`, TradingView | ⏳ |
| **8** | Connectors | Extrair trading para `NtBot.Trading` | ⏳ |
| **9** | SignalR wire-up | Todos hubs conectados na UI | ⏳ |
| **10** | Worker | Jobs background, calendário econômico | ⏳ |
| **11** | Analytics | News AI, sentiment | ⏳ |
| **12** | Observability | Logs, métricas, alertas | ⏳ |
| **13** | Coolify | Deploy prod completo | ⏳ |
| **14** | CI/CD | GitHub Actions, testes integração | ⏳ |

## Estratégia

- **Strangler fig:** Api v3 substituiu monólito (removido)
- **BarberAI como template:** Identity + Billing + Deploy
- **UI:** React mantido em paralelo até Blazor pronto

## Próxima implementação recomendada

1. Completar Fase 3 — global query filters tenant
2. Fase 4 — Identity (desbloqueia auth em produção)
3. Fase 5 — Stripe (monetização)
4. Fase 6 — ProfitChart + Quant no Blazor

## Documentação histórica

Plano original detalhado: `docs/archive/legacy/MIGRATION_PLAN.md`
