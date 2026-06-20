# Integrações — visão geral

| Integração | Pasta / código | Protocolo | Status |
|------------|----------------|-----------|--------|
| **ProfitChart RTD** | `src/NtBot.Api/Services/Profit/` | COM RTD + SignalR | Funcional (Windows + DLL) |
| **NinjaTrader 8** | `Services/NinjaTrader/`, `NinjaScript/` | REST + WebSocket | Parcial |
| **MetaTrader 5** | `MT5/`, `MT5Controller` | HTTP REST | EA + API |
| **PostgreSQL** | `NtBot.Infrastructure` | Npgsql | Ativo (`ntquant`) |
| **Stripe** | `NtBot.Billing` (stub) | Webhooks | Fase 5 |
| **Redis** | config em appsettings | Cache | Planejado |
| **Market data** | `NtBot.MarketData` (stub) | APIs externas | Fase 7 |

## Diagrama

```
                    ┌─────────────┐
                    │  NtBot.Api  │
                    └──────┬──────┘
         ┌─────────────────┼─────────────────┐
         ▼                 ▼                 ▼
  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
  │ ProfitChart │  │ NinjaTrader │  │    MT5      │
  │  (COM RTD)  │  │  (AddOn)    │  │  (Expert)   │
  └─────────────┘  └─────────────┘  └─────────────┘
         │                 │                 │
         └─────────────────┴─────────────────┘
                           │
                    ┌──────▼──────┐
                    │  PostgreSQL │
                    │   ntquant   │
                    └─────────────┘
```

## Detalhes por integração

- [ProfitChart](profitchart.md)
- [NinjaTrader](ninjatrader.md)
- [MetaTrader 5](mt5.md)
- [Stripe](stripe.md)

## Simulador

Ferramenta CLI em `Simulador/` — não é broker; replay de CSV contra `/orders/next`.
