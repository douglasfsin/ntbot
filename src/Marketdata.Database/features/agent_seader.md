# Carga e Atualização de Agentes e Dados Estáticos (QuestDB)

Esta documentação detalha a estratégia de inserção, atualização (Seeding) e manutenção de dados estáticos do ecossistema financeiro Orbital no banco de dados QuestDB.

## 1. Visão Geral do Desafio com QuestDB

O QuestDB é um banco de dados temporal (*Time-Series Database*) focado em operações de alta performance e *append-only*. Por conta disso, ele **não suporta restrições tradicionais** (como `PRIMARY KEY`) nem comandos nativos de `UPSERT / ON CONFLICT` ou `UPDATE` transacional tradicional em tempo real sobre tabelas pesadas.

Para atualizarmos dados estáticos e de dicionário (como a lista de corretoras ou tipos de trade), utilizamos o **PostgreSQL Wire Protocol (PGWire)** conectado na porta padrão `8812` do QuestDB. A estratégia oficial do projeto para contornar a limitação de Upsert consiste na sequência:
1. `DELETE FROM tabela WHERE id = @id;` (Apaga registro anterior, se existir).
2. `INSERT INTO tabela (id, nome) VALUES (@id, @nome);` (Insere o novo valor).

## 2. AgentDataSeeder: Como Atualizar os Agentes

No projeto `Marketdata.Database`, foi criada a classe utilitária `AgentDataSeeder`. O objetivo primário desta classe é popular a tabela `agentes_metadata` com as corretoras da B3.

### 2.1 Adicionando ou Modificando Novos Agentes

Para atualizar os agentes de mercado do sistema, o desenvolvedor não precisa criar scripts manuais. Basta atualizar o dicionário estático no topo do método `SeedAgentesAsync`, localizado no arquivo `AgentDataSeeder.cs`:

```csharp
var agentes = new List<AgentMetadata>
{
    new(85, "XP INVESTIMENTOS CCTVM S/A", "XP"),
    new(27, "TULLETT PREBON BRASIL CVC S/A", "TULLETT"),
    new(16, "J.P. MORGAN CCVM S.A.", "JP MORGAN"),
    new(3, "XP INVESTIMENTOS (Antiga)", "XP OLD"), // Exemplo de inclusão de novo agente
};
```

### 2.2 Como e Onde Invocar a Carga

A carga de dados estáticos é custosa e não deve rodar a todo instante. É recomendado acionar este método **sob demanda** (num pipeline de deploy, num painel administrativo) ou de forma **única na inicialização da API** (apenas durante manutenções).

**Exemplo de Invocação:**
```csharp
// Exemplo de Invocação usando Injeção de Dependências ou CLI
var seeder = new AgentDataSeeder(logger);
var pgWireConnectionString = "Host=localhost;Port=8812;Database=qdb;Username=admin;Password=quest";
await seeder.SeedAgentesAsync(pgWireConnectionString);
```

> **Nota de Atenção:** A connection string para Npgsql (`PGWire`) exige as portas nativas do PostgreSQL, que no QuestDB mapeiam tipicamente para a porta `8812`, enquanto o ILP (*InfluxDB Line Protocol*) roda na porta `9009` e o REST na porta `9000`.

## 3. Estratégia Para Demais Dados Estáticos (Trade Types / Trade Sides)

O banco QuestDB do Orbital possui outros domínios de dados estáticos fundamentais:
- **`trade_types`**: O dicionário de traduções dos tipos de agressão e de operações da B3 (Leilão, Cross, RLP, Direto).
- **`trade_side`**: Dicionário simples da ponta agressora (1 = Compra, 2 = Venda, 3 = Direto, 4 = Leilão).

### 3.1 Carga via Scripts Externos (Python / Bash)

Se os dados forem grandes planilhas e arquivos Markdown (`trade_types.md`), a estratégia homologada é utilizar pequenos scripts via **API REST** (Porta `9000`).
Na raiz do projeto, temos o uso de scripts (como `populate_types.py`) que processam arquivos físicos e enviam os dados de carga usando o endpoint `/exec`:

```bash
http://localhost:9000/exec?query=INSERT INTO trade_types (type_id, description, comment) VALUES ...
```

### 3.2 Carga via .NET (Evolução do Seeder)

Caso seja do escopo da plataforma C# evoluir para gerenciar automaticamente todos os domínios da B3, os passos são simples:
1. Copie o padrão do método `SeedAgentesAsync` para novos métodos como `SeedTradeTypesAsync()`.
2. Siga o mesmo padrão SQL com Npgsql:
   `DELETE FROM trade_types WHERE type_id = @id;`
   `INSERT INTO trade_types ...`
3. Inclua a chamada dos métodos no mesmo processo de boot inicial que já roda os agentes.

---
A persistência destes domínios estáticos é fundamental para os motores de agregação de *Market Data*, pois permitem que as *Views* e Dashboards do ecossistema substituam visualmente `Agent_ID: 85` para `XP`, trazendo legibilidade sem a necessidade da API instanciar a mesma consulta em tempo real (pois as pontas de frontend farão os *JOINs* direto no QuestDB).
