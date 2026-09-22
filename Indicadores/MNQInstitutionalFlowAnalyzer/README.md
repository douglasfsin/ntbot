# MNQInstitutionalFlowAnalyzer (NinjaTrader 8)

Indicador **NinjaTrader 8 / NinjaScript** de fluxo institucional para **MNQ** (Micro E-mini Nasdaq-100). Não é script Profit Chart.

Especificação-fonte: `docs/ninja/MNQInstitutionalFlowAnalyzer-prompt.md`.

Math engines (sem DLL do NT8) compilam em `Core/NtBot.MnqFlow.Core.csproj`. O arquivo `MNQInstitutionalFlowAnalyzer.cs` só compila **dentro** do NinjaTrader.

---

## Instalação (PT-BR)

**Passo a passo completo (use este):** [README-INSTALACAO-NT8.md](README-INSTALACAO-NT8.md)

Resumo: o NinjaScript Editor **compila todos os `.cs`** sob `bin\Custom`. O indicador só entra na lista Available se `MNQInstitutionalFlowAnalyzer.cs` estiver **flat** em `Custom\Indicators\` (mesmo nível que `@ADL.cs`). Engines ficam na subpasta `MNQInstitutionalFlowAnalyzer\Engines\` etc.

```powershell
cd C:\Projetos\ntbot\Indicadores\MNQInstitutionalFlowAnalyzer
powershell -ExecutionPolicy Bypass -File .\deploy-to-nt8.ps1
```

Depois: fechar/reabrir NT8 → NinjaScript Editor → abrir o **script** flat (não só a pasta) → **F5** → gráfico MNQ → Indicators → buscar `MNQ` / `Institutional`.

**CS0579** = segundo `AssemblyInfo` / `*AssemblyAttributes.cs`. Oficial (manter): `Custom\AssemblyInfo.cs`. Apague duplicatas em `Core\obj\...`.

### Indicador não aparece na lista?

| Causa | O que fazer |
|--------|-------------|
| `.cs` principal dentro da subpasta | Mover para `Indicators\MNQInstitutionalFlowAnalyzer.cs` (flat) e recompilar F5 |
| Falta `#region NinjaScript generated code` | Recompilar F5 no NinjaScript Editor (ou criar indicador pelo Wizard e colar a lógica) |
| Erro em `OnStateChange` ao abrir a lista | Aba **Log** do Control Center — corrigir erro (símbolos inválidos, etc.) |
| `MEAI.cs` wrapper vazio | Apagar `Indicators\MEAI.cs` — herda o mesmo `Name` e confunde; **não** crie wrapper vazio salvo se quiser outro nome e `SetDefaults` próprio |
| Nome ≠ classe ≠ arquivo | Os três devem ser `MNQInstitutionalFlowAnalyzer` |
| Compilação com CS0579 | Segundo `AssemblyInfo` — ver seção CS0579 acima |

### Remover pasta "AltaRenda" da lista de indicadores

A pasta **AltaRenda** no diálogo Indicators vem de scripts em `Custom\Indicators\AltaRenda\` (namespace `NinjaTrader.NinjaScript.Indicators.AltaRenda`). É pacote de terceiros / curso — **não** faz parte deste repositório.

**Nesta instalação (OneDrive):** não há pasta `AltaRenda` em `Custom\Indicators\` — se você a vê no NT8, confira se está em outro caminho de Custom ou outra máquina.

Remoção segura:

1. **Backup:** copie `Documents\NinjaTrader 8\bin\Custom` (ou `OneDrive\Documentos\...`) inteira.
2. **Feche** o NinjaTrader 8.
3. Apague a pasta `Custom\Indicators\AltaRenda\` (ou o nome exato que aparece no Explorer do NinjaScript Editor).
4. Abra `Custom\NinjaTrader.Custom.csproj` e remova linhas `<Compile Include="Indicators\AltaRenda\..." />` se ainda existirem.
5. Reinicie NT8 → NinjaScript Editor → Compile (F5).
6. A pasta AltaRenda some da lista Available. Indicadores nativos (`@EMA`, etc.) permanecem.

**Não** apague pastas `@*` — são indicadores oficiais do NinjaTrader.

### Tick Replay / Level 1

- Bid/Ask aggression usa `OnMarketData` (`MarketDataType.Last` no ask = compra agressora; no bid = venda).
- Em histórico **sem Tick Replay**, o indicador **não inventa** Bid/Ask: mostra `DATA QUALITY: LOW` e reduz o peso de order flow.
- Para backtest de agressão: ative **Tick Replay** no Data Series e `Calculate = On each tick`.

### Market Depth

`OnMarketDepth` só entra se `EnableMarketDepth = true`. Não há garantia de ordem entre depth e last.

---

## Installation (EN)

Copy **only** `MNQInstitutionalFlowAnalyzer.cs` to `Custom\Indicators\` (flat, same level as `@EMA.cs`) plus `Engines/`, `Helpers/`, `Math/`, `Models/` under `Custom\Indicators\MNQInstitutionalFlowAnalyzer\` (or run `deploy-to-nt8.ps1`). Do **not** put the main `.cs` inside the subfolder — NT8 will not generate NinjaScript wrappers and the indicator will not appear in the UI list. Do **not** copy `Core/` (`.csproj`, `obj`, generated `AssemblyInfo`), `Tests/`, or any extra `AssemblyInfo.cs`. **CS0579** means a second assembly-info file is compiled with NT8 Custom — search Custom for `AssemblyInfo.cs`; keep only `Custom\AssemblyInfo.cs`. Compile in the NinjaScript Editor (F5); NT8 auto-appends `#region NinjaScript generated code`. Search **MNQ** or **Institutional** in the Indicators dialog. Delete empty `MEAI.cs` wrapper unless you override `Name` in `SetDefaults`. Set correlation symbols to valid NT8 instrument names. Historical Bid/Ask requires Tick Replay.

This repo does **not** ship fake NinjaTrader assemblies; the indicator class will not compile with `dotnet build` on the main solution.

---

## Parâmetros principais

| Grupo | Defaults |
|--------|----------|
| Pesos | Wyckoff 25, Aggression 20, Delta 15, Absorption 10, Imbalance 10, Volume 5, VWAP 5, Correlation 5, Regime 5 |
| Lookbacks | Volume 50, Correlation 50, Delta 50 |
| ImbalanceRatio | 3.0 |
| Sinais | Buy ≥ 60, Strong ≥ 75, Sell ≤ -60, Confidence ≥ 70 |
| MTF | 60m 30% / 15m 25% / 5m 20% / 1m 15% / 15s 10% |
| Calculate (padrão) | **OnPriceChange** — tape/OF continua em `OnMarketData` |
| Visual | `EnablePaintBars=false`, `MaxMarkerBars=150`, `DashboardThrottleMs=250` |

### Performance / freeze no gráfico (MNQ live)

Causa clássica NT8: `Draw.TextFixed` + `Draw.Text` (tags por barra) + `BarBrushes` a cada tick, com objetos acumulando.

| Setting | Recomendado live | Custo |
|---------|------------------|--------|
| `Calculate` | **OnPriceChange** (padrão) ou **OnBarClose** (mais leve) | `OnEachTick` = score/dashboard a cada print |
| `EnablePaintBars` | **false** | Alto — pinta o painel de preço |
| `EnableSignalBanner` | true | Médio — redesenho só com throttle/mudança |
| `MaxMarkerBars` | 150 | Remove SPRING/UT/BA/… antigos |
| `DashboardThrottleMs` | 250–500 | Limita TextFixed sob tick/price |
| `EnableMultiTimeframe` | desligar se chart já estiver pesado | Séries extras |

`OnMarketData` classifica Bid/Ask independentemente do `Calculate`. Use `OnEachTick` só se precisar do score no dashboard a cada negócio.

`IsTradeSetupValid(bool isBuy)` exige score, confiança, estrutura, delta, agressão, Wyckoff e ausência de absorção oposta.

---

## Cores do viés (DisplayBias — só UI)

Gates de trade (`Classify` / `IsTradeSetupValid`) continuam estritos (±60, conf ≥ 70). O **DisplayBias** só pinta banner/dashboard:

| Texto PT | Cor | Quando |
|----------|-----|--------|
| COMPRA FORTE / COMPRA | verde forte | score ≥ 75/60 + conf OK |
| VIÉS COMPRA | verde claro | WeakBuy **ou** WAIT com score ≳ +18…+30 |
| AGUARDAR | dourado | WAIT com \|score\| pequeno |
| VIÉS VENDA | vermelho claro | WeakSell **ou** WAIT com score ≲ −18…−30 |
| VENDA / VENDA FORTE | vermelho forte | score ≤ −60/−75 + conf OK |
| SEM TRADE / NEUTRO | cinza / branco | conf muito baixa / neutro |

## Interpretação

- **InstitutionalFlowScore** −100…+100: pressão vendedora ↔ compradora.
- **ConfidenceScore** 0–100: qualidade + confluência. Score alto + confiança baixa ≠ strong buy.
- **Wyckoff**: fases + Spring/Upthrust scores (preço + volume + delta + absorção).
- **Delta / agressão**: tape classificado; não é o Cumulative Delta nativo copiado.
- **Absorção**: agressão forte + preço quase parado (BA/SA).
- **Correlação**: Pearson de **retornos** MNQ vs NQ/ES/RTY (YM opcional). Correlação ≠ causalidade.
- Dashboard **WHY?** / **RISKS** no gráfico (`Draw.TextFixed`). Histograma no painel inferior (score + confidence). Marcadores: SPRING, UT, BA, SA, DIV+, DIV−, BO, FBO.

CSV opcional: buffer, flush em blocos para `UserDataDir\MNQInstitutionalFlowAnalyzer.csv`.

---

## Limitações (API real NT8)

- Sem Tick Replay, Last vs Bid/Ask histórico não é confiável.
- `AddDataSeries` quebra se o símbolo não existir na base NT8 — desligue o instrumento.
- MTF adiciona 60/15/5/1 min e 15s no **mesmo** instrumento do gráfico. Scores HTF no código usam proxy OHLC no `BarsInProgress` secundário (não reprocessam tape em cada TF).
- ADX/ATR: indicadores nativos NT8 na série primária (`ADX(14)`, `ATR(14)`).
- Volume Profile completo por preço não está no núcleo (imbalance usa bid/ask da barra ou best depth).
- Sem LINQ em `OnMarketData`; lock entre bar/data/depth; desenhos só em `OnBarUpdate`.

Testes matemáticos: `dotnet test tests/NtBot.UnitTests --filter MnqFlow`.
