# 🚀 Configuração Rápida: NinjaTrader + NTBot

## 📋 Passo a Passo Simplificado

### ✅ Passo 1: Instalar NinjaScript Addon (5 minutos)

1. **Abra o NinjaTrader 8**

2. **Abra o Editor de NinjaScript:**
   ```
   Tools → Edit NinjaScript → AddOn
   ```

3. **Crie um novo Addon:**
   - Clique com botão direito na seção "AddOn"
   - Selecione "New..."
   - Nome: `NTBotHttpServer`
   - Clique OK

4. **Cole o código:**
   - Copie TODO o conteúdo do arquivo: `NinjaScript/NTBotHttpServer.cs`
   - Cole no editor (substituindo o código padrão)

5. **Compile:**
   - Pressione **F5** ou clique no botão "Compile"
   - Aguarde compilar (deve aparecer "Compiled successfully")
   - Feche o editor

### ✅ Passo 2: Ativar o Addon (1 minuto)

1. **No Control Center do NinjaTrader:**
   ```
   Tools → AddOns
   ```

2. **Encontre "NTBot HTTP Server"** na lista

3. **Clique no botão "Enable"** ou marque o checkbox

4. **Verifique a aba "Output"** no canto inferior:
   ```
   Deve aparecer:
   ✅ NTBot HTTP Server iniciado com sucesso!
   📡 Porta: 8080
   🔗 URL: http://localhost:8080
   ```

### ✅ Passo 3: Testar Conexão (1 minuto)

**Opção A: No Navegador**
```
Abra: http://localhost:8080/api/health
```

Resposta esperada:
```json
{
  "status": "healthy",
  "service": "NinjaTrader 8",
  "version": "1.0.0",
  "timestamp": "2025-12-29T...",
  "isConnected": true,
  "accountCount": 1
}
```

**Opção B: No PowerShell**
```powershell
Invoke-WebRequest -Uri "http://localhost:8080/api/health" | Select-Object -Expand Content
```

### ✅ Passo 4: Configurar NTBot.Api

Edite `appsettings.json`:

```json
{
  "NinjaTrader": {
    "ApiBaseUrl": "http://localhost:8080",
    "Timeout": 30,
    "RetryAttempts": 3
  }
}
```

### ✅ Passo 5: Reiniciar Backend

```powershell
cd C:\Projetos\ntbot\NTBot.Api
dotnet run
```

## 🎯 Endpoints Disponíveis

Após configurado, você terá acesso a:

| Endpoint | Método | Descrição |
|----------|--------|-----------|
| `/api/health` | GET | Status do servidor |
| `/api/accounts` | GET | Lista de contas NT |
| `/api/positions` | GET | Posições abertas |
| `/api/orders` | GET | Lista de ordens |
| `/api/orders` | POST | Criar nova ordem |
| `/api/marketdata?symbol=ES` | GET | Dados de mercado |

## 📊 Exemplos de Uso

### 1. Ver Contas
```powershell
Invoke-WebRequest -Uri "http://localhost:8080/api/accounts" | Select-Object -Expand Content
```

### 2. Ver Posições
```powershell
Invoke-WebRequest -Uri "http://localhost:8080/api/positions" | Select-Object -Expand Content
```

### 3. Criar Ordem Market
```powershell
$order = @{
    Symbol = "ES 12-25"
    Action = "BUY"
    OrderType = "MARKET"
    Quantity = 1
} | ConvertTo-Json

Invoke-WebRequest -Uri "http://localhost:8080/api/orders" `
  -Method POST `
  -Body $order `
  -ContentType "application/json"
```

### 4. Criar Ordem Limit
```powershell
$order = @{
    Symbol = "ES 12-25"
    Action = "SELL"
    OrderType = "LIMIT"
    Quantity = 1
    LimitPrice = 6050.00
} | ConvertTo-Json

Invoke-WebRequest -Uri "http://localhost:8080/api/orders" `
  -Method POST `
  -Body $order `
  -ContentType "application/json"
```

## 🚨 Solução de Problemas

### ❌ Erro: "Access Denied" ou "Permission Error"

**Causa:** Windows precisa de permissão para criar HTTP listener

**Solução 1 (Recomendada):**
Execute o NinjaTrader como Administrador:
```
Botão direito no ícone do NT → Executar como administrador
```

**Solução 2 (Alternativa):**
Liberar porta para todos os usuários (PowerShell como Admin):
```powershell
netsh http add urlacl url=http://+:8080/ user=Everyone
```

### ❌ Erro: "Port already in use"

**Causa:** Outra aplicação está usando a porta 8080

**Solução:**
Alterar porta no código do addon:
```csharp
// Linha ~38 no NTBotHttpServer.cs
private const int PORT = 8081; // Mudar para outra porta
```

### ❌ Addon não aparece em Tools → AddOns

**Causa:** Erro de compilação ou arquivo não foi salvo

**Solução:**
1. Reabra o editor: Tools → Edit NinjaScript → AddOn
2. Encontre "NTBotHttpServer"
3. Verifique se há erros (aba "Errors" na parte inferior)
4. Compile novamente (F5)
5. Reinicie o NinjaTrader

### ❌ "Instrument not found"

**Causa:** Símbolo não está carregado no NinjaTrader

**Solução:**
Adicione o instrumento:
```
Control Center → Instruments → ES → Add
```

## 🔥 Recursos Avançados

### Adicionar Logging Detalhado

No código do addon, descomente as linhas de Print():
```csharp
// Print($"📥 {method} {path}"); // ← Já está ativo
```

### Adicionar Autenticação

Adicione verificação de token JWT:
```csharp
private bool ValidateToken(HttpListenerRequest request)
{
    string token = request.Headers["Authorization"]?.Replace("Bearer ", "");
    // Implementar validação JWT
    return !string.IsNullOrEmpty(token);
}
```

### WebSocket para Real-Time

Para streaming real-time de candles e orders, implemente WebSocket:
```csharp
// TODO: Adicionar WebSocket listener
// Para updates em tempo real
```

## ✅ Checklist de Validação

Marque conforme vai completando:

- [ ] NinjaTrader 8 instalado
- [ ] Conta demo conectada (Sim ou Kinetick)
- [ ] Addon compilado sem erros
- [ ] Addon ativado em Tools → AddOns
- [ ] Mensagem "✅ NTBot HTTP Server iniciado" na aba Output
- [ ] `/api/health` responde no navegador
- [ ] `/api/accounts` retorna suas contas
- [ ] Firewall/antivírus não está bloqueando
- [ ] `appsettings.json` do NTBot.Api configurado
- [ ] Backend NTBot.Api rodando
- [ ] Dashboard React conectado

## 🎓 Próximos Passos

Após configurar:

1. ✅ Testar todos endpoints manualmente
2. ✅ Rodar backend NTBot.Api
3. ✅ Conectar dashboard React
4. ✅ Testar criação de ordens em conta demo
5. ✅ Implementar estratégias de trading
6. ✅ Monitorar logs e performance

## 📞 Suporte

Se encontrar problemas:

1. Verifique a aba "Output" do NT (Ctrl+O)
2. Verifique logs do NTBot.Api
3. Teste endpoints individualmente no Postman/Insomnia
4. Verifique firewall Windows
5. Consulte documentação oficial: https://ninjatrader.com

---

**Desenvolvido por:** NTBot Team  
**Versão:** 1.0.0  
**Data:** Dezembro 2025
