# Plano de Implementação: Persistência de Dados (QuestDB)

Este documento detalha o plano para persistir dados tick-by-tick e book snapshots dos ativos configurados na Bússola (WDO/DOL) utilizando o banco de dados **QuestDB**.

## 1. Objetivos
* Persistir todos os negócios (Trades) em tempo real.
* Garantir latência zero no pipeline principal de distribuição (SignalR).
* Permitir análise retrospectiva e backtesting com dados reais do ambiente.

## 2. Arquitetura de Dados (QuestDB)

### 2.1. Tabela: `trades`
Responsável por armazenar cada negócio executado.
| Coluna | Tipo | Descrição |
| :--- | :--- | :--- |
| `timestamp` | TIMESTAMP | Horário exato do negócio (Designated Timestamp) |
| `ticker` | SYMBOL | Símbolo do ativo (ex: WDOFUT) |
| `price` | DOUBLE | Preço da execução |
| `quantity` | LONG | Quantidade negociada |
| `side` | SYMBOL | Agressor (COMPRA, VENDA, LEILAO) |
| `buy_agent` | SYMBOL | Nome da corretora compradora (ex: XP, UBS, BTG) |
| `sell_agent` | SYMBOL | Nome da corretora vendedora (ex: XP, UBS, BTG) |
| `trade_type`| INT    | Código interno da DLL para o tipo de agressão |
| `trade_number`| LONG  | ID único do trade gerado pela B3 |
| `is_auction`| BOOLEAN | Indica se o trade ocorreu durante leilão |

### 2.2. Tabela: `agents` (Referência)
Mapeamento de IDs de corretoras para nomes legíveis.
| Coluna | Tipo | Descrição |
| :--- | :--- | :--- |
| `agent_id` | INT | ID numérico (ex: 85) |
| `agent_name` | SYMBOL | Nome da corretora (ex: BTG PACTUAL) |

### 2.3. Tabela: `trade_types` (Referência)
Descrição dos tipos de agressão capturados.
| Coluna | Tipo | Descrição |
| :--- | :--- | :--- |
| `type_id` | INT | Código do tipo de trade |
| `description` | SYMBOL | Descrição (COMPRA, VENDA, LEILAO, DIRETO) |

## 3. Plano de Implementação

### Fase 1: Infraestrutura na MarketData.API
1. **Configuração:** Adicionar seção `QuestDB` no `appsettings.json` (Host, Porto ILP, Enabled).
2. **Cliente ILP:** Implementar um `QuestDBPublisher` utilizando o protocolo TCP/ILP (Line Protocol) para máxima eficiência.
3. **Background Service:** Criar o `PersistenceWorker` que consome uma `Channel<T>` interna para evitar bloqueios.

### Fase 2: Captura de Dados
1. No `MarketDataWorker.cs`, interceptar o callback da ProfitDLL (`TradeCallback`).
2. Publicar os dados brutos no `Channel` de persistência apenas para os ativos marcados para monitoramento.

### Fase 3: Integração com Configurações
1. A API deve ler os ativos "MINI" e "CHEIO" configurados para priorizar a persistência destes.

### Fase 4: Sincronização de Metadados (Agents & Types)
1. **Auto-Discovery de Agentes:** Implementar verificação no `QuestDBPublisher`: se o `AgentID` não existir no cache local, buscar nome na DLL e persistir na tabela `agents`.
2. **Bootstrap de Types:** Na primeira execução, popular a tabela `trade_types` com o dicionário padrão da Nelogica (0=Indefinido, 1=Compra, 2=Venda, etc).

## 4. Considerações de Performance
* **Batching:** O `PersistenceWorker` deve agrupar múltiplos registros antes de enviar ao QuestDB.
* **Non-blocking:** Se o buffer do Channel encher, o sistema deve descartar logs de persistência em favor da manutenção do streaming em tempo real (prioridade zero).

## 5. Próximos Passos
1. Validar a conectividade com a instância do QuestDB do usuário.
2. Criar as tabelas no QuestDB via console/SQL.
3. Implementar a classe `QuestDBPublisher` na API.
