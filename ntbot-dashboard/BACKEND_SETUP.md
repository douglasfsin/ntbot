# Backend Setup — NTBot API v3

> **Api atual:** `src/NtBot.Api` (porta 5053). O monólito `NTBot.Api/` na raiz foi removido.

Documentação completa: [../docs/getting-started.md](../docs/getting-started.md)

## ✅ Status Atual

O **dashboard React está funcionando corretamente**! 🎉

Os erros no console são **esperados** porque o backend precisa de ajustes:

## 🔧 Correções Necessárias

### 1. CORS Configurado ✅

Já adicionei a porta `3001` no CORS do `Program.cs`:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDashboard", policy =>
    {
        policy.WithOrigins(
                  "http://localhost:3000", 
                  "http://localhost:3001",  // ✅ ADICIONADO
                  "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

### 2. Reiniciar a API

A API está rodando na porta `:5053`. Para aplicar as mudanças do CORS:

**Opção 1 - Parar e Reiniciar:**
```powershell
# Encontrar processo
Get-Process -Name "NtBot.Api" -ErrorAction SilentlyContinue | Stop-Process -Force

# Reiniciar
cd C:\Projetos\ntbot\src\NtBot.Api
dotnet run
```

**Opção 2 - Usar Ctrl+C no terminal onde a API está rodando e executar:**
```powershell
dotnet run
```

### 3. SignalR Hub (Opcional - Não Critico)

O erro do SignalR é porque o hub não está implementado:

```
POST http://localhost:5053/hubs/trading/negotiate?negotiateVersion=1 net::ERR_FAILED
```

**Para implementar:**

1. Criar `Hubs/TradingHub.cs`:
```csharp
using Microsoft.AspNetCore.SignalR;

namespace NtBot.Api.Hubs;

public class TradingHub : Hub
{
    public async Task SendCandle(object candle)
    {
        await Clients.All.SendAsync("ReceiveCandle", candle);
    }

    public async Task SendSignal(object signal)
    {
        await Clients.All.SendAsync("ReceiveSignal", signal);
    }

    public async Task SendTrade(object trade)
    {
        await Clients.All.SendAsync("ReceiveTrade", trade);
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        Console.WriteLine($"Client connected: {Context.ConnectionId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        Console.WriteLine($"Client disconnected: {Context.ConnectionId}");
    }
}
```

2. Descomentar no `Program.cs` (linha ~147):
```csharp
// Remover comentário:
app.MapHub<TradingHub>("/hubs/trading");
```

3. Adicionar using:
```csharp
using NtBot.Api.Hubs;
```

## 🎯 Ordem de Prioridade

1. **CRÍTICO**: Reiniciar a API para aplicar CORS ✅
2. **IMPORTANTE**: Implementar SignalR Hub (se quiser real-time)
3. **OPCIONAL**: Testar endpoints da API

## 🧪 Testando Após Reiniciar

Após reiniciar a API, recarregue o dashboard (`F5`) e você deve ver:

✅ **Dashboard funcionando**
✅ **Sem erros de CORS**
✅ **Dados carregando** (se houver dados no banco)
⚠️ **SignalR mostrando "Desconectado"** (até implementar o hub)

## 📊 Dashboard Features

Já implementado:
- ✅ Layout com Header + Sidebar
- ✅ Dashboard principal com cards de estatísticas
- ✅ Integração com API (Axios)
- ✅ Roteamento (6 páginas)
- ✅ Store Zustand (auth + trading)
- ✅ Tailwind CSS dark theme
- ✅ PWA configurado

Faltam implementar:
- ⏳ Páginas completas (Wyckoff, Macro, Signals, Trades, Settings)
- ⏳ Componentes de charts (TradingView)
- ⏳ SignalR real-time updates
- ⏳ Autenticação JWT

## 🚀 Próximos Passos

1. Reiniciar a API
2. Testar o dashboard funcionando sem erros CORS
3. Implementar as páginas faltantes
4. Adicionar SignalR hub
5. Criar componentes de visualização de dados
