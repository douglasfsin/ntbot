# Spec: MNQInstitutionalFlowAnalyzer (NinjaTrader 8)

Este arquivo é a **especificação-fonte** do indicador NinjaTrader 8 `MNQInstitutionalFlowAnalyzer`.

A implementação no repositório fica em `Indicadores/` na raiz (`Indicadores/MNQInstitutionalFlowAnalyzer/`). **Não** é um indicador Profit Chart.

Para o NinjaScript Editor: copie **somente** `MNQInstitutionalFlowAnalyzer.cs` + `Engines/` + `Helpers/` + `Math/` + `Models/` (`.cs`). **Nunca** `Core/` (`.csproj` / `obj` / AssemblyInfo gerado), `Tests/` ou `AssemblyInfo.cs` — isso duplica atributos do Custom (CS0579).

---

# Criar indicador institucional MNQ — Wyckoff + Order Flow + Correlação + Weighted Flow Score

Você é um desenvolvedor especialista em **C#**, **NinjaTrader 8**, **NinjaScript**, **Order Flow**, **Market Microstructure**, **Wyckoff**, **Volume Profile**, **Cumulative Delta** e análise quantitativa de futuros.

Crie um indicador profissional para NinjaTrader 8 chamado:

`MNQInstitutionalFlowAnalyzer`

O objetivo é criar um sistema de análise de fluxo para o **MNQ — Micro E-mini Nasdaq-100 Futures**, combinando:

1. Wyckoff
2. Volume agressor Bid/Ask
3. Delta
4. Absorção
5. Exaustão
6. Imbalance
7. Volume relativo
8. Estrutura de preço
9. VWAP
10. Correlação entre MNQ/NQ, ES e RTY
11. Regime de volatilidade
12. Score ponderado de fluxo
13. Detecção de divergências
14. Classificação do contexto de mercado
15. Sinais de provável acumulação, distribuição, markup, markdown, spring e upthrust.

O indicador NÃO deve simplesmente copiar o Order Flow Cumulative Delta nativo.

Ele deverá possuir uma camada própria de processamento e normalização dos dados.

---

# 1. OBJETIVO PRINCIPAL

Criar um indicador que responda:

* Quem está controlando o mercado?
* Compradores ou vendedores?
* A agressão está sendo absorvida?
* O preço está confirmando o fluxo?
* Existe divergência preço × delta?
* Existe confirmação pelo ES?
* Existe confirmação pelo NQ?
* Existe confirmação pelo RTY?
* O movimento atual é tendência, acumulação, distribuição ou exaustão?
* Existe sinal de Wyckoff?
* Qual é a força do fluxo?

O resultado principal deve ser:

`InstitutionalFlowScore`

Variando de:

`-100 até +100`

Onde:

+100 = pressão compradora extrema

0 = equilíbrio

-100 = pressão vendedora extrema

---

# 2. ARQUITETURA

Não colocar toda a lógica dentro de uma única classe.

Criar componentes separados:

`MNQInstitutionalFlowAnalyzer.cs`

`OrderFlowEngine.cs`

`WyckoffEngine.cs`

`CorrelationEngine.cs`

`AbsorptionEngine.cs`

`ImbalanceEngine.cs`

`VolumeEngine.cs`

`MarketRegimeEngine.cs`

`FlowScoreEngine.cs`

`SignalEngine.cs`

`MarketStructureEngine.cs`

Sempre que possível utilizar classes internas ou arquivos separados compatíveis com NinjaScript.

Não criar dependências externas desnecessárias.

---

# 3. DADOS PRINCIPAIS

Utilizar:

* Last
* Bid
* Ask
* Last volume
* Bid volume
* Ask volume
* Delta
* Cumulative Delta
* Volume por preço
* VWAP
* ATR
* High/Low
* Open/Close
* Previous High/Low
* Session High/Low
* Market Depth quando disponível

Utilizar `OnMarketData()` para processamento de Level 1.

Utilizar `OnMarketDepth()` somente quando a funcionalidade de Market Depth estiver habilitada.

Não assumir que `OnMarketDepth()` e `OnMarketData()` ocorrerão sempre em uma ordem específica.

Criar proteção contra eventos fora de sequência.

---

# 4. BID/ASK AGGRESSION

Implementar classificação de agressão.

Compra agressora:

Trade executado no Ask.

Venda agressora:

Trade executado no Bid.

Calcular:

`AggressiveBuyVolume`

`AggressiveSellVolume`

`AggressiveDelta`

Fórmula:

`AggressiveDelta = AggressiveBuyVolume - AggressiveSellVolume`

Também calcular:

`AggressionRatio`

Exemplo:

`AggressionRatio = BuyVolume / max(SellVolume, 1)`

Normalizar o resultado para evitar valores extremos.

---

# 5. VALIDAÇÃO DO VOLUME

Implementar proteção contra dados inválidos.

Nunca assumir que:

`TotalVolume = BidVolume + AskVolume`

sem validar.

Criar:

`VolumeConsistencyCheck`

Verificar:

* volume total
* bid volume
* ask volume
* agressão
* trades classificados
* possíveis eventos duplicados
* valores negativos
* spikes impossíveis
* volumes zerados
* alterações anormais

Criar:

`DataQualityScore`

de 0 a 100.

Se a qualidade dos dados estiver abaixo de um limite configurável, reduzir o peso do Order Flow no score final.

---

# 6. DELTA

Calcular:

`BarDelta`

`SessionDelta`

`RollingDelta`

`DeltaAcceleration`

`DeltaMomentum`

`DeltaZScore`

Detectar:

### Delta positivo

Compradores agressivos dominando.

### Delta negativo

Vendedores agressivos dominando.

### Divergência bullish

Preço faz novo low.

Delta não faz novo low.

### Divergência bearish

Preço faz novo high.

Delta não faz novo high.

---

# 7. ABSORPTION ENGINE

Criar detector de absorção.

Exemplo:

Grande agressão compradora:

`AggressiveBuyVolume >> média`

Mas:

`PriceChange ≈ 0`

Isso pode indicar:

`SELLER_ABSORPTION`

Da mesma maneira:

Grande agressão vendedora sem queda significativa:

`BUYER_ABSORPTION`

Calcular:

`AbsorptionScore`

de -100 a +100.

Regras:

Buyer absorption:

* agressão vendedora elevada
* preço não cai
* volume elevado
* suporte próximo

Seller absorption:

* agressão compradora elevada
* preço não sobe
* volume elevado
* resistência próxima

---

# 8. EXHAUSTION ENGINE

Detectar exaustão.

Exemplos:

Preço fazendo novo high.

Volume agressor diminuindo.

Delta diminuindo.

Isso deve gerar:

`BUYER_EXHAUSTION`

Preço fazendo novo low.

Agressão vendedora diminuindo.

Delta melhorando.

Gerar:

`SELLER_EXHAUSTION`

Calcular:

`ExhaustionScore`

---

# 9. IMBALANCE

Criar detector de imbalance.

Por preço:

Buy volume / Sell volume

Exemplo:

Se:

`AskVolume >= BidVolume * ImbalanceRatio`

classificar:

`BUY_IMBALANCE`

Se:

`BidVolume >= AskVolume * ImbalanceRatio`

classificar:

`SELL_IMBALANCE`

Parâmetro configurável:

`ImbalanceRatio = 3.0`

Permitir alteração pelo usuário.

Criar:

`StackedImbalance`

Detectar vários níveis consecutivos de imbalance.

---

# 10. WYCKOFF ENGINE

Criar classificação de contexto Wyckoff.

Estados:

```text
UNKNOWN
ACCUMULATION
MARKUP
DISTRIBUTION
MARKDOWN
TRADING_RANGE
SPRING
UPTHRUST
SIGN_OF_STRENGTH
SIGN_OF_WEAKNESS
TEST
ABSORPTION
EXHAUSTION
```

Não tentar identificar Wyckoff apenas pelo preço.

Combinar:

* estrutura
* volume
* delta
* agressão
* absorção
* rompimentos
* falhas de rompimento
* contexto de suporte/resistência

---

# 11. SPRING

Detectar possível Spring.

Condições:

1. preço rompe fundo anterior
2. volume aumenta
3. agressão vendedora aumenta
4. preço recupera rapidamente
5. delta começa a melhorar
6. fechamento volta para dentro do range

Gerar:

`SPRING_SCORE`

entre 0 e 100.

---

# 12. UPTHRUST

Detectar:

1. preço rompe topo anterior
2. agressão compradora elevada
3. volume elevado
4. preço não sustenta rompimento
5. fechamento volta para dentro do range
6. delta começa a deteriorar

Gerar:

`UPTHRUST_SCORE`

---

# 13. MARKET STRUCTURE

Detectar:

* Higher High
* Higher Low
* Lower High
* Lower Low
* Breakout
* Failed Breakout
* BOS
* CHoCH

Criar:

`StructureBias`

de -100 a +100.

---

# 14. VWAP

Calcular ou utilizar VWAP disponível.

Determinar:

Preço acima da VWAP:

viés comprador.

Preço abaixo:

viés vendedor.

Também analisar:

* distância da VWAP
* retorno para VWAP
* rejeição
* aceitação
* desvio padrão

Criar:

`VWAPScore`

---

# 15. VOLUME RELATIVO

Calcular:

`RelativeVolume`

Comparar volume atual com média dos últimos N períodos.

Parâmetro:

`VolumeLookback = 50`

Criar:

`VolumeZScore`

Identificar:

* volume normal
* volume elevado
* volume extremo

---

# 16. MARKET REGIME

Criar classificação:

```text
TREND_UP
TREND_DOWN
RANGE
HIGH_VOLATILITY
LOW_VOLATILITY
BREAKOUT
ACCUMULATION
DISTRIBUTION
```

Utilizar:

* ATR
* ADX se disponível
* VWAP
* estrutura
* volume
* delta

---

# 17. CORRELAÇÃO

O indicador deverá permitir adicionar instrumentos de confirmação.

Principal:

`MNQ`

Instrumentos secundários configuráveis:

`NQ`

`ES`

`RTY`

Opcional:

`YM`

`DXY`

`ZN`

`10Y Yield`

Não assumir que todos estarão disponíveis.

O usuário deverá conseguir habilitar/desabilitar cada instrumento.

---

# 18. CORRELAÇÃO MNQ × NQ

Calcular correlação rolling.

Parâmetro:

`CorrelationPeriod = 50`

Calcular:

`PearsonCorrelation`

entre retornos, e não simplesmente preços.

Classificar:

```text
>= 0.80  forte confirmação
0.50-0.79 confirmação moderada
0.20-0.49 fraca
-0.20 a +0.20 neutra
< -0.20 divergência
```

---

# 19. CORRELAÇÃO MNQ × ES

Calcular correlação rolling entre retornos do MNQ e ES.

Identificar:

### Confirmação bullish

MNQ subindo + ES subindo.

### Confirmação bearish

MNQ caindo + ES caindo.

### Divergência

MNQ subindo enquanto ES perde força.

Ou:

MNQ caindo enquanto ES permanece forte.

Criar:

`ESCrossMarketScore`

---

# 20. CORRELAÇÃO COM RTY

Utilizar RTY para detectar:

* confirmação de risk-on
* risk-off
* divergência de small caps

Criar:

`RTYCorrelationScore`

---

# 21. CORRELAÇÃO DINÂMICA

Não utilizar somente correlação.

Criar também:

`RelativeStrength`

Comparar retorno percentual dos ativos.

Exemplo:

MNQ +0.8%

ES +0.2%

RTY -0.4%

Interpretar:

MNQ apresenta liderança relativa.

Gerar:

`LeadershipScore`

---

# 22. WEIGHTED FLOW SCORE

Criar engine de pesos.

Pesos padrão:

```text
WyckoffStructure      = 25
Aggression             = 20
Delta                  = 15
Absorption             = 10
Imbalance              = 10
RelativeVolume         = 5
VWAP                   = 5
CrossMarketCorrelation = 5
MarketRegime           = 5
```

Total:

100%

Os pesos devem ser configuráveis.

Normalizar cada componente para:

`-100 até +100`

Depois:

```text
FinalScore =
    WyckoffScore * 0.25
  + AggressionScore * 0.20
  + DeltaScore * 0.15
  + AbsorptionScore * 0.10
  + ImbalanceScore * 0.10
  + VolumeScore * 0.05
  + VWAPScore * 0.05
  + CorrelationScore * 0.05
  + RegimeScore * 0.05;
```

Aplicar `DataQualityScore` como fator de confiança.

---

# 23. CONFIDENCE SCORE

Criar:

`ConfidenceScore`

0-100.

O score deve considerar:

* qualidade dos dados
* quantidade de confirmações
* divergências
* estabilidade do fluxo
* volatilidade
* correlação
* força do sinal

Exemplo:

```text
Score = +82
Confidence = 91
```

Interpretação:

BUY STRONG / HIGH CONFIDENCE

Mas:

```text
Score = +82
Confidence = 48
```

deve ser:

BUY BIAS / LOW CONFIDENCE

Não emitir sinal forte somente pelo score.

---

# 24. SIGNAL ENGINE

Criar estados:

```text
STRONG_BUY
BUY
WEAK_BUY
NEUTRAL
WEAK_SELL
SELL
STRONG_SELL
```

Adicionar:

```text
NO_TRADE
WAIT_CONFIRMATION
POSSIBLE_REVERSAL
BREAKOUT
FAILED_BREAKOUT
ABSORPTION
EXHAUSTION
```

---

# 25. FILTRO DE ENTRADA

Criar uma função:

`IsTradeSetupValid()`

Para compra, exigir configurable:

* Score >= 60
* Confidence >= 70
* estrutura bullish
* delta bullish
* agressão bullish
* sem forte absorção vendedora
* contexto Wyckoff compatível

Para venda:

* Score <= -60
* Confidence >= 70
* estrutura bearish
* delta bearish
* agressão bearish
* sem forte absorção compradora
* contexto Wyckoff compatível

Todos esses limites devem ser configuráveis.

---

# 26. DIVERGENCE ENGINE

Detectar:

### Bullish divergence

Preço ↓

Delta ↑

### Bearish divergence

Preço ↑

Delta ↓

Também detectar:

### Volume divergence

Preço ↑

Volume ↓

### Aggression divergence

Preço ↑

Agressão ↓

### Cross-market divergence

MNQ ↑

ES ↓

ou

MNQ ↓

ES ↑

---

# 27. DASHBOARD

Criar painel no gráfico.

Exemplo:

```text
┌─────────────────────────────────────────┐
│ MNQ INSTITUTIONAL FLOW                  │
├─────────────────────────────────────────┤
│ FLOW SCORE        +72                    │
│ CONFIDENCE         84                    │
│ SIGNAL       STRONG BUY                  │
├─────────────────────────────────────────┤
│ WYCKOFF            +80  ACCUMULATION     │
│ AGGRESSION         +76  BUYERS           │
│ DELTA              +68  BULLISH          │
│ ABSORPTION         -10  NEUTRAL          │
│ IMBALANCE          +74  BUY              │
│ VOLUME             +61  HIGH             │
│ VWAP               +45  ABOVE            │
├─────────────────────────────────────────┤
│ NQ CORRELATION     +0.92                 │
│ ES CORRELATION     +0.81                 │
│ RTY CORRELATION    +0.67                 │
├─────────────────────────────────────────┤
│ MARKET REGIME     TREND UP               │
│ STRUCTURE         HH / HL                │
│ WYCKOFF           SIGN OF STRENGTH       │
├─────────────────────────────────────────┤
│ ABSORPTION         NONE                  │
│ EXHAUSTION         LOW                   │
│ DIVERGENCE         NONE                  │
└─────────────────────────────────────────┘
```

---

# 28. HISTOGRAMA

Criar painel inferior com:

`InstitutionalFlowScore`

Linha:

`+60`

`0`

`-60`

Coloração dinâmica.

Também plotar:

`ConfidenceScore`

---

# 29. MARCADORES NO GRÁFICO

Quando detectar eventos:

Spring:

`SPRING`

Upthrust:

`UT`

Absorção compradora:

`BA`

Absorção vendedora:

`SA`

Divergência bullish:

`DIV+`

Divergência bearish:

`DIV-`

Breakout:

`BO`

Failed breakout:

`FBO`

---

# 30. ALERTAS

Criar alertas configuráveis para:

* Strong Buy
* Strong Sell
* Spring
* Upthrust
* Absorption
* Exhaustion
* Bullish Divergence
* Bearish Divergence
* Breakout
* Failed Breakout
* Score crossing +60
* Score crossing -60

Permitir:

* som
* alerta visual
* NinjaTrader Alert
* opção de desabilitar cada alerta

---

# 31. PERFORMANCE

Este indicador será utilizado em MNQ, portanto performance é crítica.

Evitar:

* LINQ dentro de OnMarketData
* criação excessiva de objetos
* alocações desnecessárias
* loops sobre grandes históricos em cada tick
* chamadas repetitivas de indicadores
* cálculos redundantes

Utilizar:

* buffers
* arrays
* CircularBuffer
* RollingWindow
* cache
* cálculo incremental

Não recalcular toda a série a cada tick.

---

# 32. THREAD SAFETY

NinjaTrader possui processamento multithread.

Criar proteção para dados compartilhados entre:

`OnBarUpdate`

`OnMarketData`

`OnMarketDepth`

Evitar race conditions.

Não atualizar diretamente objetos gráficos a partir de threads inadequadas.

---

# 33. HISTÓRICO X REALTIME

Separar claramente:

`HistoricalMode`

`RealtimeMode`

O indicador deve detectar limitações de dados históricos.

Não fingir que existe Bid/Ask histórico quando os dados não estiverem disponíveis.

Se não houver dados suficientes:

mostrar:

`DATA QUALITY: LOW`

e reduzir a confiança.

Utilizar Tick Replay somente quando fizer sentido.

---

# 34. CONFIGURAÇÕES DO USUÁRIO

Criar propriedades:

```text
EnableWyckoff
EnableOrderFlow
EnableAbsorption
EnableImbalance
EnableCorrelation
EnableVWAP
EnableVolume
EnableMarketDepth

CorrelationPeriod
VolumeLookback
DeltaLookback
ImbalanceRatio
AbsorptionThreshold
ExhaustionThreshold

BuyThreshold
SellThreshold
StrongBuyThreshold
StrongSellThreshold

MinimumConfidence
```

Também permitir configurar pesos.

---

# 35. CONFIGURAÇÃO DE INSTRUMENTOS

Criar:

```text
PrimaryInstrument = MNQ

CorrelationInstrument1 = NQ
CorrelationInstrument2 = ES
CorrelationInstrument3 = RTY
CorrelationInstrument4 = YM
```

Não quebrar se um instrumento não estiver disponível.

---

# 36. LOG

Criar modo:

`DebugMode`

Quando ativado, registrar:

* mudanças de score
* detecção de absorção
* detecção de divergência
* eventos Wyckoff
* qualidade dos dados
* correlações
* erros

Quando desativado, não gerar logs excessivos.

---

# 37. EXPORTAÇÃO

Criar opção para exportar os eventos para CSV.

Campos:

```text
Timestamp
Instrument
Price
Volume
BidVolume
AskVolume
Delta
CumulativeDelta
AggressionScore
AbsorptionScore
ImbalanceScore
WyckoffScore
VolumeScore
VWAPScore
CorrelationScore
StructureScore
InstitutionalFlowScore
ConfidenceScore
Signal
WyckoffPhase
MarketRegime
```

Não escrever no arquivo em todo tick.

Utilizar buffer e gravação controlada.

---

# 38. REGRAS IMPORTANTES

Não utilizar IA dentro do indicador.

O indicador deve ser determinístico.

Não utilizar machine learning.

Não inventar dados ausentes.

Não confundir volume negociado com volume de ordens resting.

Não confundir agressão com intenção.

Não tratar correlação como causalidade.

Não gerar entrada apenas por um indicador.

Sempre mostrar o motivo do sinal.

---

# 39. EXPLICAÇÃO DO SINAL

Criar no dashboard uma seção:

`WHY?`

Exemplo:

```text
STRONG BUY +78

Reasons:
+ Strong positive delta
+ Aggressive buyers dominant
+ Buyer absorption at support
+ Price above VWAP
+ ES confirms
+ NQ confirms
+ Wyckoff Sign of Strength

Risks:
- RTY weak
- Price near resistance
```

Outro exemplo:

```text
SELL -71

Reasons:
+ Seller aggression
+ Negative delta acceleration
+ Failed breakout
+ Seller absorption
+ Wyckoff Upthrust
+ Price below VWAP

Risks:
- ES still bullish
```

---

# 40. CONTEXTO MULTI-TIMEFRAME

Permitir utilizar:

```text
60 minutes
15 minutes
5 minutes
1 minute
```

E execução/análise operacional em:

```text
15 seconds
```

Criar:

`HigherTimeFrameBias`

Prioridade:

60m > 15m > 5m > 1m > 15s

O timeframe maior deve funcionar como filtro de contexto.

Exemplo:

60m bullish

15m bullish

5m bullish

1m bullish

15s bullish

=> alta confluência.

Mas:

60m bearish

15m bearish

5m bullish

15s bullish

=> tratar como provável pullback, não como tendência principal.

---

# 41. SCORE MULTI-TIMEFRAME

Criar:

```text
60m Weight = 30%
15m Weight = 25%
5m Weight  = 20%
1m Weight  = 15%
15s Weight = 10%
```

Gerar:

`MultiTimeframeScore`

Também configurável.

---

# 42. RESULTADO FINAL

O indicador deverá apresentar:

```text
MARKET BIAS
FLOW SCORE
CONFIDENCE
WYCKOFF PHASE
ORDER FLOW
DELTA
ABSORPTION
IMBALANCE
VWAP
VOLUME
STRUCTURE
NQ CORRELATION
ES CORRELATION
RTY CORRELATION
MULTI-TIMEFRAME
MARKET REGIME
SIGNAL
WHY?
```

---

# 43. EXEMPLO DE SAÍDA

```text
MNQ INSTITUTIONAL FLOW

BIAS: BULLISH

FLOW SCORE: +74
CONFIDENCE: 87

WYCKOFF:
SIGN OF STRENGTH

ORDER FLOW:
BUYERS +81

DELTA:
+68

ABSORPTION:
BUYER ABSORPTION +72

IMBALANCE:
BUY +76

VWAP:
ABOVE +55

VOLUME:
1.82x average

STRUCTURE:
HH / HL

CORRELATION:

NQ: +0.94
ES: +0.82
RTY: +0.71

REGIME:
TREND UP

MULTI TF:

60m  +61
15m  +72
5m   +79
1m   +81
15s  +88

SIGNAL:

STRONG BUY

CONFIRMATION:

★★★★★

WHY?

1. Wyckoff Sign of Strength
2. Positive delta acceleration
3. Buyers dominating aggression
4. Buyer absorption detected
5. Price above VWAP
6. ES and NQ confirm
7. Multi-timeframe alignment
```

---

# 44. QUALIDADE DO CÓDIGO

O código deve:

* compilar no NinjaTrader 8
* utilizar namespaces corretos
* respeitar ciclo de vida NinjaScript
* utilizar State.SetDefaults
* State.Configure
* State.DataLoaded
* State.Historical
* State.Realtime quando necessário
* implementar Dispose/limpeza quando apropriado
* evitar dependências incompatíveis
* utilizar propriedades NinjaScript corretamente
* utilizar `[NinjaScriptProperty]` somente quando apropriado
* utilizar `[Display]`
* utilizar `[Range]`
* utilizar `Series<double>` quando necessário

Antes de finalizar:

1. revisar todas as referências
2. verificar métodos disponíveis na versão atual do NinjaTrader
3. corrigir erros de compilação
4. eliminar warnings desnecessários
5. verificar performance
6. verificar acesso concorrente
7. verificar histórico
8. verificar realtime

---

# 45. DOCUMENTAÇÃO

Criar também:

`README.md`

Explicando:

* instalação
* configuração
* parâmetros
* interpretação do score
* interpretação Wyckoff
* interpretação Delta
* interpretação agressão
* interpretação absorção
* interpretação correlação
* limitações dos dados
* histórico versus realtime
* exemplos de leitura

---

# 46. TESTES

Criar uma estrutura de testes para as engines matemáticas sempre que possível.

Testar:

* Delta
* AggressionRatio
* ZScore
* Correlation
* RelativeStrength
* Absorption
* Imbalance
* WeightedScore
* ConfidenceScore

Criar casos:

1. mercado fortemente comprador
2. mercado fortemente vendedor
3. mercado lateral
4. absorção compradora
5. absorção vendedora
6. divergência bullish
7. divergência bearish
8. breakout confirmado
9. failed breakout
10. dados incompletos
11. volume anormal
12. ausência de instrumento de correlação

---

# 47. ENTREGA

Não entregar apenas pseudocódigo.

Criar o código C# completo.

Criar todos os arquivos necessários.

Garantir que o indicador possa ser instalado no NinjaTrader 8.

Se alguma API do NinjaTrader não permitir determinada funcionalidade exatamente como especificada, não inventar a API.

Adaptar para a API real disponível e documentar a limitação.

Ao finalizar, apresentar:

1. arquivos criados
2. arquitetura
3. principais cálculos
4. parâmetros
5. instruções de instalação
6. instruções para testar no MNQ
7. limitações de dados
8. próximos pontos de melhoria.

Prioridade absoluta:

CORREÇÃO DOS DADOS > QUALIDADE DO SCORE > PERFORMANCE > VISUAL.

Não gerar sinais artificiais apenas para produzir BUY/SELL.

O indicador deve privilegiar **confluência**, **qualidade dos dados** e **contexto Wyckoff**.  o indicador é para ninjatrader não para o profit chart