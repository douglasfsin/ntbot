# Feature: Tape Speed (Termômetro de Velocidade do Fluxo)

## 📌 Objetivo
Motor analítico em tempo real (`TapeSpeedEngine`) dentro do `Orbital.Core` que mede a velocidade, frequência e apetite do fluxo de ordens (Tape Reading), respondendo às seguintes perguntas de negócio:

1. **O mercado está com maior apetite para compra ou para venda?**
2. **O mercado está calmo ou agitado?**
3. **Qual é a média de negócios e contratos negociados?**
4. **O mercado está negociando acima ou abaixo da média de contratos?**

---

## 🎯 Lógica de Classificação (Regras de Negócio)

### 1. Apetite (Compra vs Venda)
- **Fonte:** Janela de 15 segundos (estável o suficiente para confirmar tendência, curta o suficiente para ser imediata).
- **Threshold:** Se o volume de compra representar ≥ **55%** do volume total → `Appetite.Buy`. Se ≤ **45%** → `Appetite.Sell`. Caso contrário → `Appetite.Neutral`.
- **Fórmula:** `ratio = CompraVolume15s / (CompraVolume15s + VendaVolume15s)`

### 2. Agitação (Calmo vs Agitado)
- **Fonte:** Comparação entre a frequência de negócios por segundo dos últimos 5s e a média dos últimos 60s.
- **Threshold:** Se `freq5s > freq60s * 1.5` **E** houver no mínimo 5 negócios na janela → `MarketState.Agitated`.
- **Interpretação:** Indica urgência e pressa dos players. Pode sinalizar entrada de ordens a mercado (stops, notícias, grandes jogadores).

### 3. Médias de Negócios e Contratos
- **`AvgTradesPerSecond60s`:** Frequência base = `TotalNegocios60s / 60`.
- **`AvgContractsPerTrade60s`:** Tamanho médio do lote base nos últimos 60 segundos.
- **`AvgContractsPerTrade5s`:** Tamanho médio do lote imediato nos últimos 5 segundos.

### 4. Acima ou Abaixo da Média?
- **Fórmula:** `IsAboveAverage = (AvgContractsPerTrade5s > AvgContractsPerTrade60s)`
- **Interpretação:**
  - **Acima** → Lotes grandes estão dominando o curto prazo = **Institucionais/Baleias ativos**.
  - **Abaixo** → Lotes menores = **Varejo / Robôs de Alta Frequência (HFTs)** dominando o tape.

---

## 📊 Estratégia de Consulta (QuestDB)

Uma **única query** com agregação condicional por janela de tempo, sem múltiplos round-trips ao banco:

```sql
SELECT
    -- Janela de 5 Segundos (urgência e apetite imediato)
    sum(case when trade_type = '2' and timestamp > dateadd('s', -5, now()) then quantity else 0 end)  as compra_5s,
    sum(case when trade_type = '3' and timestamp > dateadd('s', -5, now()) then quantity else 0 end)  as venda_5s,
    count(case when timestamp > dateadd('s', -5, now()) then 1 else null end)                         as negocios_5s,

    -- Janela de 15 Segundos (confirmação de tendência)
    sum(case when trade_type = '2' and timestamp > dateadd('s', -15, now()) then quantity else 0 end) as compra_15s,
    sum(case when trade_type = '3' and timestamp > dateadd('s', -15, now()) then quantity else 0 end) as venda_15s,
    count(case when timestamp > dateadd('s', -15, now()) then 1 else null end)                        as negocios_15s,

    -- Janela de 60 Segundos (baseline)
    sum(case when trade_type = '2' then quantity else 0 end)                                          as compra_60s,
    sum(case when trade_type = '3' then quantity else 0 end)                                          as venda_60s,
    count()                                                                                           as negocios_60s,

    -- Médias de lote (tamanho médio por trade)
    avg(case when timestamp > dateadd('s', -5, now()) then quantity else null end)                    as media_lotes_5s,
    avg(quantity)                                                                                     as media_lotes_60s

FROM trades
WHERE ticker = @ticker
  AND timestamp > dateadd('s', -60, now());
```

> **Performance:** 1 único *table scan* na memória quente do QuestDB (latência < 2ms).
> Os valores de `trade_type = '2'` representam agressão de compra, `'3'` representa agressão de venda.

---

## 💻 Arquitetura de Software

| Arquivo | Camada | Responsabilidade |
|---|---|---|
| `Models/TapeSpeedMetrics.cs` | Domain | DTOs `TapeSpeedRawData` e `TapeSpeedAnalysis`. Enums `MarketState` e `Appetite`. |
| `Services/TapeSpeedAnalyzer.cs` | Business Logic | Classe pura e testável. Recebe o dado cru e aplica as 4 regras de classificação. |
| `Services/TapeSpeedDbClient.cs` | Data Access | Executa a query otimizada no QuestDB via Npgsql. |
| `Services/TapeSpeedEngine.cs` | Worker | `BackgroundService` que cicla a cada 1s, dispara `OnAnalysisUpdated` (event) para assinantes (UI, SignalR). |
| `CoreExtensions.cs` | DI Registration | Registra todos os serviços acima como `Singleton` + `HostedService`. |

---

## 🔄 Fluxo de Dados (Runtime)

```
QuestDB (trades)
      │
      │  1× query / segundo
      ▼
TapeSpeedDbClient.GetRawTapeSpeedDataAsync()
      │
      │  TapeSpeedRawData
      ▼
TapeSpeedAnalyzer.Analyze()
      │
      │  TapeSpeedAnalysis { Appetite, MarketState, Médias, IsAboveAverage }
      ▼
TapeSpeedEngine._latestAnalyses[ticker]   ← Cache em memória (ConcurrentDictionary)
      │
      ├──▶ OnAnalysisUpdated (event) ──▶ OrbitalUI ViewModel / SignalR Hub
      └──▶ GetLatestAnalysis(ticker)  ──▶ Consulta por pull (ex: API REST)
```

---

## 🔧 Configuração Disponível (TapeSpeedEngine)

| Parâmetro | Valor Padrão | Descrição |
|---|---|---|
| `_tickersToMonitor` | `{ "WDOFUT", "WING15" }` | Lista de ativos monitorados |
| `_refreshInterval` | `1 segundo` | Frequência de varredura no QuestDB |

---

## ✅ Status de Implementação

| Tarefa | Status |
|---|---|
| `TapeSpeedMetrics.cs` (DTOs + Enums) | ✅ Implementado |
| `TapeSpeedAnalyzer.cs` (Regras de Negócio) | ✅ Implementado |
| `TapeSpeedDbClient.cs` (Query QuestDB) | ✅ Implementado |
| `TapeSpeedEngine.cs` (BackgroundService + Event) | ✅ Implementado |
| `CoreExtensions.cs` (Registro DI) | ✅ Implementado |
| `Program.cs` (Demo Console ao vivo) | ✅ Implementado |
| Validação com dados reais (runtime) | ⏳ Aguardando execução local |

---

## 🧪 Como Validar Localmente

1. Encerrar qualquer execução/debug ativa da solução no **Visual Studio**.
2. Executar via terminal na raiz do projeto:
```bash
dotnet run --project Orbital.Core\Orbital.Core.csproj
```
3. O console exibirá, segundo a segundo, a temperatura do mercado:
```
=====================================================
           MONITOR EM TEMPO REAL: TAPE SPEED
=====================================================
Ativo: WDOFUT | Hora: 14:55:03.123
-----------------------------------------------------
1. Apetite do Mercado (Urgência):   🟢 COMPRA
2. Estado do Mercado:                🔥 AGITADO (Forte Fluxo)
3. Frequência de Negócios:           12,40 trades/seg
   Média de Contratos (Baseline 60s): 3,2 contratos/trade
   Média de Contratos (Imediata 5s):   6,8 contratos/trade
4. Volume por Negócio Atual:         ▲ ACIMA DA MÉDIA (Institucionais Ativos)
=====================================================
```
