# MetaTrader 5

## Arquivos

```
MT5/
├── Experts/TradeAssistant.mq5   # Expert Advisor
└── Include/NTBot.mqh          # Cliente HTTP
```

## API backend

`src/NtBot.Api/Controllers/MT5Controller.cs` — rota `/api/mt5`

## Fluxo

1. EA no MT5 consulta sinais/ordens via HTTP
2. Api responde com próxima ação
3. EA executa no terminal MT5

## Configuração EA

Apontar URL base: `http://localhost:5053`

## Simulador alternativo

`Simulador/` usa mesma rota `/orders/next` para replay CSV sem MT5.

## Status

Bridge funcional para desenvolvimento; produção requer VPS com MT5 + Api acessível.
