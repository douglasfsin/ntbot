# NinjaTrader 8

## Backend

`src/NtBot.Api/Services/NinjaTrader/NinjaTraderService.cs`

Config em `appsettings.json`:

```json
"NinjaTrader": {
  "ApiBaseUrl": "http://localhost:8080",
  "WebSocketUrl": "ws://localhost:8080/ws",
  "DefaultAccount": "Sim101"
}
```

## AddOn (cliente NT8)

Pasta: `NinjaScript/NTBotHttpServer.cs`

Expõe HTTP server dentro do NinjaTrader para bridge com NTBot.Api.

## Fluxo

1. NT8 AddOn recebe market data e ordens locais
2. NTBot.Api envia comandos via REST/WebSocket
3. Execuções retornam via `ExecutionHub`

## Setup rápido

1. Compilar/importar script em NinjaTrader 8
2. Configurar URL da Api (`http://localhost:5053`)
3. Conta sim `Sim101` para testes

Documentação histórica: `docs/archive/legacy/NINJATRADER_*.md`

## Status

Integração parcial — validar WebSocket em ambiente real NT8.
