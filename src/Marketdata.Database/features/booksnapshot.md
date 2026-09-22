# Feature: Book Snapshot & Persistência (QuestDB)

## Objetivo
Implementar a captura e gravação de um "Snapshot" do Livro de Ofertas e Livro de Preços a partir da integração com a ProfitDll, sem engessar a periodicidade em limites rígidos (como 1s), preservando recursos computacionais e garantindo escalabilidade no armazenamento com o banco temporal QuestDB.

## 1. Fonte de Dados (`ProfitDll`)
A ProfitDll disponibiliza os dados em duas vertentes:

1. **Book de Preços (`TPriceBookCallback`)**: Retorna agregados por nível de preço (`TConnectorPriceGroup`), exibindo volume acumulado e quantidade de ofertas pendentes no respectivo preço.
2. **Book de Ofertas (`TOfferBookCallback`)**: Retorna dados discriminados (`TConnectorOffer`), detalhando qual Player Institucional (Corretora/Agent) ofertou qual volume, em que fila/preço.

## 2. Arquitetura Proposta
- **C# Worker/Service**: Responsável por ler a memória em cache (`DataStore.OfferBuy`, `DataStore.OfferSell` e o *Price Depth*) do C++ de forma não-bloqueante (*lock-free* ou com *Double Buffering*).
- **Throatling/Debounce Engine**: Componente responsável por gerenciar o volume de inserções no banco. Como o mercado pode oscilar centenas de vezes por milissegundo, esse engine aplicará um *Rate Limit/Debounce*, aglutinando os dados da mesma janela temporal ou gravando apenas a variação delta.
- **QuestDB (Influx Line Protocol)**: Um repositório temporal (Time-Series) focado em altíssima performance de inserção e agrupamentos estatísticos posteriores.

## 3. Modelo do Banco de Dados (`book_snapshots`)
Para assegurar consultas otimizadas, a tabela utilizará partições diárias e o formato de Write-Ahead Logging (WAL).

```sql
CREATE TABLE book_snapshots (
    ticker SYMBOL capacity 256,
    side SYMBOL capacity 2,         -- 'B' para Buy, 'S' para Sell
    position INT,                   -- Posição na fila de nível de preço/oferta
    price DOUBLE,                   -- Nível do preço ofertado
    quantity LONG,                  -- Tamanho da oferta pendente
    order_count INT,                -- Qtd de pedidos nesse preço (PriceBook)
    agent INT,                      -- ID da corretora institucional (OfferBook)
    timestamp TIMESTAMP
) timestamp(timestamp) PARTITION BY DAY WAL;
```

## 4. Impacto e Riscos Analisados

1. **Overhead na Thread do C++ / Memória**
   * **Risco**: Paralelizar cópias pesadas da estrutura do book no momento em que a corretora está injetando lotes de mercado causará enfileiramento (lag) de execução dos callbacks do ProfitDll.
   * **Mitigação**: O `DataStore` ou o conector devem usar estruturas de dados Thread-Safe ou mecanismos de cópia atômica (Array Snapshotting), de forma que o envio assíncrono pro QuestDB não concorra com o lock de preenchimento.

2. **Inundação de Dados no QuestDB**
   * **Risco**: Salvar eventos unitários de book pode disparar milhões de inserções em poucos minutos.
   * **Mitigação**: Implementação de *Bulk Insert* através do componente `ILineSender` do InfluxDB, abrindo uma conexão socket TCP/UDP e despejando um lote (array de `LineProtocol`) periodicamente, minimizando as rodadas de rede.

## 5. Casos de Uso Posteriores
O acúmulo desses dados serve como fundação (Inteligência) para as seguintes análises macro:
- **Big Players Tape Reading**: Análise preditiva de paredes de lote criadas por players institucionais vs Real Intenção.
- **Microestrutura de Mercado**: Calculo do "Iceberg effect" da microestrutura.
- **Densidade (Liquidity Heatmap)**: Gráficos de calor com poços de liquidez que atraem a Vwap ou a cotação futura.
