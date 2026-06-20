# REST API e SignalR

Base URL: `http://localhost:5053`  
Swagger: `http://localhost:5053/swagger`

## Health

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/api/health` | Status geral + DB connected |

## Controllers REST

| Controller | Rota base | Função |
|------------|-----------|--------|
| `AnalysisController` | `/api/analysis` | Wyckoff, macro |
| `TenantsController` | `/api/tenants` | CRUD tenants |
| `MarketDataController` | `/api/marketdata` | Candles, ticks |
| `GridController` | `/api/grid` | Grid trading engine |
| `QuantStrategyController` | `/api/quantstrategy` | GEX, opções, quant |
| `ProfitChartController` | `/api/profitchart` | RTD status, subscribe |
| `MT5Controller` | `/api/mt5` | Bridge MetaTrader 5 |
| `OrdersController` | `/orders` | Próxima ordem (Simulador/EA) |

### Exemplos

```powershell
# Health
Invoke-RestMethod http://localhost:5053/api/health

# Wyckoff
Invoke-RestMethod "http://localhost:5053/api/analysis/wyckoff/MNQ?timeframe=5m"

# Tenants
Invoke-RestMethod http://localhost:5053/api/tenants

# ProfitChart health
Invoke-RestMethod http://localhost:5053/api/profitchart/health
```

## SignalR Hubs

| Hub | Path | Uso |
|-----|------|-----|
| `ProfitChartHub` | `/hubs/profitchart` | Quotes RTD tempo real |
| `TradingHub` | `/hubs/trading` | Sinais e ordens |
| `MarketHub` | `/hubs/market` | Market data |
| `ExecutionHub` | `/hubs/execution` | Fills, execuções |
| `RiskHub` | `/hubs/risk` | Alertas de risco |
| `NotificationHub` | `/hubs/notifications` | Notificações gerais |

Cliente JS (dashboard):

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl('http://localhost:5053/hubs/profitchart')
  .build();
```

## Autenticação

JWT configurado em `Program.cs`. Endpoints de login/registro **pendentes** (Fase 4).  
Hoje a maioria dos endpoints é acessível sem auth — tratar como dev-only.

## CORS

Credenciais habilitadas para origens localhost (dashboard Blazor/React).

## Versionamento

Health retorna `"version": "3.0.0"` (Clean Architecture host).
