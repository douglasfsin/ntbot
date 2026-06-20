# 🚀 Quick Start - NTBot

## Comandos Rápidos para Começar AGORA

### 1️⃣ Restaurar e Compilar (1 minuto)

```powershell
# Navegar para o projeto
cd c:\Projetos\ntbot\NTBot.Api

# Restaurar dependências
dotnet restore

# Compilar
dotnet build
```

### 2️⃣ Criar Database (2 minutos)

```powershell
# Instalar EF Tools (se ainda não tiver)
dotnet tool install --global dotnet-ef

# Criar migration
dotnet ef migrations add InitialCreate

# Criar database
dotnet ef database update
```

**Resultado esperado:**
```
✓ Migration InitialCreate created
✓ Database NTBotDB created
✓ Tables created: Tenants, Users, AssetConfigurations, TradingSignals, Trades, Candles, EconomicEvents, NewsAnalyses
✓ Seed data inserted
```

### 3️⃣ Rodar API (10 segundos)

```powershell
dotnet run
```

**Você verá:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5053
🚀 NTBot API starting on http://localhost:5053
📊 Swagger available at http://localhost:5053
```

### 4️⃣ Testar Endpoints (Swagger UI)

Abra no navegador:
```
http://localhost:5053
```

---

## 🧪 Testes Básicos (Sem NinjaTrader)

### Teste 1: Health Check ✅

```powershell
curl http://localhost:5053/api/health
```

**Esperado:**
```json
{
  "status": "healthy",
  "timestamp": "2025-12-28T...",
  "version": "2.0.0",
  "services": {
    "database": "connected",
    "ninjaTrader": "ready",
    "wyckoff": "enabled",
    "macro": "enabled"
  }
}
```

### Teste 2: Listar Tenants ✅

```powershell
curl http://localhost:5053/api/tenants
```

**Esperado:**
```json
[
  {
    "id": "11111111-1111-1111-1111-111111111111",
    "name": "Test Tenant",
    "email": "test@ntbot.com",
    "plan": "PRO",
    "isActive": true,
    "assetConfigurations": [
      {
        "symbol": "MNQ",
        "isActive": true
      }
    ]
  }
]
```

### Teste 3: Análise Wyckoff (Mock Data)

Para testar sem NinjaTrader, você precisa primeiro popular a tabela `Candles` com dados históricos.

**Opção 1: Usar Simulador para popular dados**

```powershell
cd c:\Projetos\ntbot\Simulador
dotnet run
```

Isso vai ler o CSV e enviar dados para a API.

**Opção 2: Mock temporário no código**

Edite `NinjaTraderService.cs` temporariamente:

```csharp
public async Task<List<Candle>> GetHistoricalCandlesAsync(string symbol, string timeframe, DateTime from, DateTime to)
{
    // Mock data para teste
    return Enumerable.Range(0, 100).Select(i => new Candle
    {
        Id = Guid.NewGuid(),
        Symbol = symbol,
        Timeframe = timeframe,
        OpenTime = from.AddMinutes(i * 5),
        CloseTime = from.AddMinutes((i + 1) * 5),
        Open = 16000 + i,
        High = 16010 + i,
        Low = 15990 + i,
        Close = 16005 + i,
        Volume = 1000 + i * 10,
        CreatedAt = DateTime.UtcNow
    }).ToList();
}
```

Depois teste:

```powershell
curl "http://localhost:5053/api/analysis/wyckoff/MNQ?timeframe=5m"
```

---

## 📝 Verificar Se Está Tudo OK

### Checklist ✅

- [ ] `dotnet --version` → 8.0 ou superior
- [ ] SQL Server rodando (pode ser Express ou LocalDB)
- [ ] Database `NTBotDB` criada
- [ ] API rodando em http://localhost:5053
- [ ] Swagger abre sem erros
- [ ] `/api/health` retorna `healthy`
- [ ] `/api/tenants` retorna lista com 1 tenant

---

## 🔥 Se Algo Der Errado

### Erro: "Unable to connect to SQL Server"

**Solução:**

1. Verificar se SQL Server está rodando:
```powershell
# Iniciar serviço SQL Server
net start MSSQLSERVER
# OU para SQL Express:
net start MSSQL$SQLEXPRESS
```

2. Verificar connection string no `appsettings.json`:
```json
"DefaultConnection": "Server=localhost;Database=NTBotDB;Trusted_Connection=True;TrustServerCertificate=True"
```

3. Se usar SQL Express, mudar para:
```json
"DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=NTBotDB;Trusted_Connection=True;TrustServerCertificate=True"
```

### Erro: "Migration already applied"

**Solução:**

```powershell
# Remover migration
dotnet ef migrations remove

# Recriar
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### Erro: "Port 5053 already in use"

**Solução:**

```powershell
# Matar processo na porta
netstat -ano | findstr :5053
taskkill /PID <PID> /F

# OU mudar porta em launchSettings.json
```

### Erro ao compilar: "Missing package"

**Solução:**

```powershell
# Limpar e restaurar
dotnet clean
dotnet restore
dotnet build
```

---

## 🎯 Próximos Passos Após Rodar

### Passo 1: Popular Dados Históricos

Use o projeto `Simulador` para popular a tabela `Candles`:

```powershell
cd c:\Projetos\ntbot\Simulador
# Editar Program.cs para apontar para sua API
dotnet run
```

### Passo 2: Testar Análises

```powershell
# Wyckoff 5m
curl "http://localhost:5053/api/analysis/wyckoff/MNQ?timeframe=5m"

# Macro
curl "http://localhost:5053/api/analysis/macro/MNQ"

# Completa
curl "http://localhost:5053/api/analysis/complete/MNQ?timeframe=5m"
```

### Passo 3: Implementar Economic Calendar

Veja `README_IMPLEMENTATION.md` seção "Economic Calendar Service"

### Passo 4: Implementar Decision Engine

Veja `README_IMPLEMENTATION.md` seção "Decision Engine"

### Passo 5: Criar Dashboard

```powershell
cd c:\Projetos\ntbot\Dashboard
npx create-react-app . --template typescript
npm install @microsoft/signalr axios recharts antd
npm start
```

---

## 📚 Documentação Completa

- **`ARCHITECTURE.md`** - Arquitetura detalhada do sistema
- **`README_IMPLEMENTATION.md`** - Guia de implementação completo
- **`GETTING_STARTED.md`** - Como começar do zero
- **Este arquivo** - Comandos rápidos

---

## 💡 Dicas Pro

### 1. Usar Watch Mode (Hot Reload)

```powershell
dotnet watch run
```

Agora toda vez que salvar um arquivo .cs, a API recompila automaticamente!

### 2. Ver Logs Detalhados

```powershell
dotnet run --verbosity detailed
```

### 3. Rodar em Produção

```powershell
dotnet publish -c Release -o ./publish
cd publish
dotnet NTBot.Api.dll
```

### 4. Docker (Opcional)

```powershell
docker build -t ntbot-api .
docker run -p 5053:80 ntbot-api
```

---

## 🎉 Tudo Funcionando?

**PARABÉNS! 🚀**

Você tem agora:
- ✅ API RESTful rodando
- ✅ Database configurado
- ✅ Wyckoff Engine funcional
- ✅ Macro Analyzer funcional
- ✅ Swagger UI para testes

**Próximo milestone:** Implementar Decision Engine e começar backtesting!

---

## 📞 Precisa de Ajuda?

1. Revisar logs em `logs/ntbot-YYYY-MM-DD.txt`
2. Verificar Swagger: http://localhost:5053
3. Testar health check primeiro
4. Consultar documentação detalhada nos arquivos `.md`

**Boa sorte! Keep building! 💪🚀**
