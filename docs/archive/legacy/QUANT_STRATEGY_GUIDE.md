# Estratégia Quantitativa - NTBot

## 📊 Visão Geral

Estratégia quantitativa completa que integra três componentes principais para geração de sinais de alta precisão:

1. **Correlação Global (NQ/WIN)** - Identifica direção e força do mercado líder
2. **Gamma Exposure (GEX)** - Detecta regimes de volatilidade e pontos de inflexão
3. **Wyckoff** - Analisa estrutura de mercado e fase de acumulação/distribuição

---

## 🏗️ Arquitetura

```
┌─────────────────────────────────────────────────────────────┐
│                    Frontend (React/TS)                       │
│  - QuantStrategyPage                                        │
│  - CorrelationChart, GEXChart, SignalCard                  │
└─────────────────────────────────────────────────────────────┘
                            ↕ REST API
┌─────────────────────────────────────────────────────────────┐
│                  QuantStrategyController                     │
│  - /api/quantstrategy/dashboard                            │
│  - /api/quantstrategy/analyze                              │
│  - /api/quantstrategy/correlation                          │
│  - /api/quantstrategy/gex                                  │
└─────────────────────────────────────────────────────────────┘
                            ↕
┌─────────────────────────────────────────────────────────────┐
│                      QuantStrategy                           │
│  Integra: Correlation + GEX + Wyckoff                      │
└─────────────────────────────────────────────────────────────┘
           ↕                    ↕                    ↕
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│ Correlation      │  │ GammaExposure    │  │ Wyckoff          │
│ Service          │  │ Service          │  │ Service          │
└──────────────────┘  └──────────────────┘  └──────────────────┘
```

---

## 🎯 Componentes

### 1. GlobalCorrelationService

Analisa a correlação entre o líder global (NQ - Nasdaq) e o ativo brasileiro (WIN/WDO).

**Cálculos:**
- Correlação de Pearson (força e direção)
- Correlação de Spearman (ranking)
- EMA 20 e EMA 50 do líder
- Momentum do líder (% change)
- Força da tendência (ADX simplificado)

**Output:**
```csharp
GlobalBias: BULLISH | BEARISH | NEUTRAL
LeaderMomentum: +2.5%
Correlation: 0.85 (85% de correlação)
TrendStrength: 67/100
```

### 2. GammaExposureService

Calcula a exposição gamma agregada do mercado de opções.

**Conceitos:**
- **GEX Positivo**: Market Makers compram quando preço sobe → compressão, mean reversion
- **GEX Negativo**: Market Makers vendem quando preço sobe → expansão, breakouts
- **Gamma Flip**: Nível onde GEX muda de sinal (pivô crítico)
- **Gamma Walls**: Concentrações de open interest (suporte/resistência)

**Regimes:**
- `POSITIVE_HIGH`: Forte compressão (mean reversion 90%)
- `POSITIVE_LOW`: Compressão leve (mean reversion 70%)
- `NEUTRAL`: Equilíbrio (50/50)
- `NEGATIVE_LOW`: Início de expansão (breakout 70%)
- `NEGATIVE_HIGH`: Alta volatilidade (breakout 90%)

**Output:**
```csharp
TotalGEX: -1250
Regime: NEGATIVE_HIGH
GammaFlipLevel: 120500
GammaWalls: [119800 (Support), 121200 (Resistance)]
ExpansionPotential: 90%
```

### 3. WyckoffService

Identifica fase de mercado segundo metodologia Wyckoff (já implementado no sistema).

**Fases:**
- Accumulation (compra institucional)
- Markup (alta)
- Distribution (venda institucional)
- Markdown (baixa)

---

## 📈 Regras de Entrada

### Estratégia 1: BREAKOUT (Continuação)

**Condições para LONG:**
```
✓ GlobalBias = BULLISH
✓ GEX Negativo (NEGATIVE_HIGH ou NEGATIVE_LOW)
✓ TrendStrength >= 25 (tendência definida)
✓ Wyckoff = ACCUMULATION ou MARKUP
✓ Preço rompeu Gamma Wall (resistência)
```

**Condições para SHORT:**
```
✓ GlobalBias = BEARISH
✓ GEX Negativo
✓ TrendStrength >= 25
✓ Wyckoff = DISTRIBUTION ou MARKDOWN
✓ Preço rompeu Gamma Wall (suporte)
```

### Estratégia 2: MEAN REVERSION (Reversão)

**Condições para LONG:**
```
✓ GEX Positivo (POSITIVE_HIGH ou POSITIVE_LOW)
✓ Preço próximo de Gamma Wall (< 1%)
✓ Momentum negativo recente
✓ Wyckoff NÃO bearish
```

**Condições para SHORT:**
```
✓ GEX Positivo
✓ Preço próximo de Gamma Wall (< 1%)
✓ Momentum positivo recente
✓ Wyckoff NÃO bullish
```

---

## 🛡️ Gestão de Risco

### Stop Loss
- **Breakout**: ATR × 2.0
- **Mean Reversion**: ATR × 1.4 (stops mais apertados)

### Take Profit
- **TP1 (50% da posição)**: ATR × 2.5
- **TP2 (50% restante)**: ATR × 4.0

### Score de Confiança

O sinal só é gerado se `ConfidenceScore >= 70%`.

**Cálculo:**
```csharp
ConfidenceScore = 
  (Correlação × 30%) +      // Peso: 30%
  (GEX Alignment × 30%) +   // Peso: 30%
  (Wyckoff Score × 20%) +   // Peso: 20%
  (Trend Strength × 20%)    // Peso: 20%
```

---

## 🚀 Como Usar

### Backend (C#)

1. **Registrar serviços no `Program.cs`:**

```csharp
// Services
builder.Services.AddScoped<IGlobalCorrelationService, GlobalCorrelationService>();
builder.Services.AddScoped<IGammaExposureService, GammaExposureService>();
builder.Services.AddScoped<IWyckoffService, WyckoffService>();
builder.Services.AddScoped<QuantStrategy>();

// HttpClient para APIs externas
builder.Services.AddHttpClient<GlobalCorrelationService>();
```

2. **Chamar a API:**

```bash
# Obter dashboard completo
GET /api/quantstrategy/dashboard?symbol=WINFUT&leaderSymbol=NQ

# Analisar e gerar sinal
POST /api/quantstrategy/analyze
{
  "symbol": "WINFUT",
  "leaderSymbol": "NQ"
}

# Obter correlação
GET /api/quantstrategy/correlation?leaderSymbol=NQ&followerSymbol=WINFUT

# Obter GEX
GET /api/quantstrategy/gex?symbol=WINFUT
```

### Frontend (React)

**Acessar a página:**
```
http://localhost:5173/quant
```

**Usar o serviço:**
```typescript
import { quantStrategyApi } from '@/services/quantStrategyApi';

// Obter dashboard
const data = await quantStrategyApi.getDashboard('WINFUT', 'NQ');

// Gerar sinal
const signal = await quantStrategyApi.analyze('WINFUT', 'NQ');
```

---

## 📊 Visualizações

### Dashboard Principal
- **Overview Cards**: Preço, Bias Global, Regime GEX, Fase Wyckoff
- **Signal Card**: Sinal gerado com confiança, níveis de entrada/saída
- **Correlation Chart**: Gráfico de correlação com EMAs
- **GEX Chart**: Distribuição de gamma e walls

### Gráficos

1. **Correlação**: Barra de intensidade, bias do líder, momentum
2. **GEX**: Regime, potenciais (expansão vs mean reversion), gamma walls
3. **Sinal**: Direção, confiança, alinhamento dos 3 componentes

---

## 🔧 Configuração de Dados

### Fontes de Dados Necessárias

**1. Dados do NQ (Nasdaq Futures):**
- Yahoo Finance API (gratuito, limitado)
- Alpha Vantage (API key necessária)
- Interactive Brokers TWS API
- NinjaTrader Market Data

**2. Dados de Opções (para GEX):**
- B3 (para opções brasileiras WIN/WDO)
- CBOE DataShop
- Interactive Brokers
- TD Ameritrade API
- HistoricalOptionData.com

**3. Implementar integração real:**

Editar `GlobalCorrelationService.GetLeaderDataAsync`:
```csharp
public async Task<List<Candle>> GetLeaderDataAsync(string symbol, int periods)
{
    // Substituir por integração real:
    // - NinjaTrader ATI
    // - Yahoo Finance
    // - Alpha Vantage
    // etc.
}
```

Editar `GammaExposureService.GetOptionsDataAsync`:
```csharp
public async Task<List<OptionData>> GetOptionsDataAsync(string symbol)
{
    // Substituir por integração real:
    // - B3 OpLab
    // - CBOE
    // - Interactive Brokers
    // etc.
}
```

---

## 📝 Exemplo de Sinal Gerado

```json
{
  "id": "...",
  "symbol": "WINFUT",
  "direction": "LONG",
  "strategyType": "BREAKOUT",
  "confidenceScore": 85.5,
  
  "globalBias": "BULLISH",
  "gexRegime": "NEGATIVE_HIGH",
  "wyckoffPhase": "ACCUMULATION",
  
  "entryPrice": 119850,
  "stopLoss": 119350,
  "takeProfit1": 120475,
  "takeProfit2": 121350,
  "riskRewardRatio": 3.0,
  
  "correlationStrength": 82,
  "gexAlignment": 90,
  "wyckoffAlignment": 85,
  
  "observations": [
    "Correlação NQ/WIN: 0.82",
    "GEX Total: -1450",
    "Gamma Flip: 120200",
    "Wyckoff Phase: ACCUMULATION",
    "Trend Strength: 68.5"
  ]
}
```

---

## 🎓 Interpretação dos Sinais

### Alta Confiança (85%+)
- Todos os componentes alinhados
- Correlação forte (> 0.75)
- GEX confirmando a direção
- Wyckoff em fase apropriada

### Média Confiança (70-85%)
- 2 de 3 componentes alinhados
- Correlação moderada (0.6-0.75)
- Executar com cautela

### Baixa Confiança (< 70%)
- Componentes conflitantes
- Sinal NÃO é gerado
- Aguardar melhor setup

---

## 🚨 Avisos Importantes

⚠️ **Dados Mock**: A implementação atual usa dados simulados. Para produção, implementar integrações reais.

⚠️ **Backtesting**: Realizar backtesting extensivo antes de usar com capital real.

⚠️ **Gerenciamento de Risco**: Sempre usar stop loss e respeitar o tamanho de posição adequado ao seu capital.

⚠️ **Mercado Real**: Resultados passados não garantem resultados futuros. Trading envolve risco.

---

## 🔄 Próximos Passos

1. ✅ Estrutura base implementada
2. ⏳ Integrar fontes de dados reais
3. ⏳ Implementar persistência de sinais (banco de dados)
4. ⏳ Adicionar backtesting engine
5. ⏳ Implementar execução automática via NinjaTrader
6. ⏳ Adicionar notificações (email, telegram, push)
7. ⏳ Dashboard de performance (win rate, profit factor, etc.)

---

## 📚 Referências

- **Wyckoff Method**: Richard D. Wyckoff
- **Gamma Exposure**: SpotGamma, Squeezemetrics
- **Correlação de Mercados**: "Trading and Exchanges" by Larry Harris
- **Opções**: "The Volatility Surface" by Jim Gatheral

---

## 👨‍💻 Suporte

Para dúvidas ou problemas:
1. Verificar logs em `NTBot.Api/logs/`
2. Verificar console do navegador (F12)
3. Verificar erros no backend (Program.cs)

---

**Desenvolvido para NTBot - Trading Automatizado**
Data: Abril 2026
