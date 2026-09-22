# 🗄️ Marketdata.Database

O projeto **Marketdata.Database** é a biblioteca de persistência de séries temporais de dados de mercado (Market Data) para o ecossistema Orbital. Sua função primária é registrar em tempo real e de forma ultra eficiente os negócios (Trades) e metadados vindos do fluxo streaming da `MarketData.API`, armazenando-os para análises históricas, consultas futuras e inteligência quantitativa (Backtesting).

## 🚀 Arquitetura & Escolha Tecnológica: QuestDB

Para suportar a altíssima frequência de dados de mercado (tick-by-tick), adotamos o **QuestDB** como mecanismo de banco de dados para séries temporais (TSDB - Time-Series Database).

### Por que QuestDB?
1. **Performance Extrema:** Ingestão de milhões de linhas por segundo com uso de recursos otimizado.
2. **Consultas Rápidas:** Extensões SQL focadas em tempo de série (ex: `SAMPLE BY`).
3. **Protocolo InfluxDB Line Protocol (ILP):** Ingestão ultra rápida via protocolo nativo sobre rede TCP, evitando overheads do protocolo HTTP ou drivers ORM pesados.

---

## 🛠️ Detalhes da Implementação

### 1. Camada de Cliente (`Client`)
* **`IQuestDbClient`**: Contrato para a publicação assíncrona de dados.
* **`QuestDbPublisher`**: Implementação que encapsula o driver oficial do QuestDB (`QuestDB.Senders`). Garante **Thread Safety** no contexto concorrente do background service utilizando um `SemaphoreSlim` durante a inicialização do socket TCP/ILP e envios.

### 2. Dicionário de Dados & Tabelas
* **`trades`**: Dados tick-by-tick dos negócios executados na B3.
* **`agents`**: Tabela de dimensão/referência mapeando o `agent_id` da corretora para seu nome descritivo legível (`agent_name`).
* **`trade_types`**: Tabela de dimensão contendo os códigos descritivos de agressão padrão Nelogica/B3.

### 3. Registro no Container DI (`DependencyInjection`)
O projeto fornece uma extensão de `IServiceCollection` para facilitar o acoplamento:
```csharp
services.AddMarketDataPersistence(Configuration);
```

---

## ⚙️ Configuração no Consumidor (`appsettings.json`)

Para ativar e conectar ao banco, configure a seção `QuestDB` no `appsettings.json` da aplicação host (como a `MarketData.API`):

```json
"QuestDB": {
  "Host": "localhost",
  "IlpPort": 9009,
  "Enabled": true
}
```

---

## 🏎️ Pipeline Não-Bloqueante (Non-Blocking Batching)

Em produção, o cliente do banco de dados é consumido pelo `PersistenceWorker` da API de Market Data.
* **Canal Concorrente:** Os dados entram em um `System.Threading.Channels.Channel<T>` assíncrono e não-bloqueante no callback do stream de mercado.
* **Consumo em Lote (Batching):** O `PersistenceWorker` agrupa os registros e faz a publicação assíncrona em blocos de até **1.000 registros** a cada **1 segundo** (ou conforme o canal encher), otimizando a latência de rede sem competir com o pipeline real-time de entrega do SignalR.

---

## ▶️ Como Executar e Utilizar

Como este projeto é uma **Biblioteca de Classes (Class Library)**, ele não é executado de forma autônoma. Siga as instruções abaixo para preparar o ambiente e rodar junto ao seu projeto principal (API ou Worker).

### 1. Subindo a Infraestrutura (QuestDB)
É obrigatório ter uma instância do QuestDB rodando para que a camada de persistência e os Seeders funcionem. A maneira mais fácil é via Docker:

```bash
docker run -p 9000:9000 -p 9009:9009 -p 8812:8812 -p 9003:9003 questdb/questdb:latest
```
- **9000:** Painel Web e API REST (Acesse `http://localhost:9000` no navegador).
- **9009:** Porta ILP (InfluxDB Line Protocol) para ingestão rápida de trades.
- **8812:** Porta Postgres Wire Protocol (PGWire) para consultas Npgsql e operações `DELETE/INSERT`.

### 2. Alimentando Tabelas Estáticas (Seed)
A biblioteca inclui scripts de carga inicial para dados estáticos (Agentes, SMC). Para populá-los no seu banco, chame as classes `Seeder` no momento do startup da sua aplicação (por exemplo, no `Program.cs` ou `BackgroundService` da sua API principal):

```csharp
// Exemplo em um Worker ou Host:
var connectionString = "Host=localhost;Port=8812;Database=qdb;Username=admin;Password=quest";

// Injetando dicionário de corretoras (Agentes)
var agentSeeder = new AgentDataSeeder(logger);
await agentSeeder.SeedAgentesAsync(connectionString);

// Injetando Pontos de Interesse (SMC) fixos no banco
var smcSeeder = new SMCDataSeeder(loggerSMC);
await smcSeeder.SeedPointsOfInterestAsync(connectionString);
```

### 3. Build do Projeto
Para garantir que o projeto compile sem erros de dependência:
```bash
cd Marketdata.Database
dotnet build
```
