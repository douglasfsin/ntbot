# ProfitChart RTD

Integração com ProfitChart Pro via COM RTD (mercado brasileiro).

## Código

| Arquivo | Função |
|---------|--------|
| `src/NtBot.Api/Services/Profit/ProfitService.cs` | Serviço RTD |
| `src/NtBot.Api/Services/Profit/IRtdService.cs` | Interface |
| `src/NtBot.Api/Hubs/ProfitChartHub.cs` | SignalR |
| `src/NtBot.Api/Controllers/ProfitChartController.cs` | REST |
| `src/NtBot.Api/rtd_config.json` | Configuração RTD |
| `src/NtBot.Api/Libs/Interop.RTDTrading.dll` | COM interop (**não versionado**) |

## Setup

1. ProfitChart Pro instalado (Windows)
2. Copiar `Interop.RTDTrading.dll` para `src/NtBot.Api/Libs/`
3. Ajustar `rtd_config.json` (símbolos, servidor RTD)
4. Iniciar Api — log: `ProfitChart RTD Service initialized`

Sem DLL ou ProfitChart: API inicia; RTD skipped.

## API

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/api/profitchart/health` | Status conexão RTD |
| POST | `/api/profitchart/subscribe` | Inscrever símbolos |

## SignalR

Hub: `/hubs/profitchart`  
Eventos de quote em tempo real para dashboard React/Blazor.

## UI

- React: `ntbot-dashboard/src/pages/ProfitChart.tsx`
- Blazor: stub em `/app/profitchart` (Fase 6)

## Troubleshooting

- **503 em `/api/profitchart/health`**: RTD desconectado ou sem dados recentes
- **COM error**: executar Api como mesmo usuário Windows que ProfitChart
- Ver logs em `src/NtBot.Api/logs/`

Documentação histórica: `docs/archive/legacy/PROFITCHART_*.md`
