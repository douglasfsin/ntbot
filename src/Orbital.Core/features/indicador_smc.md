# 🚀 Plano de Implementação: Motor Analítico SMC & Order Flow (Orbital)

Este documento descreve a especificação técnica, a divisão de responsabilidades entre os projetos locais e a lógica de implementação para os conceitos de **Smart Money Concepts (SMC)** e **Order Flow**. A arquitetura utiliza o **QuestDB** como camada de persistência/pesquisa histórica e o **SignalR** como despachante de eventos em tempo real.

---

## 🏢 1. Arquitetura do Ecossistema e Divisão por Projetos

Para garantir a blinda de performance, baixa latência e evitar gargalos de memória na API e na interface, o sistema adota um modelo descentralizado de responsabilidades (CQRS Local):

+----------------------------+
|         ProfitDLL          |
+----------------------------+
|
| (Fluxo de Gravação - ILP)
v
+----------------------------+
|   marketData.database      | <--- Persistência de alta frequência (QuestDB)
+----------------------------+
|
| (Consultas SQL Otimizadas / PGWire)
v
+----------------------------+
|        Orbital.Core        | <--- Processamento Pesado, Regras SMC e Timers
+----------------------------+
|
| (Eventos Mastigados via SignalR Hub)
v
+----------------------------+
|         Orbital.UI         | <--- Dashboard Web Clean, Alertas e Front-End
+----------------------------+


### 📁 1.1. Perfil de cada Projeto
* **`marketData.database`:** Camada de infraestrutura pura. Gerencia a gravação de dados via protocolo **ILP** (*InfluxDB Line Protocol*) [cite: 583] e expõe interfaces de leitura utilizando o driver leve `Npgsql` (PostgreSQL Wire Protocol) para consultas.
* **`Orbital.Core`:** Biblioteca de classes em .NET 8/9. Centraliza toda a inteligência analítica de SMC. Roda *BackgroundServices* temporais que consultam o QuestDB, processa e valida as faixas de preço e gerencia o ciclo de vida das zonas de interesse na memória.
* **`Orbital.UI`:** Aplicação Blazor, React ou similar. Atua apenas como uma casca visual estritamente *event-driven*. Não possui lógica de mercado ou conexões pesadas, limitando-se a renderizar os paylods analíticos finais via SignalR.

---

## 📌 2. Mapeamento de Conceitos SMC via QuestDB SQL

Para evitar sobrecarga na memória RAM da aplicação principal, os cálculos mais pesados de volumetria histórica e agrupamentos de preços são delegados diretamente ao motor de banco de dados do **QuestDB**.

### 2.1. Detecção de Order Blocks (OB / Zonas de Absorção)
Um *Order Block* institucional se caracteriza por uma alta concentração de agressões em uma faixa estreita de preço onde o mercado não consegue se deslocar (absorção passiva). 

O projeto `Orbital.Core` executará periodicamente consulta no banco para varrer e encontrar essas anomalias para o ativo `WDOFUT` exemplo de consulta SQL:

```sql
SELECT 
    price,
    sum(case when trade_type = '2' then quantity else 0 end) as vol_compra,
    sum(case when trade_type = '3' then quantity else 0 end) as vol_venda,
    count() as total_ticks
FROM trades
WHERE ticker = 'WDOFUT' 
  AND timestamp > now() - 5000000 -- Varre apenas a janela dos últimos 5 segundos
GROUP BY price
HAVING total_ticks > 15 AND (vol_compra > 1000 OR vol_venda > 1000);

2.2. Liquidity Sweep (Captura de Stops por Exaustão)
O Orbital.Core busca picos abruptos na média móvel acumulada de 1 segundo de agressões que ocorram exatamente nas extremidades (máximas ou mínimas) geradas nas últimas horas pelo QuestDB. Se houver o estouro do volume de 1s seguido de um sumiço imediato da liquidez no book de ofertas na mesma faixa, o Core valida a captura e projeta a zona de rejeição.

⚙️ 3. Engenharia de Lógica no Orbital.Core
As zonas detectadas pelas consultas ao banco de dados são convertidas em modelos de domínio leves (PriceZone) e retidas na memória RAM do Orbital.Core para monitoramento dinâmico de proximidade do preço atual de tela.

C#
// Localizado em: Orbital.Core / Models / PriceZone.cs
public record PriceZone
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Type { get; init; } = "OrderBlock"; // OrderBlock ou LiquiditySweep
    public string Side { get; init; } = "Compra";      // Compra ou Venda
    public double HighPrice { get; init; }
    public double LowPrice { get; init; }
    public double TrackedVolume { get; init; }
    public bool IsActive { get; set; } = true;
}

// Localizado em: Orbital.Core / Services / ZoneInterestManager.cs
public class ZoneInterestManager
{
    private readonly List<PriceZone> _activeZones = new();

    // Sincroniza em memória as zonas calculadas pelo QuestDB
    public void UpdateZonesFromQuery(IEnumerable<PriceZone> freshZones)
    {
        lock (_activeZones)
        {
            _activeZones.Clear();
            _activeZones.AddRange(freshZones);
        }
    }

    // Chamado a cada tick da ProfitDLL para validar se o preço está testando uma POI
    public PriceZone? CheckCollision(double lastMarketPrice)
    {
        lock (_activeZones)
        {
            return _activeZones.FirstOrDefault(z => z.IsActive && 
                                                    lastMarketPrice >= z.LowPrice && 
                                                    lastMarketPrice <= z.HighPrice);
        }
    }
}
📡 4. Comunicação de Alertas (Orbital.Core ➔ Orbital.UI)
Para garantir que o front-end permaneça limpo, fluido e responsivo, o Orbital.Core filtra, calcula a distância em ticks e empacota as informações em payloads simplificados antes de disparar pelo hub do SignalR.

Exemplo de Payload de Proximidade (Enviado a cada 100ms/Throttling):
JSON
{
  "event": "POI_PROXIMITY",
  "ticker": "WDOFUT",
  "currentPrice": 5052.5,
  "zoneType": "OrderBlock",
  "zoneSide": "Compra",
  "targetPrice": 5051.0,
  "distanceTicks": 3,
  "strength": "ALTA"
}
📋 5. Cronograma de Desenvolvimento & Tasks
[ ] Fase 1: Infraestrutura e Ajustes no marketData.database

[ ] Garantir que a tabela trades_stream no QuestDB utilize o tipo SYMBOL para a coluna ticker.

[ ] Validar a tabela com suporte a WAL (PARTITION BY DAY WAL;) para concorrência de alta frequência.

[ ] Implementar classe utilitária de conexão via driver Npgsql.

[ ] Fase 2: Motores de Cálculo e Lógica no Orbital.Core

[ ] Criar o BackgroundService com instanciador de timers para rodar as queries de agrupamento de SMC.

[ ] Desenvolver o ZoneInterestManager com travas de concorrência (lock ou ReaderWriterLockSlim).

[ ] Escrever o algoritmo de contagem de ticks por profundidade (janelas de 10% a 100%) rodando direto na RAM para os dados do livro.

[ ] Fase 3: Distribuição via SignalR & Dashboard (Orbital.UI)

[ ] Configurar o MarketDataHub no Core estruturando os grupos de transmissão por Ticker.

[ ] Implementar mecanismo de controle de fluxo (Throttling) de mensageria para não sobrecarregar a UI.

[ ] Desenvolver os painéis e cartões visuais em Dark Mode na UI para receber os alertas de mitigação e sweeps de mercado.

⚠️ 6. Diretrizes Críticas de Performance Financeira
Sem alocações no Laço Crítico: Não utilize LINQ (.Where, .Any, .Select) dentro dos laços que processam os ticks do livro ou os cálculos de 1 segundo. Use loops for tradicionais.

Consultas Não-Bloqueantes: As queries pesadas de SMC feitas no QuestDB devem rodar de forma puramente assíncrona (ExecuteReaderAsync) no Core para nunca congelar as threads de recepção da ProfitDLL.