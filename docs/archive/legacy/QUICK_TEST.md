# 🚀 TESTE RÁPIDO - Estratégia Quantitativa

## ✅ Pré-requisitos
- .NET 6+ instalado
- Node.js instalado
- Projeto compilando

---

## 🎯 TESTE EM 5 MINUTOS

### Passo 1: Backend

```powershell
# Navegar para a pasta da API
cd C:\Projetos\ntbot\NTBot.Api

# Compilar (verificar erros)
dotnet build

# Se compilar com sucesso, executar
dotnet run
```

**Verificar:**
- ✅ Backend rodando em `http://localhost:5000`
- ✅ Swagger acessível em `http://localhost:5000`
- ✅ Console mostrando "Application started"

---

### Passo 2: Testar API via Browser

Abrir no navegador:
```
http://localhost:5000/api/quantstrategy/dashboard?symbol=WINFUT&leaderSymbol=NQ
```

**Esperado:**
```json
{
  "symbol": "WINFUT",
  "leaderSymbol": "NQ",
  "currentPrice": 120xxx,
  "correlation": { ... },
  "gex": { ... },
  "signal": { ... } ou null,
  "recentCandles": [ ... ]
}
```

---

### Passo 3: Frontend

```powershell
# Abrir OUTRO terminal
cd C:\Projetos\ntbot\ntbot-dashboard

# Instalar dependências (se ainda não instalou)
npm install

# Executar
npm run dev
```

**Verificar:**
- ✅ Frontend rodando em `http://localhost:5173`
- ✅ Console sem erros

---

### Passo 4: Acessar Dashboard

Abrir navegador em:
```
http://localhost:5173/quant
```

**O que você deve ver:**

1. **Header**
   - Título "Estratégia Quantitativa"
   - Seletor de ativo (WIN/WDO)
   - Botão "Atualizar"

2. **Cards de Overview** (4 cards)
   - Preço Atual (com valor)
   - Bias Global (BULLISH/BEARISH/NEUTRAL com ícone)
   - Regime GEX (badge colorido)
   - Fase Wyckoff (texto)

3. **Signal Card** (se houver sinal)
   - Direção (LONG/SHORT)
   - Tipo de estratégia (BREAKOUT/MEAN_REVERSION)
   - Score de confiança (%)
   - Preços (entrada, stop, take profits)
   - Alinhamentos (barras de progresso)

4. **Gráficos** (2 colunas)
   - **Esquerda**: Correlation Chart
     - Barra de correlação
     - Bias do líder
     - EMAs
   - **Direita**: GEX Chart
     - Regime
     - Potenciais
     - Gamma walls

5. **Detalhes** (2 cards na parte inferior)
   - Detalhes da Correlação
   - Detalhes do GEX

---

## 🧪 TESTES FUNCIONAIS

### Teste 1: Trocar Ativo
1. Clicar no seletor
2. Escolher "WDO (Dólar)"
3. Verificar se os dados atualizam

### Teste 2: Atualizar Manualmente
1. Clicar em "Atualizar"
2. Verificar que loading aparece
3. Dados são recarregados

### Teste 3: APIs Individuais

**Correlação:**
```
http://localhost:5000/api/quantstrategy/correlation?leaderSymbol=NQ&followerSymbol=WINFUT&lookback=50
```

**GEX:**
```
http://localhost:5000/api/quantstrategy/gex?symbol=WINFUT
```

**Opções:**
```
http://localhost:5000/api/quantstrategy/options?symbol=WINFUT
```

---

## 🎨 VERIFICAÇÕES VISUAIS

### Cores Corretas
- ✅ Verde para BULLISH / LONG / Positivo
- ✅ Vermelho para BEARISH / SHORT / Negativo
- ✅ Azul para NEUTRAL
- ✅ Roxo para BREAKOUT
- ✅ Azul claro para MEAN_REVERSION

### Badges
- ✅ Bias Global com cor apropriada
- ✅ Regime GEX com descrição

### Ícones
- ✅ TrendingUp para alta
- ✅ TrendingDown para baixa
- ✅ Shield para gamma walls
- ✅ Target para preços

---

## 📊 INTERPRETANDO OS DADOS MOCK

Como os dados são simulados (mock), você verá:

### Dados Aleatórios mas Realistas:
- **Preço WIN**: ~120.000
- **Preço NQ**: ~16.000
- **Correlação**: Entre 0.6 e 0.9
- **GEX**: Pode ser positivo ou negativo
- **Gamma Walls**: 3-5 níveis diferentes

### Sinais Gerados:
- Nem sempre haverá sinal
- Depende do alinhamento dos 3 componentes
- Se houver sinal, confiança será > 70%

---

## 🐛 TROUBLESHOOTING RÁPIDO

### Backend não inicia:
```powershell
# Verificar erros
dotnet build

# Verificar porta ocupada
netstat -ano | findstr :5000

# Matar processo se necessário
taskkill /PID <PID> /F
```

### Frontend com erro:
```powershell
# Limpar e reinstalar
rm -r node_modules
rm package-lock.json
npm install
```

### CORS Error no console:
- Verificar se backend está rodando
- Verificar URL no `.env` do frontend

### Dados não aparecem:
- F12 no navegador → Console → verificar erros
- Verificar se API responde: `http://localhost:5000/api/health`

---

## 📸 SCREENSHOTS ESPERADOS

### Vista Geral:
```
┌──────────────────────────────────────────────────┐
│ Estratégia Quantitativa      [WIN▼] [Atualizar] │
├──────────────────────────────────────────────────┤
│ [Preço] [Bias] [GEX] [Wyckoff]                  │
├──────────────────────────────────────────────────┤
│ [Signal Card se houver]                          │
├─────────────────────────┬────────────────────────┤
│ Correlation Chart       │ GEX Chart              │
│ [Gráfico]               │ [Gráfico]              │
├─────────────────────────┼────────────────────────┤
│ Detalhes Correlação     │ Detalhes GEX           │
└─────────────────────────┴────────────────────────┘
```

---

## ✅ CRITÉRIOS DE SUCESSO

### Backend:
- ✅ Compila sem erros
- ✅ Inicia sem crashes
- ✅ APIs respondem com status 200
- ✅ Dados JSON válidos

### Frontend:
- ✅ Compila sem erros TypeScript
- ✅ Carrega sem erros no console
- ✅ Todos os componentes renderizam
- ✅ Dados aparecem nos gráficos

### Integração:
- ✅ Frontend consome APIs com sucesso
- ✅ Dados atualizados a cada 30s
- ✅ Seletor de ativo funciona
- ✅ Botão atualizar funciona

---

## 🎓 PRÓXIMOS PASSOS APÓS TESTE

1. **Se tudo funcionar:**
   - ✅ Parabéns! Sistema está operacional
   - → Ir para integração de dados reais (ver QUANT_SETUP.md)

2. **Se houver problemas:**
   - → Verificar section Troubleshooting
   - → Consultar logs do backend
   - → Verificar console do navegador

3. **Para produção:**
   - → Implementar dados reais (NQ + Opções)
   - → Fazer backtesting
   - → Configurar persistência
   - → Adicionar notificações

---

## 📞 SUPORTE

Arquivos de referência:
- `QUANT_STRATEGY_GUIDE.md` - Explicação detalhada
- `QUANT_SETUP.md` - Setup completo
- `QUANT_SUMMARY.md` - Resumo executivo

Logs:
- Backend: `NTBot.Api/logs/ntbot-<date>.txt`
- Frontend: Console do navegador (F12)

---

## 🎉 BOA SORTE!

Você está a poucos minutos de ver sua estratégia quantitativa em ação! 🚀📊

---

**Tempo estimado para este teste: 5-10 minutos**
