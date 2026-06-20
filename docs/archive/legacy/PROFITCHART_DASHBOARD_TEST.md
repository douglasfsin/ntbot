# Teste Rápido - Integração ProfitChart Dashboard

## Pré-requisitos

1. ProfitChart instalado e aberto
2. .NET 8.0 SDK instalado
3. Node.js 18+ instalado

## Passo 1: Configurar Backend

### 1.1. Editar rtd_config.json
```bash
cd c:\Projetos\ntbot\NTBot.Api
```

Edite `rtd_config.json` com seus tickers:
```json
{
  "LogicalNames": [
    {
      "Name": "WIN",
      "Topics": {
        "LastTrade": 1,
        "BestBid": 2,
        "BestAsk": 3,
        "Volume": 4,
        "Quantity1": 5,
        "Quantity2": 6,
        "Quantity3": 7,
        "Quantity4": 8,
        "Quantity5": 9
      }
    },
    {
      "Name": "WDO",
      "Topics": {
        "LastTrade": 1,
        "BestBid": 2,
        "BestAsk": 3,
        "Volume": 4
      }
    }
  ]
}
```

### 1.2. Iniciar Backend
```powershell
dotnet run
```

Aguarde a mensagem:
```
✅ ProfitChart RTD Integration initialized
Now listening on: http://localhost:5053
```

### 1.3. Teste o Backend
**REST API:**
```powershell
# Testar health
curl http://localhost:5053/api/profitchart/health

# Buscar tickers
curl http://localhost:5053/api/profitchart/tickers

# Buscar estatísticas
curl http://localhost:5053/api/profitchart/statistics
```

**Resultado esperado:**
```json
{
  "isHealthy": true,
  "isConnected": true,
  "statistics": {
    "isConnected": true,
    "tickersRegistered": 2,
    "topicsRegistered": 13,
    ...
  }
}
```

## Passo 2: Configurar Frontend

### 2.1. Instalar Dependências
```bash
cd c:\Projetos\ntbot\ntbot-dashboard
npm install
```

### 2.2. Verificar package.json
Certifique-se de que essas dependências estão instaladas:
- `@microsoft/signalr`: ^10.0.0
- `axios`: ^1.13.2
- `react-hot-toast`: ^2.6.0
- `react-router-dom`: ^6.30.2

### 2.3. Iniciar Dashboard
```bash
npm run dev
```

Aguarde a mensagem:
```
➜  Local:   http://localhost:5173/
➜  Network: use --host to expose
```

## Passo 3: Testar Dashboard

### 3.1. Abrir Dashboard
1. Abrir navegador em: http://localhost:5173
2. Verificar que a página principal carrega
3. Ver seção "ProfitChart - Tempo Real" com 3 cards:
   - WIN (Mini Índice)
   - WDO (Mini Dólar)
   - USD/BRL (Dólar Comercial)

### 3.2. Verificar Tempo Real
**O que você deve ver:**
- Cards com preços atualizando automaticamente
- Indicador de tendência (↑ verde / ↓ vermelho)
- Volume formatado (ex: 1.5K, 2.3M)
- Timestamp da última atualização

**Console do Browser (F12):**
```
SignalR connected to ProfitChart hub
Subscribed to ticker: WIN
Subscribed to ticker: WDO
Tick received: { ticker: 'WIN', topic: 'LastTrade', value: 126500 }
```

### 3.3. Testar Página ProfitChart
1. Clicar em "ProfitChart" no menu lateral
2. Ver página completa com:
   - Estatísticas gerais (conexão, uptime, tickers)
   - Grid com todos os tickers
   - Book de ofertas do ticker selecionado

### 3.4. Testar Book de Ofertas
1. Clicar em qualquer ticker card
2. Ver book na parte inferior com:
   - Níveis de compra (verde)
   - Níveis de venda (vermelho)
   - Barras visuais de quantidade

## Passo 4: Troubleshooting

### Problema: Cards não atualizam

**Verificações:**
```powershell
# 1. Backend rodando?
curl http://localhost:5053/api/profitchart/health

# 2. SignalR conectado?
# No console do browser (F12), ver:
SignalR connected to ProfitChart hub
```

**Solução:**
- Certificar que ProfitChart está aberto
- Verificar logs em `NTBot.Api/logs/`
- Reiniciar backend (Ctrl+C e `dotnet run`)

### Problema: "Cannot connect to SignalR"

**Causa:** Backend não está rodando ou CORS bloqueando.

**Solução:**
```powershell
# Verificar se backend está rodando na porta 5053
netstat -an | findstr 5053

# Se não estiver, iniciar:
cd c:\Projetos\ntbot\NTBot.Api
dotnet run
```

### Problema: 404 Not Found

**Causa:** Rota não encontrada.

**Solução:**
- Verificar URL no browser: http://localhost:5173/profitchart
- Confirmar que App.tsx tem a rota configurada
- Limpar cache do browser (Ctrl+Shift+R)

### Problema: Dados não aparecem

**Verificações:**
1. ProfitChart está aberto? ✅
2. rtd_config.json tem tickers configurados? ✅
3. Backend logs mostram "RTD initialized"? ✅

**Solução:**
```powershell
# Ver logs em tempo real
cd c:\Projetos\ntbot\NTBot.Api\logs
Get-Content .\ntbot-*.txt -Tail 50 -Wait
```

## Passo 5: Validação Completa

### Checklist ✅

Backend:
- [ ] Backend rodando em http://localhost:5053
- [ ] GET /api/profitchart/health retorna OK
- [ ] GET /api/profitchart/tickers retorna lista
- [ ] SignalR hub em /hubs/profitchart conectado
- [ ] Logs mostram "RTD initialized"

Frontend:
- [ ] Dashboard rodando em http://localhost:5173
- [ ] Página principal mostra 3 TickerCards
- [ ] Cards atualizam em tempo real
- [ ] Console mostra "SignalR connected"
- [ ] Link "ProfitChart" no menu funciona

Integração:
- [ ] Clicar em ticker abre book
- [ ] Book atualiza automaticamente
- [ ] Estatísticas mostram uptime correto
- [ ] Indicadores de tendência mudam com preço

## Passo 6: Exemplos de Uso

### Adicionar Novo Ticker

**Backend (rtd_config.json):**
```json
{
  "Name": "PETR4",
  "Topics": {
    "LastTrade": 1,
    "BestBid": 2,
    "BestAsk": 3,
    "Volume": 4
  }
}
```

**Frontend (Dashboard.tsx):**
```tsx
<TickerCard 
  ticker="PETR4" 
  logicalName="Petrobras PN" 
  autoSubscribe={true} 
/>
```

### Criar Widget Personalizado

```tsx
import { TickerCard } from '../components/profitchart';

function MyCustomWidget() {
  return (
    <div className="grid grid-cols-2 gap-4">
      <TickerCard ticker="WIN" autoSubscribe={true} />
      <TickerCard ticker="WDO" autoSubscribe={true} />
    </div>
  );
}
```

### Monitorar Múltiplos Ativos

```tsx
import { profitChartSignalR } from '../services/profitchart.signalr';

useEffect(() => {
  const init = async () => {
    await profitChartSignalR.connect();
    await profitChartSignalR.subscribeAll();
    
    profitChartSignalR.onTickUpdate((update) => {
      console.log(`${update.ticker}: ${update.value}`);
    });
  };
  
  init();
  
  return () => profitChartSignalR.disconnect();
}, []);
```

## Comandos Úteis

```powershell
# Backend
cd c:\Projetos\ntbot\NTBot.Api
dotnet build              # Compilar
dotnet run                # Executar
dotnet watch run          # Executar com hot-reload

# Frontend
cd c:\Projetos\ntbot\ntbot-dashboard
npm install               # Instalar dependências
npm run dev               # Executar dev server
npm run build             # Build para produção
npm run lint              # Verificar código

# Verificar processos
netstat -an | findstr 5053      # Backend
netstat -an | findstr 5173      # Frontend

# Ver logs
cd c:\Projetos\ntbot\NTBot.Api\logs
Get-Content .\ntbot-*.txt -Tail 50
```

## Próximos Passos

Após testar com sucesso:
1. [ ] Adicionar mais tickers no rtd_config.json
2. [ ] Customizar visual dos componentes
3. [ ] Criar alertas de preço
4. [ ] Integrar com estratégias de trading
5. [ ] Adicionar gráficos de candlestick

## Documentação

- `PROFITCHART_INTEGRATION_GUIDE.md` - Guia completo de integração
- `PROFITCHART_DASHBOARD.md` - Referência do dashboard
- `PROFITCHART_SUMMARY.md` - Resumo técnico
- `README_IMPLEMENTATION.md` - Implementação geral

## Suporte

**Logs:** `NTBot.Api/logs/ntbot-YYYYMMDD.txt`
**Console:** Browser DevTools (F12)
**Status:** http://localhost:5053/api/profitchart/health
