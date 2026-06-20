# 🔌 Integrador ProfitChart - Guia Completo

[![ProfitChart](https://img.shields.io/badge/ProfitChart-RTD-blue)](https://profitchart.com.br)
[![SignalR](https://img.shields.io/badge/SignalR-WebSocket-green)](https://docs.microsoft.com/aspnet/signalr)
[![REST API](https://img.shields.io/badge/API-REST-orange)](https://swagger.io)

## 📋 Índice

- [Visão Geral](#visão-geral)
- [Arquitetura](#arquitetura)
- [Configuração](#configuração)
- [API REST](#api-rest)
- [WebSocket (SignalR)](#websocket-signalr)
- [Tópicos RTD Disponíveis](#tópicos-rtd-disponíveis)
- [Exemplos de Uso](#exemplos-de-uso)
- [Troubleshooting](#troubleshooting)

---

## 🎯 Visão Geral

O **NTBot** agora atua como **integrador completo** para a plataforma **ProfitChart**, permitindo que sistemas externos consumam dados de mercado em tempo real através de:

### Recursos Principais

✅ **RTD (Real-Time Data)** - Conexão nativa com ProfitChart via COM/RTD  
✅ **REST API** - Endpoints HTTP para consultas pontuais e snapshots  
✅ **WebSocket/SignalR** - Streaming de dados em tempo real para clientes  
✅ **Cache Inteligente** - Armazena último valor de cada ticker/tópico  
✅ **Health Monitoring** - Diagnóstico de comunicação e reconexão automática  
✅ **Multi-Ticker** - Suporte a múltiplos ativos simultaneamente  

---

## 🏗️ Arquitetura

```
┌──────────────┐
│  ProfitChart │  (Fonte de dados RTD)
└──────┬───────┘
       │ RTD COM
       ▼
┌──────────────┐
│  RTDService  │  (Recebe e processa dados)
└──────┬───────┘
       │
       ├─────────────┬─────────────┐
       ▼             ▼             ▼
┌─────────┐   ┌────────────┐  ┌──────────┐
│ REST API│   │  SignalR   │  │  Events  │
└────┬────┘   └─────┬──────┘  └────┬─────┘
     │              │              │
     ▼              ▼              ▼
┌────────────────────────────────────┐
│      Clientes Externos             │
│  (Web Apps, Bots, APIs, etc.)      │
└────────────────────────────────────┘
```

### Fluxo de Dados

1. **ProfitChart** envia dados via RTD
2. **RTDService** captura e armazena em cache
3. **Distribuição** via 3 canais:
   - **REST API**: Consultas HTTP  
   - **SignalR**: Streaming WebSocket  
   - **Events**: Callbacks C# internos  

---

## ⚙️ Configuração

### 1. Pré-requisitos

- ✅ **ProfitChart** instalado e rodando
- ✅ **RTD habilitado** no ProfitChart (Ferramentas → Configurações → RTD)
- ✅ **.NET 8.0** ou superior
- ✅ **Interop.RTDTrading.dll** na pasta `Libs`

### 2. Configurar Tickers

Edite o arquivo `rtd_config.json`:

```json
{
  "WIN": {
    "TICK": "WINJ25",
    "TICKERS": ["WINJ25", "WINFUT_F_0"],
    "BASE": 1,
    "N_CONTRATO": 5,
    "Description": "Mini Índice Bovespa",
    "AssetType": "FUTURE",
    "IsActive": true
  },
  "WDO": {
    "TICK": "WDOK25",
    "TICKERS": ["WDOK25", "DOLFUT_F_0"],
    "BASE": 5,
    "N_CONTRATO": 10,
    "Description": "Mini Dólar",
    "AssetType": "FUTURE",
    "IsActive": true
  }
}
```

### 3. Inicializar Serviço

O serviço é inicializado automaticamente na startup:

```csharp
// Program.cs
builder.Services.AddSingleton<IRtdService, RtdService>();

// Na inicialização
var rtdService = app.Services.GetRequiredService<IRtdService>();
await rtdService.InitializeAsync("rtd_config.json");
```

---

## 🌐 API REST

Base URL: `http://localhost:5053/api/profitchart`

### 📊 Endpoints

#### 1. Health Check

```http
GET /api/profitchart/health
```

**Resposta 200 OK:**
```json
{
  "status": "healthy",
  "isConnected": true,
  "totalDataReceived": 15847,
  "secondsSinceLastData": 0.5,
  "serviceStarted": "2026-04-15T10:30:00",
  "topicsConnected": 180,
  "topicsWithData": 165,
  "dataRate": "12.45 data/s",
  "timestamp": "2026-04-15T14:25:30"
}
```

#### 2. Estatísticas

```http
GET /api/profitchart/statistics
```

**Resposta:**
```json
{
  "totalDataReceived": 15847,
  "lastDataReceived": "2026-04-15T14:25:30",
  "serviceStarted": "2026-04-15T10:30:00",
  "totalTopicsConnected": 180,
  "topicsWithData": 165,
  "dataRatePerSecond": 12.45,
  "isConnected": true,
  "secondsSinceLastData": 0.5
}
```

#### 3. Lista de Tickers

```http
GET /api/profitchart/tickers
```

**Resposta:**
```json
{
  "WINJ25": {
    "ticker": "WINJ25",
    "logicalName": "WIN",
    "isReceivingData": true,
    "totalTopics": 60,
    "topicsWithData": 58,
    "lastUpdate": "2026-04-15T14:25:30",
    "lastPrice": 127850.0,
    "volume": 125478
  },
  "WDOK25": {
    "ticker": "WDOK25",
    "logicalName": "WDO",
    "isReceivingData": true,
    "totalTopics": 60,
    "topicsWithData": 55,
    "lastPrice": 5.4520,
    "volume": 89654
  }
}
```

#### 4. Snapshot de Ticker

```http
GET /api/profitchart/tickers/{ticker}
```

**Exemplo:**
```http
GET /api/profitchart/tickers/WINJ25
```

**Resposta:**
```json
{
  "ULT": 127850.0,
  "VOL": 125478,
  "VOC": 65230,
  "VOV": 60248,
  "OCP": 127800.0,
  "MAX": 128100.0,
  "MIN": 127200.0,
  "PRT": 127825.0,
  "QC": 150,
  "QV": 120,
  "QC1": 50, "QV1": 45,
  "QC2": 30, "QV2": 25
}
```

#### 5. Tópico Específico

```http
GET /api/profitchart/tickers/{ticker}/{topic}
```

**Exemplo:**
```http
GET /api/profitchart/tickers/WINJ25/ULT
```

**Resposta:**
```json
{
  "ticker": "WINJ25",
  "topic": "ULT",
  "value": 127850.0,
  "timestamp": "2026-04-15T14:25:30"
}
```

#### 6. Múltiplos Preços

```http
GET /api/profitchart/prices?tickers=WINJ25,WDOK25,PETR4
```

**Resposta:**
```json
{
  "WINJ25": {
    "price": 127850.0,
    "timestamp": "2026-04-15T14:25:30"
  },
  "WDOK25": {
    "price": 5.4520,
    "timestamp": "2026-04-15T14:25:30"
  },
  "PETR4": {
    "price": 38.45,
    "timestamp": "2026-04-15T14:25:30"
  }
}
```

#### 7. Book de Ofertas

```http
GET /api/profitchart/book/{ticker}?levels=5
```

**Exemplo:**
```http
GET /api/profitchart/book/WINJ25?levels=5
```

**Resposta:**
```json
{
  "ticker": "WINJ25",
  "compra": [
    { "level": 1, "quantity": 50, "price": 127845 },
    { "level": 2, "quantity": 30, "price": 127840 },
    { "level": 3, "quantity": 25, "price": 127835 }
  ],
  "venda": [
    { "level": 1, "quantity": 45, "price": 127850 },
    { "level": 2, "quantity": 35, "price": 127855 },
    { "level": 3, "quantity": 20, "price": 127860 }
  ],
  "timestamp": "2026-04-15T14:25:30"
}
```

#### 8. Configuração de Ticker

```http
GET /api/profitchart/config/{logical}
```

**Exemplo:**
```http
GET /api/profitchart/config/WIN
```

**Resposta:**
```json
{
  "tick": "WINJ25",
  "tickers": ["WINJ25", "WINFUT_F_0"],
  "base": 1,
  "n_CONTRATO": 5,
  "description": "Mini Índice Bovespa",
  "assetType": "FUTURE",
  "isActive": true
}
```

---

## 🔌 WebSocket (SignalR)

Hub URL: `ws://localhost:5053/hubs/profitchart`

### Exemplo JavaScript

```javascript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5053/hubs/profitchart")
    .withAutomaticReconnect()
    .build();

// Eventos de conexão
connection.on("ConnectionStatus", (data) => {
    console.log("Conectado:", data);
});

// Receber atualizações em tempo real
connection.on("TickUpdate", (data) => {
    console.log(`${data.ticker}.${data.topic} = ${data.value}`);
    // Exemplo: WINJ25.ULT = 127850
});

// Snapshot completo do ticker
connection.on("TickerSnapshot", (data) => {
    console.log("Snapshot:", data);
});

// Iniciar conexão
await connection.start();

// Inscrever em ticker específico
await connection.invoke("SubscribeTicker", "WINJ25");

// Inscrever em todos os tickers
await connection.invoke("SubscribeAll");

// Obter estatísticas
await connection.invoke("GetStatistics");

// Cancelar inscrição
await connection.invoke("UnsubscribeTicker", "WINJ25");
```

### Exemplo C#

```csharp
using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5053/hubs/profitchart")
    .WithAutomaticReconnect()
    .Build();

connection.On<object>("TickUpdate", (data) =>
{
    Console.WriteLine($"Tick recebido: {data}");
});

await connection.StartAsync();
await connection.InvokeAsync("SubscribeTicker", "WINJ25");
```

### Exemplo Python

```python
from signalrcore.hub_connection_builder import HubConnectionBuilder

connection = HubConnectionBuilder()\
    .with_url("http://localhost:5053/hubs/profitchart")\
    .with_automatic_reconnect()\
    .build()

def on_tick_update(data):
    print(f"{data['ticker']}.{data['topic']} = {data['value']}")

connection.on("TickUpdate", on_tick_update)
connection.start()
connection.send("SubscribeTicker", ["WINJ25"])
```

---

## 📡 Tópicos RTD Disponíveis

### Preços e Volumes

| Tópico | Descrição |
|--------|-----------|
| `ULT` | Último preço negociado |
| `PRT` | Preço de abertura |
| `MAX` | Máxima do dia |
| `MIN` | Mínima do dia |
| `FEC` | Fechamento anterior |
| `VOL` | Volume total |
| `VOC` | Volume de compra |
| `VOV` | Volume de venda |

### Book de Ofertas

| Tópico | Descrição |
|--------|-----------|
| `QC` | Quantidade compra total |
| `QV` | Quantidade venda total |
| `QC1-QC20` | Quantidade compra por nível |
| `QV1-QV20` | Quantidade venda por nível |

### Outros

| Tópico | Descrição |
|--------|-----------|
| `OCP` | Oferta de compra |
| `OVD` | Oferta de venda |
| `EST` | Estado (mercado aberto/fechado) |
| `AJA` | Ajuste anterior |
| `AJU` | Ajuste atual |
| `HORA` | Horário da última atualização |
| `VWA` | Preço médio ponderado (VWAP) |

---

## 💡 Exemplos de Uso

### 1. Monitor de Preços (Python)

```python
import requests
import time

def monitor_prices():
    while True:
        response = requests.get(
            "http://localhost:5053/api/profitchart/prices",
            params={"tickers": "WINJ25,WDOK25,PETR4"}
        )
        
        if response.status_code == 200:
            prices = response.json()
            for ticker, data in prices.items():
                print(f"{ticker}: R${data['price']}")
        
        time.sleep(1)

monitor_prices()
```

### 2. Dashboard em React

```jsx
import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';

function TradingDashboard() {
  const [prices, setPrices] = useState({});

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5053/hubs/profitchart')
      .build();

    connection.on('TickUpdate', (data) => {
      if (data.topic === 'ULT') {
        setPrices(prev => ({
          ...prev,
          [data.ticker]: data.value
        }));
      }
    });

    connection.start().then(() => {
      connection.invoke('SubscribeAll');
    });

    return () => connection.stop();
  }, []);

  return (
    <div>
      {Object.entries(prices).map(([ticker, price]) => (
        <div key={ticker}>
          <strong>{ticker}:</strong> {price}
        </div>
      ))}
    </div>
  );
}
```

### 3. Bot de Trading (C#)

```csharp
public class TradingBot
{
    private readonly IHttpClientFactory _httpClientFactory;

    public async Task MonitorAndTrade(string ticker)
    {
        var client = _httpClientFactory.CreateClient();
        
        while (true)
        {
            var response = await client.GetAsync(
                $"http://localhost:5053/api/profitchart/tickers/{ticker}/ULT");
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TickData>();
                
                if (data.Value > 128000)
                {
                    Console.WriteLine("VENDER!");
                }
                else if (data.Value < 127000)
                {
                    Console.WriteLine("COMPRAR!");
                }
            }
            
            await Task.Delay(1000);
        }
    }
}
```

---

## 🔧 Troubleshooting

### ❌ Erro: "Nenhum dado recebido"

**Verificar:**
1. ✅ ProfitChart está aberto?
2. ✅ Há um "M" verde no canto superior direito do ProfitChart?
3. ✅ RTD está habilitado? (Ferramentas → Configurações → RTD)
4. ✅ Os tickers em `rtd_config.json` existem no ProfitChart?
5. ✅ Firewall não está bloqueando a comunicação?

### ❌ Erro: "Service Unhealthy"

```json
{
  "status": "unhealthy",
  "isConnected": false,
  "totalDataReceived": 0
}
```

**Solução:**
1. Reinicie o ProfitChart
2. Reinicie o NTBot API
3. Verifique os logs em `logs/ntbot-*.txt`

### ❌ SignalR não conecta

**Verificar CORS:**
```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
```

---

## 📞 Suporte

- **Logs**: Verifique `logs/ntbot-*.txt`
- **Console**: Mensagens detalhadas durante execução
- **Health**: `GET /api/profitchart/health`
- **Statistics**: `GET /api/profitchart/statistics`

---

## 📄 Licença

MIT License - NTBot © 2026
