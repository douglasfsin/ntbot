# 🚀 Teste Rápido - Integrador ProfitChart

## ⚡ Inicialização (2 minutos)

### Passo 1: Verificar Pré-requisitos

- [ ] **ProfitChart** está aberto e rodando
- [ ] **RTD habilitado** no ProfitChart (deve ter "M" verde no canto superior direito)
- [ ] **.NET 8.0** instalado

### Passo 2: Configurar Tickers

Edite `NTBot.Api/rtd_config.json` com tickers válidos do seu ProfitChart:

```json
{
  "WIN": {
    "TICK": "WINJ25",
    "TICKERS": ["WINJ25"],
    "BASE": 1,
    "N_CONTRATO": 5,
    "IsActive": true
  }
}
```

> **IMPORTANTE:** Substitua `WINJ25` pelo ticker correto que aparece no ProfitChart!

### Passo 3: Iniciar API

```bash
cd NTBot.Api
dotnet run
```

**Aguarde ver:**
```
[RTD INIT] ✓ Servidor RTD iniciado com sucesso
✓ ProfitChart RTD Service initialized successfully
🚀 NTBot API starting on http://localhost:5053
```

---

## ✅ Testes Básicos

### Teste 1: Health Check (10 segundos)

```bash
curl http://localhost:5053/api/profitchart/health
```

**Resposta esperada (200 OK):**
```json
{
  "status": "healthy",
  "isConnected": true,
  "totalDataReceived": 1547
}
```

✅ **SUCESSO** se `isConnected: true` e `totalDataReceived > 0`  
❌ **FALHA** se `isConnected: false` → Verifique se ProfitChart está enviando dados

---

### Teste 2: Listar Tickers (5 segundos)

```bash
curl http://localhost:5053/api/profitchart/tickers
```

**Resposta esperada:**
```json
{
  "WINJ25": {
    "ticker": "WINJ25",
    "isReceivingData": true,
    "lastPrice": 127850.0
  }
}
```

✅ **SUCESSO** se aparecer o ticker configurado  
❌ **FALHA** se aparecer vazio → Verifique `rtd_config.json`

---

### Teste 3: Obter Preço (2 segundos)

```bash
curl http://localhost:5053/api/profitchart/tickers/WINJ25/ULT
```

> Substitua `WINJ25` pelo seu ticker!

**Resposta esperada:**
```json
{
  "ticker": "WINJ25",
  "topic": "ULT",
  "value": 127850.0,
  "timestamp": "2026-04-15T14:30:00"
}
```

✅ **SUCESSO** se retornar o preço atual  
❌ **FALHA (404)** se ticker não existir

---

### Teste 4: Snapshot Completo (5 segundos)

```bash
curl http://localhost:5053/api/profitchart/tickers/WINJ25
```

**Resposta esperada:**
```json
{
  "ULT": 127850.0,
  "VOL": 125478,
  "MAX": 128100.0,
  "MIN": 127200.0,
  "PRT": 127800.0
}
```

✅ **SUCESSO** se retornar múltiplos tópicos  

---

### Teste 5: WebSocket (SignalR) - JavaScript

Crie arquivo `test-websocket.html`:

```html
<!DOCTYPE html>
<html>
<head>
    <title>Test ProfitChart WebSocket</title>
    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@latest/dist/browser/signalr.min.js"></script>
</head>
<body>
    <h1>ProfitChart WebSocket Test</h1>
    <div id="status">Connecting...</div>
    <div id="data"></div>

    <script>
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("http://localhost:5053/hubs/profitchart")
            .build();

        connection.on("ConnectionStatus", (data) => {
            document.getElementById('status').innerHTML = 
                `✓ Connected - ID: ${data.connectionId}`;
        });

        connection.on("TickUpdate", (data) => {
            document.getElementById('data').innerHTML = 
                `${data.ticker}.${data.topic} = ${data.value} (${new Date(data.timestamp).toLocaleTimeString()})`;
        });

        connection.start().then(() => {
            connection.invoke("SubscribeTicker", "WINJ25"); // ALTERE O TICKER!
        });
    </script>
</body>
</html>
```

Abra no navegador: `test-websocket.html`

✅ **SUCESSO** se ver dados atualizando em tempo real  

---

## 🔍 Diagnóstico de Problemas

### ❌ "Service Unhealthy" ou "isConnected: false"

**Causa:** ProfitChart não está enviando dados

**Soluções:**

1. ✅ Verifique se o **"M" verde** aparece no ProfitChart (canto superior direito)
2. ✅ No ProfitChart: **Ferramentas → Configurações → RTD**
   - Marque **"Habilitar RTD"**
   - Clique em **Aplicar**
3. ✅ Verifique se o ticker existe no ProfitChart
4. ✅ Reinicie o ProfitChart
5. ✅ Reinicie a API (Ctrl+C e `dotnet run` novamente)

### ❌ "totalDataReceived: 0" após 30 segundos

**Causa:** RTD conectou mas não recebe dados

**Checklist:**

- [ ] Mercado está aberto?
- [ ] Ticker está correto no `rtd_config.json`?
- [ ] ProfitChart está logado na corretora?
- [ ] Firewall não está bloqueando?

**Veja logs detalhados:**
```bash
type NTBot.Api\logs\ntbot-*.txt
```

Procure por:
```
[RTD DATA] ✓✓✓ DADO RECEBIDO #1
```

Se **não aparecer** → ProfitChart não está enviando

---

## 📊 Swagger UI (Explorar API)

Abra no navegador: **http://localhost:5053**

- ✅ Veja todos os endpoints
- ✅ Teste diretamente pelo navegador
- ✅ Veja schemas de request/response

---

## 🎯 Teste Completo (Opcional)

### Node.js (npm install @microsoft/signalr)

```javascript
const signalR = require("@microsoft/signalr");

const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5053/hubs/profitchart")
    .build();

connection.on("TickUpdate", (data) => {
    console.log(`${data.ticker}.${data.topic} = ${data.value}`);
});

connection.start().then(() => {
    console.log("✓ Conectado!");
    connection.invoke("SubscribeAll");
});
```

Execute:
```bash
node test.js
```

✅ **SUCESSO** se ver dados chegando no console

---

### Python (pip install requests)

```python
import requests
import time

while True:
    r = requests.get('http://localhost:5053/api/profitchart/tickers/WINJ25/ULT')
    if r.status_code == 200:
        data = r.json()
        print(f"WINJ25 = {data['value']}")
    time.sleep(1)
```

Execute:
```bash
python test.py
```

✅ **SUCESSO** se ver preços atualizando a cada segundo

---

## ✅ Lista de Verificação Final

Antes de declarar sucesso, confirme:

- [ ] Health check retorna `"status": "healthy"`
- [ ] `totalDataReceived` está aumentando
- [ ] Consegue obter preço via REST API
- [ ] Consegue obter snapshot completo
- [ ] WebSocket conecta e recebe dados em tempo real
- [ ] Logs não mostram erros críticos

---

## 🎉 Sucesso!

Se todos os testes passaram, o integrador está funcionando perfeitamente!

**Próximos passos:**
1. Leia a [documentação completa](PROFITCHART_INTEGRATOR.md)
2. Veja os [exemplos de código](NTBot.Api/Examples/ProfitChartExamples.cs)
3. Integre com sua aplicação

---

## 📞 Ajuda

Se algo não funcionar:

1. **Veja os logs:** `NTBot.Api/logs/ntbot-*.txt`
2. **Console da API:** Mensagens detalhadas de debug
3. **Health endpoint:** Status em tempo real
4. **Documentação:** [PROFITCHART_INTEGRATOR.md](PROFITCHART_INTEGRATOR.md)

---

**Tempo total do teste: ~5 minutos** ⏱️
