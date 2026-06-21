# Configuration

## TradingIntelligence (appsettings.json)

| Chave | Default | Descrição |
|-------|---------|-----------|
| DefaultRefreshSeconds | 60 | Ciclo SignalR |
| AiRefreshSeconds | 120 | AI Workspace |
| SupportedAssets | WIN, WDO, … | Ativos do workspace |
| ChartTimeframes | 5,15,30,60 | Regiões no gráfico |
| N8nWebhookUrl | "" | Webhook n8n (opcional) |

## MarketDrivers

| Chave | Descrição |
|-------|-----------|
| DashboardAssets | Ativos no dashboard |
| DefaultRefreshSeconds | Refresh drivers |

## Coolify

Configure `N8nWebhookUrl` nas env vars da **NTBot.Api** se usar IA externa.
